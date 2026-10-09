import { test } from "node:test";
import assert from "node:assert/strict";
import { bindStaffRealtime } from "../src/lib/staffRealtime.ts";

function deferred() {
  let resolve, reject;
  const promise = new Promise((ok, fail) => { resolve = ok; reject = fail; });
  return { promise, resolve, reject };
}
// Flush promises without sleeps or network connections.
const flush = async () => { for (let i = 0; i < 8; i++) await Promise.resolve(); };
function fixture() {
  const start = deferred();
  const calls = [], errors = [];
  let refreshes = 0;
  const connection = {
    state: "Connected", listener: null, reconnect: null,
    on(name, callback) { assert.equal(name, "cashierDashboardChanged"); this.listener = callback; },
    off(name, callback) { assert.equal(this.listener, callback); this.listener = null; },
    onreconnected(callback) { this.reconnect = callback; },
    start() { return start.promise; },
    stop() { calls.push(["stop"]); return Promise.resolve(); },
    invoke(...args) { calls.push(args); return Promise.resolve(); },
  };
  const dispose = bindStaffRealtime(connection, 7, () => refreshes++, error => errors.push(error));
  return { connection, calls, start, dispose, errors, refreshes: () => refreshes };
}

test("initial connect joins authorized branch then REST resyncs; event triggers refresh", async () => {
  const f = fixture(); f.start.resolve(); await flush();
  assert.deepEqual(f.calls, [["SubscribeBranch", 7]]); assert.equal(f.refreshes(), 1);
  f.connection.listener(); assert.equal(f.refreshes(), 2); f.dispose();
});
test("reconnect rejoins same branch and REST resyncs", async () => {
  const f = fixture(); f.start.resolve(); await flush(); f.connection.reconnect(); await flush();
  assert.deepEqual(f.calls, [["SubscribeBranch", 7], ["SubscribeBranch", 7]]);
  assert.equal(f.refreshes(), 2); f.dispose();
});
test("unmount before start completes stops and never subscribes or refreshes", async () => {
  const f = fixture(); f.dispose(); f.start.resolve(); await flush();
  f.connection.reconnect(); await flush();
  assert.deepEqual(f.calls, [["stop"]]); assert.equal(f.refreshes(), 0); assert.equal(f.connection.listener, null);
});
test("unmount during subscribe suppresses delayed refresh", async () => {
  const f = fixture(), join = deferred(); f.connection.invoke = () => join.promise;
  f.start.resolve(); await flush(); f.dispose(); join.resolve(); await flush();
  assert.equal(f.refreshes(), 0);
});
test("subscription rejection reports an error, not false success", async () => {
  const f = fixture(); const error = new Error("denied");
  f.connection.invoke = () => Promise.reject(error); f.start.resolve(); await flush();
  assert.deepEqual(f.errors, [error]); assert.equal(f.refreshes(), 0); f.dispose();
});
test("start failure after disposal does not update unmounted UI", async () => {
  const f = fixture(); f.dispose(); f.start.reject(new Error("stopped")); await flush();
  assert.deepEqual(f.errors, []); assert.equal(f.refreshes(), 0);
});
