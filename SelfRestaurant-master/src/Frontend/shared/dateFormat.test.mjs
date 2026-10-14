import { test } from "node:test";
import assert from "node:assert/strict";
import { formatViDate, formatViDateTime, formatViIsoDate, formatViTime } from "./dateFormat.ts";

test("formats local date and time as HH:mm, dd/MM/yyyy", () => {
  const date = new Date(2026, 9, 13, 18, 54, 7);
  assert.equal(formatViDateTime(date), "18:54, 13/10/2026");
  assert.equal(formatViDate(date), "13/10/2026");
  assert.equal(formatViTime(new Date(2026, 0, 2, 9, 5)), "09:05");
});

test("returns the fallback for missing or invalid dates", () => {
  assert.equal(formatViDateTime(null), "-");
  assert.equal(formatViDateTime(new Date("nope"), "Chưa cập nhật"), "Chưa cập nhật");
});

test("calendar-day strings are reordered without time-zone shifts", () => {
  assert.equal(formatViIsoDate("2026-10-14"), "14/10/2026");
  assert.equal(formatViIsoDate("2026-09-22T00:00:00"), "22/09/2026");
  assert.equal(formatViIsoDate(""), "-");
  assert.equal(formatViIsoDate("hôm nay"), "hôm nay");
});
