import { test } from "node:test";
import assert from "node:assert/strict";
import { formatVnd, parseVnd } from "./money.ts";

test("thousands separators never become a decimal point", () => {
  assert.equal(parseVnd("300.000"), 300_000);
  assert.equal(parseVnd("300,000"), 300_000);
  assert.equal(parseVnd("300 000đ"), 300_000);
  assert.equal(parseVnd("1.250.000"), 1_250_000);
  assert.equal(parseVnd("300000"), 300_000);
});

test("empty is zero, text without digits is not a number", () => {
  assert.equal(parseVnd(""), 0);
  assert.equal(parseVnd(null), 0);
  assert.ok(Number.isNaN(parseVnd("abc")));
  assert.ok(Number.isNaN(parseVnd("-5000")));
});

test("the hint under the box reads like money", () => {
  assert.match(formatVnd(300_000), /^300.000đ$/);
});
