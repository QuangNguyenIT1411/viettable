import { test } from "node:test";
import assert from "node:assert/strict";
import { createControlledRead } from "./controlledRead.ts";
const flush = async () => { for (let i = 0; i < 12; i++) await Promise.resolve(); };
function deferred() {
  let resolve, reject;
  const promise = new Promise((ok, fail) => { resolve = ok; reject = fail; });
  return { promise, resolve, reject };
}
function fixture(interval = 4000) {
  const calls = [], values = [], errors = [], timers = new Map();
  let visible = true, nextId = 0;
  const loop = createControlledRead({
    read: signal => { const task = deferred(); calls.push({ ...task, signal }); return task.promise; },
    apply: value => values.push(value), error: error => errors.push(error), interval,
  }, { visible: () => visible, schedule: (fn, delay) => { const id = ++nextId; timers.set(id, { fn, delay }); return id; }, cancel: id => timers.delete(id) });
  return { loop, calls, values, errors, timers, visible: value => { visible = value; loop.visibilityChanged(); } };
}
test("event burst while reading coalesces to exactly one trailing REST resync", async () => {
  const f = fixture(); const done = f.loop.refresh(); await flush();
  for (let i = 0; i < 20; i++) void f.loop.refresh();
  assert.equal(f.calls.length, 1); f.calls[0].resolve("old"); await flush();
  assert.equal(f.calls.length, 2); f.calls[1].resolve("new"); await done;
  assert.deepEqual(f.values, ["old", "new"]); assert.equal(f.timers.size, 1); f.loop.dispose();
});
test("next poll starts after completion, not on overlapping interval", async () => {
  const f = fixture(); const done = f.loop.refresh(); await flush();
  assert.equal(f.timers.size, 0); f.calls[0].resolve(1); await done;
  const timer = [...f.timers.values()][0]; assert.equal(timer.delay, 4000);
  timer.fn(); await flush(); assert.equal(f.calls.length, 2); f.loop.dispose(); f.calls[1].resolve(2); await flush();
});
test("hidden tab suppresses reads; foreground performs canonical resync", async () => {
  const f = fixture(); f.visible(false); await f.loop.refresh(); assert.equal(f.calls.length, 0);
  f.visible(true); await flush(); assert.equal(f.calls.length, 1);
  f.calls[0].resolve(1); await flush(); f.visible(false); assert.equal(f.timers.size, 0); f.loop.dispose();
});
test("dispose aborts fetch and suppresses late results, errors and timers", async () => {
  const f = fixture(); const done = f.loop.refresh(); await flush(); f.loop.dispose();
  assert.equal(f.calls[0].signal.aborted, true); f.calls[0].resolve("late"); await done;
  assert.deepEqual(f.values, []); assert.deepEqual(f.errors, []); assert.equal(f.timers.size, 0);
  await f.loop.refresh(); assert.equal(f.calls.length, 1);
});
test("failure backs off without replaying queued reads, success resets delay", async () => {
  const f = fixture(); const done = f.loop.refresh(); await flush(); void f.loop.refresh();
  f.calls[0].reject(new Error("offline")); await done;
  assert.equal(f.calls.length, 1); assert.equal(f.errors.length, 1);
  assert.equal([...f.timers.values()][0].delay, 8000);
  const retry = f.loop.refresh(); await flush(); f.calls[1].resolve(2); await retry;
  assert.equal([...f.timers.values()][0].delay, 4000); f.loop.dispose();
});
test("interval may change between polls: realtime up = slow fallback, realtime down = faster", async () => {
  let delay = 10000;
  const f = fixture(() => delay); const first = f.loop.refresh(); await flush();
  f.calls[0].resolve(1); await first;
  assert.equal([...f.timers.values()][0].delay, 10000);
  delay = 60000; const second = f.loop.refresh(); await flush();
  f.calls[1].resolve(2); await second;
  assert.equal([...f.timers.values()][0].delay, 60000); f.loop.dispose();
});
