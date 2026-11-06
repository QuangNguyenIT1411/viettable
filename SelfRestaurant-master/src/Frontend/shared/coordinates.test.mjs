import { test } from "node:test";
import assert from "node:assert/strict";
import { parseCoordinates } from "./coordinates.ts";

test("accepts dot, comma and a pasted pair", () => {
  assert.deepEqual(parseCoordinates("10.776889", "106.700806"), { latitude: 10.776889, longitude: 106.700806, error: null });
  assert.deepEqual(parseCoordinates("10,7769", "106,7009"), { latitude: 10.7769, longitude: 106.7009, error: null });
  assert.deepEqual(parseCoordinates("10.776889, 106.700806", ""), { latitude: 10.776889, longitude: 106.700806, error: null });
});

test("both empty clears; one missing, out of range, zero or text is refused", () => {
  assert.deepEqual(parseCoordinates("", ""), { latitude: null, longitude: null, error: null });
  assert.ok(parseCoordinates("10.7", "").error);
  assert.ok(parseCoordinates("91", "106").error);
  assert.ok(parseCoordinates("10", "181").error);
  assert.ok(parseCoordinates("0", "0").error);
  assert.ok(parseCoordinates("abc", "106").error);
  assert.ok(parseCoordinates("10.1234567", "106").error);
});
