import { test } from "node:test";
import assert from "node:assert/strict";
import { CASHIER_TABS, badgeText, tabAccessibleName, tabFromHash } from "./cashierTabs.ts";

test("the address remembers the open tab; unknown or empty opens the counter", () => {
  assert.equal(tabFromHash("#cho-thu-tien"), "cho-thu-tien");
  assert.equal(tabFromHash("dat-ban"), "dat-ban");
  assert.equal(tabFromHash(""), "quay");
  assert.equal(tabFromHash("#khong-co"), "quay");
  assert.equal(tabFromHash(undefined), "quay");
});

test("five tabs, the counter first", () => {
  assert.deepEqual(CASHIER_TABS.map((t) => t.key), ["quay", "cho-thu-tien", "dat-ban", "phuc-vu", "chuyen-khoan"]);
});

test("a waiting count is read as a whole phrase, not a bare number", () => {
  const onsite = CASHIER_TABS.find((t) => t.key === "cho-thu-tien");
  assert.equal(tabAccessibleName(onsite, 2), "Chờ thu tiền, 2 giao dịch chờ thu tiền");
  assert.equal(tabAccessibleName(onsite, 0), "Chờ thu tiền");
  assert.equal(tabAccessibleName(CASHIER_TABS[0], 5), "Quầy thanh toán");
});

test("big counts stay short on the badge", () => {
  assert.equal(badgeText(7), "7");
  assert.equal(badgeText(120), "99+");
});
