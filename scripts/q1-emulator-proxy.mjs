// Local-only QA bridge for a protected Vercel Preview. No credential is stored
// here: VERCEL_OIDC_TOKEN must be injected into this process's environment.
import http from 'node:http';
import https from 'node:https';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const WebSocket = require('../SelfRestaurant-master/src/Frontend/selfrestaurant-cashier-web/node_modules/ws');
const WebSocketServer = WebSocket.Server;
const candidate = new URL('https://viettable-k8j32aegw-hienkhanhvu981-7145.vercel.app');
const token = process.env.VERCEL_OIDC_TOKEN;
const port = Number(process.env.VIETTABLE_QA_PROXY_PORT || 48127);
if (!token || !Number.isInteger(port) || port < 1024 || port > 65535) {
  throw new Error('Missing local Preview authentication or invalid QA port.');
}

const allowed = (path) => /^\/(api|hubs|healthz|readyz)(\/|\?|$)/.test(path);
const outboundHeaders = (incoming) => {
  const headers = { ...incoming, host: candidate.host,
    'x-vercel-trusted-oidc-idp-token': token };
  for (const name of ['connection', 'upgrade', 'transfer-encoding', 'content-length',
    'sec-websocket-key', 'sec-websocket-version', 'sec-websocket-extensions',
    'sec-websocket-protocol']) {
    delete headers[name];
  }
  return headers;
};

const server = http.createServer((request, response) => {
  if (!allowed(request.url || '')) { response.writeHead(404).end(); return; }
  const upstream = https.request(new URL(request.url, candidate), {
    method: request.method,
    headers: outboundHeaders(request.headers),
    timeout: 65000,
  }, (result) => {
    const headers = { ...result.headers };
    delete headers['set-cookie']; // The native app uses Bearer tokens, not staff cookies.
    response.writeHead(result.statusCode || 502, headers);
    result.pipe(response);
  });
  upstream.on('timeout', () => upstream.destroy(new Error('upstream_timeout')));
  upstream.on('error', () => {
    if (!response.headersSent) response.writeHead(502).end();
    else response.destroy();
  });
  request.pipe(upstream);
});

const socketServer = new WebSocketServer({ noServer: true });
server.on('upgrade', (request, socket, head) => {
  socket.on('error', () => { /* A rejected/closed QA socket must not stop the bridge. */ });
  if (!allowed(request.url || '') || !request.url.startsWith('/hubs/')) {
    socket.end('HTTP/1.1 404 Not Found\r\n\r\n');
    return;
  }
  const target = new URL(request.url, candidate);
  target.protocol = 'wss:';
  const upstream = new WebSocket(target, {
    headers: outboundHeaders(request.headers),
    handshakeTimeout: 15000,
    followRedirects: false,
  });
  let client;
  upstream.once('unexpected-response', (_request, response) => {
    // Preserve the upstream auth status; never forward its body or headers.
    if (!socket.destroyed) socket.end(`HTTP/1.1 ${response.statusCode || 502} Rejected\r\nContent-Length: 0\r\n\r\n`);
    response.resume();
  });
  upstream.once('open', () => {
    socketServer.handleUpgrade(request, socket, head, (accepted) => {
      client = accepted;
      client.on('message', (data, binary) => {
        if (upstream.readyState === WebSocket.OPEN) upstream.send(data, { binary });
      });
      upstream.on('message', (data, binary) => {
        if (client.readyState === WebSocket.OPEN) client.send(data, { binary });
      });
      client.on('close', () => upstream.close());
      upstream.on('close', () => client.close());
      client.on('error', () => upstream.terminate());
    });
  });
  upstream.once('error', () => {
    if (client) client.terminate();
    else socket.end('HTTP/1.1 502 Bad Gateway\r\n\r\n');
  });
});

server.listen(port, '127.0.0.1', () => {
  process.stdout.write(`QA bridge ready on loopback port ${port}; target=candidate Preview\n`);
});
