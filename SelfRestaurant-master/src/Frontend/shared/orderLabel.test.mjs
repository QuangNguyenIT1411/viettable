import { test } from "node:test";
import assert from "node:assert/strict";
import { orderLabel } from "./orderLabel.ts";

test("uses the short order number when an id is known", () => {
  assert.equal(orderLabel(45, "ORD-20261013115122-C4B434EDF95347B"), "Đơn #45");
  assert.equal(orderLabel("45", null), "Đơn #45");
});

test("falls back to the code, then a dash, when no valid id exists", () => {
  assert.equal(orderLabel(null, "ORD-20261013115122-C4B434EDF95347B"), "ORD-20261013115122-C4B434EDF95347B");
  assert.equal(orderLabel(0, "  "), "-");
  assert.equal(orderLabel("abc", undefined), "-");
});
