import { test } from "node:test";
import assert from "node:assert/strict";
import { ALL_STAFF_TOPICS, bindStaffRealtime, createTopicBus } from "./staffRealtime.ts";

function deferred() {
  let resolve, reject;
  const promise = new Promise((ok, fail) => { resolve = ok; reject = fail; });
  return { promise, resolve, reject };
}
// Flush promises without sleeps or network connections.
const flush = async () => { for (let i = 0; i < 8; i++) await Promise.resolve(); };
function fixture() {
  const start = deferred();
  const calls = [], errors = [], changes = [], live = [];
  const connection = {
    state: "Connected", listeners: new Map(), reconnect: null, reconnecting: null, close: null,
    on(name, callback) { this.listeners.set(name, callback); },
    off(name, callback) { assert.equal(this.listeners.get(name), callback); this.listeners.delete(name); },
    onreconnecting(callback) { this.reconnecting = callback; },
    onreconnected(callback) { this.reconnect = callback; },
    onclose(callback) { this.close = callback; },
    start() { return start.promise; },
    stop() { calls.push(["stop"]); return Promise.resolve(); },
    invoke(...args) { calls.push(args); return Promise.resolve(); },
  };
  const dispose = bindStaffRealtime(connection, 7, {
    changed: topics => changes.push([...topics]),
    live: value => live.push(value),
    error: error => errors.push(error),
  });
  return { connection, calls, start, dispose, errors, changes, live };
}

test("initial connect joins the branch, then resyncs every topic and reports live", async () => {
  const f = fixture(); f.start.resolve(); await flush();
  assert.deepEqual(f.calls, [["SubscribeBranch", 7]]);
  assert.deepEqual(f.changes, [[...ALL_STAFF_TOPICS]]);
  assert.deepEqual(f.live, [true]); f.dispose();
});
test("branch events forward only their topics; the older cashier event maps to 'dashboard'", async () => {
  const f = fixture(); f.start.resolve(); await flush(); f.changes.length = 0;
  f.connection.listeners.get("staffBranchChanged")({ branchId: 7, topics: ["orders"] });
  f.connection.listeners.get("cashierDashboardChanged")({ orderId: 1 });
  f.connection.listeners.get("staffBranchChanged")({ branchId: 7, topics: [42] });
  assert.deepEqual(f.changes, [["orders"], ["dashboard"], [...ALL_STAFF_TOPICS]]); f.dispose();
});
test("reconnecting reports not live; reconnect rejoins the same branch and resyncs", async () => {
  const f = fixture(); f.start.resolve(); await flush();
  f.connection.reconnecting(); f.connection.reconnect(); await flush();
  assert.deepEqual(f.calls, [["SubscribeBranch", 7], ["SubscribeBranch", 7]]);
  assert.deepEqual(f.live, [true, false, true]);
  assert.equal(f.changes.length, 2); f.dispose();
});
test("a closed connection reports not live", async () => {
  const f = fixture(); f.start.resolve(); await flush(); f.connection.close();
  assert.deepEqual(f.live, [true, false]); f.dispose();
});
test("unmount before start completes stops and never subscribes or refreshes", async () => {
  const f = fixture(); f.dispose(); f.start.resolve(); await flush();
  f.connection.reconnect(); await flush();
  assert.deepEqual(f.calls, [["stop"]]); assert.deepEqual(f.changes, []); assert.equal(f.connection.listeners.size, 0);
});
test("unmount during subscribe suppresses delayed refresh", async () => {
  const f = fixture(), join = deferred(); f.connection.invoke = () => join.promise;
  f.start.resolve(); await flush(); f.dispose(); join.resolve(); await flush();
  assert.deepEqual(f.changes, []);
});
test("subscription rejection reports an error and not live, not false success", async () => {
  const f = fixture(); const error = new Error("denied");
  f.connection.invoke = () => Promise.reject(error); f.start.resolve(); await flush();
  assert.deepEqual(f.errors, [error]); assert.deepEqual(f.changes, []); assert.deepEqual(f.live, [false]); f.dispose();
});
test("start failure after disposal does not update unmounted UI", async () => {
  const f = fixture(); f.dispose(); f.start.reject(new Error("stopped")); await flush();
  assert.deepEqual(f.errors, []); assert.deepEqual(f.changes, []);
});
test("topic bus calls each listener once per event and supports unsubscribe", () => {
  const bus = createTopicBus(); const seen = [];
  const both = () => seen.push("both");
  const off = bus.on("orders", () => seen.push("orders"));
  bus.on("orders", both); bus.on("onsitePayments", both);
  bus.emit(["orders", "onsitePayments"]);
  assert.deepEqual(seen, ["orders", "both"]);
  off(); seen.length = 0; bus.emit(["orders"]);
  assert.deepEqual(seen, ["both"]);
});
