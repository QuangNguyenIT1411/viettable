import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseSample, summarize, groupByProcess } from './preview-request-benchmark.mjs';

const context = { deployment: 'https://fixture.vercel.app', path: '/api/mobile/v1/invoices',
  began: '2026-10-11T00:00:00Z', ended: '2026-10-11T00:00:01Z', exit: 0 };
test('metadata whitelist excludes body, cookie and curl URL credentials', () => {
  const r = parseSample('HTTP/1.1 200 OK\r\nSet-Cookie: fixture-private\r\nX-VietTable-Instance: instance-a\r\n\r\n{"password":"fixture-private"}\nMETA' +
    JSON.stringify({ http_code: 200, exitcode: 0, time_total: 1, url_effective: 'https://fixture/secret' }), context);
  assert.equal(r.sample.instance, 'instance-a');
  assert.equal(r.sample.http, 200);
  assert.ok(!JSON.stringify(r.sample).includes('fixture-private'));
  assert.ok(!JSON.stringify(r.sample).includes('/secret'));
});
test('curl timeout retains failure status and timing instead of throwing away evidence', () => {
  const r = parseSample('\nMETA' + JSON.stringify({ http_code: 0, exitcode: 28, time_total: 65 }), { ...context, exit: 28 });
  assert.equal(r.sample.curlExit, 28);
  assert.equal(r.sample.seconds, 65);
  const s = summarize([r.sample]);
  assert.equal(s.failed, 1);
  assert.equal(s.errorRate, 1);
  assert.equal(s.p95, null);
});
test('CLI failure without curl metadata remains an explicit failed sample', () => {
  assert.equal(parseSample('fixture CLI failure', { ...context, exit: 1 }).sample.error, 'missing-curl-metadata');
});
test('nearest-rank percentiles use all successful samples, with failures in denominator', () => {
  const rows = [4, 1, 3, 2].map(seconds => ({ ...context, http: 200, seconds, error: null }));
  rows.push({ ...context, exit: 28, http: 0, seconds: 65, error: null });
  assert.deepEqual(summarize(rows), { samples: 5, successful: 4, failed: 1, errorRate: 0.2, p50: 2, p95: 4 });
});

test('cold, route first-use, warm and instance switches cannot share a latency cohort', () => {
  const row = (instance, sequence, extra = {}) => ({ ...context, instance, sequence,
    http: 200, seconds: 1, error: null, ...extra });
  const grouped = groupByProcess([row('a', 1), row('a', 2), row('b', 4), row('b', 5),
    row('c', 7, { path: '/other' }), row('c', 8, { http: 401 }), row(null, null)]);
  assert.equal(grouped.firstProcess.length, 1);
  assert.equal(grouped.warmSameProcess.length, 2);
  assert.equal(grouped.crossInstance.length, 1);
  assert.equal(grouped.firstObservedRoute.length, 1);
  assert.equal(grouped.failed.length, 1);
  assert.equal(grouped.unclassified.length, 1);
});

test('failed auth cannot prime a route and another deployment cannot prime the same instance', () => {
  const row = extra => ({ ...context, instance: 'a', sequence: 2, http: 200, error: null, ...extra });
  const g = groupByProcess([row({ http: 401 }), row({}),
    row({ deployment: 'https://another.vercel.app' })]);
  assert.equal(g.firstObservedRoute.length, 2);
  assert.equal(g.warmSameProcess.length, 0);
});
