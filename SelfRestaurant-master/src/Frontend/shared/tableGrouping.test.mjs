import { test } from "node:test";
import assert from "node:assert/strict";
import { linkedNumbers, parseLinkedTables } from "./tableGrouping.ts";

const rows = [
  { tableId: 11, tableNumber: 5, numberOfSeats: 4, area: "Tầng 1", linkedTableIds: [12] },
  { tableId: 12, tableNumber: 6, numberOfSeats: 4, area: "Tầng 1", linkedTableIds: [11] },
  { tableId: 13, tableNumber: 7, numberOfSeats: 6, area: null, linkedTableIds: [] },
];

test("table numbers become ids", () => {
  assert.deepEqual(parseLinkedTables("6, 7", rows[0], rows), { ids: [12, 13], error: null });
  assert.deepEqual(parseLinkedTables("6;6 7", rows[0], rows), { ids: [12, 13], error: null });
  assert.deepEqual(parseLinkedTables("", rows[0], rows), { ids: [], error: null });
});

test("unknown numbers, the table itself and text are refused", () => {
  assert.ok(parseLinkedTables("9", rows[0], rows).error);
  assert.ok(parseLinkedTables("5", rows[0], rows).error);
  assert.ok(parseLinkedTables("bàn", rows[0], rows).error);
});

test("ids read back as sorted numbers", () => {
  assert.equal(linkedNumbers([13, 12], rows), "6, 7");
});
