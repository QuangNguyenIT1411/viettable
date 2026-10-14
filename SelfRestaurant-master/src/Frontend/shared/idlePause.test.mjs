import { test } from "node:test";
import assert from "node:assert/strict";
import { IDLE_PAUSE_MS, createIdleMonitor } from "./idlePause.ts";

function clock() {
  let now = 0, next = 1;
  const timers = new Map();
  return {
    environment: {
      now: () => now,
      schedule: (callback, delay) => { const id = next++; timers.set(id, { callback, at: now + delay }); return id; },
      cancel: id => timers.delete(id),
    },
    advance(ms) {
      const until = now + ms;
      for (;;) {
        const due = [...timers.entries()].filter(([, t]) => t.at <= until).sort((a, b) => a[1].at - b[1].at)[0];
        if (!due) break;
        timers.delete(due[0]); now = due[1].at; due[1].callback();
      }
      now = until;
    },
    pending: () => timers.size,
  };
}

test("pauses after 15 minutes without any user action", () => {
  assert.equal(IDLE_PAUSE_MS, 15 * 60 * 1000);
  const c = clock(); let idle = 0;
  createIdleMonitor({ timeout: IDLE_PAUSE_MS, idle: () => idle++ }, c.environment);
  c.advance(IDLE_PAUSE_MS - 1); assert.equal(idle, 0);
  c.advance(1); assert.equal(idle, 1);
  c.advance(IDLE_PAUSE_MS * 3); assert.equal(idle, 1, "fires once");
});

test("each user action restarts the 15 minutes", () => {
  const c = clock(); let idle = 0;
  const monitor = createIdleMonitor({ timeout: 1000, idle: () => idle++ }, c.environment);
  c.advance(900); monitor.activity();
  c.advance(900); assert.equal(idle, 0);
  c.advance(100); assert.equal(idle, 1);
});

test("dispose cancels the timer", () => {
  const c = clock(); let idle = 0;
  const monitor = createIdleMonitor({ timeout: 1000, idle: () => idle++ }, c.environment);
  monitor.dispose(); c.advance(5000);
  assert.equal(idle, 0); assert.equal(c.pending(), 0);
});
