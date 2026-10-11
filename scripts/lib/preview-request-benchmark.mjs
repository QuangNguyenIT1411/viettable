import { spawn } from 'node:child_process';

// Explicit target per call: never capture a mutable deployment/global credential.
// The raw response stays in caller memory for auth/mapping checks. Persist only
// `sample`: no response body, stderr, cookie, JWT, SQL or connection string.
export function parseSample(output, { deployment, path, began, ended, exit }) {
  const marker = output.lastIndexOf('\nMETA');
  let meta = {};
  try { if (marker >= 0) meta = JSON.parse(output.slice(marker + 5)); } catch { /* reported below */ }
  const raw = marker >= 0 ? output.slice(0, marker) : '';
  const header = name => raw.match(new RegExp(`^${name}:\\s*(.+)$`, 'mi'))?.[1]?.trim() ?? null;
  return { raw, sample: {
    deployment, path, began, ended, exit,
    http: meta.http_code ?? null, curlExit: meta.exitcode ?? null,
    seconds: meta.time_total ?? null, ttfb: meta.time_starttransfer ?? null,
    bytes: meta.size_download ?? null,
    instance: header('x-viettable-instance'), invocation: header('x-vercel-id'),
    request: header('x-viettable-request'), sequence: Number(header('x-viettable-request-sequence')) || null,
    ageMs: Number(header('x-viettable-process-age-ms')) || null,
    timing: header('server-timing'),
    error: marker < 0 || typeof meta.http_code !== 'number' ? 'missing-curl-metadata' : null
  }};
}

export function summarize(samples) {
  const successful = samples.filter(x => x.exit === 0 && x.http >= 200 && x.http < 300
    && x.error === null && Number.isFinite(x.seconds));
  const values = successful.map(x => x.seconds).sort((a, b) => a - b);
  const percentile = p => values.length ? values[Math.ceil(values.length * p) - 1] : null;
  return { samples: samples.length, successful: successful.length,
    failed: samples.length - successful.length,
    errorRate: samples.length ? (samples.length - successful.length) / samples.length : null,
    // Successful-response latency only; failed samples/timings remain in original array.
    p50: percentile(0.5), p95: percentile(0.95) };
}

export async function previewRequest({ nodeExecutable, vercelEntrypoint, cwd, deployment, path,
  cookie, token, body }) {
  const url = new URL(deployment);
  if (url.protocol !== 'https:' || !url.hostname.endsWith('.vercel.app') || url.origin !== deployment
      || !path.startsWith('/') || path.startsWith('//') || /(?:token|password|secret)=/i.test(path))
    throw new Error('Explicit Preview origin and credential-free relative path required.');
  const config = [];
  if (body !== undefined) config.push('request = "POST"', 'header = "Content-Type: application/json"',
    `data = ${JSON.stringify(JSON.stringify(body))}`);
  if (cookie) config.push(`header = ${JSON.stringify('Cookie: ' + cookie)}`);
  if (token) config.push(`header = ${JSON.stringify('Authorization: Bearer ' + token)}`);
  const began = new Date().toISOString();
  return new Promise(resolve => {
    const child = spawn(nodeExecutable, [vercelEntrypoint, 'curl', path, '--deployment', deployment,
      '--scope', 'hienkhanhvu981-7145', '--', '--silent', '--show-error', '--include',
      '--max-time', '65', '--config', '-', '--write-out', '\nMETA%{json}'], { cwd });
    let output = '';
    child.stdout.on('data', data => { output += data; });
    // Drain, never print/store raw diagnostics which may contain credentials.
    child.stderr.on('data', () => {});
    child.on('error', () => resolve(parseSample('', {
      deployment, path, began, ended: new Date().toISOString(), exit: null })));
    child.on('close', exit => resolve(parseSample(output, {
      deployment, path, began, ended: new Date().toISOString(), exit })));
    child.stdin.end(config.join('\n'));
  });
}
