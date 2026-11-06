import { test } from "node:test";
import assert from "node:assert/strict";
import { arrivalBadge } from "./arrivalStatus.ts";

test("open booking shows the guest's answer or that none came", () => {
  assert.deepEqual(arrivalBadge("Confirmed", "WillCome"), { label: "Khách xác nhận sẽ đến", tone: "success" });
  assert.deepEqual(arrivalBadge("Pending", "None"), { label: "Chưa phản hồi", tone: "neutral" });
  assert.deepEqual(arrivalBadge("PendingDeposit", null), { label: "Chưa phản hồi", tone: "neutral" });
});

test("a cancel from the reminder is named; closed bookings show nothing", () => {
  assert.deepEqual(arrivalBadge("Cancelled", "Cancelled"), { label: "Khách huỷ từ lời nhắc", tone: "danger" });
  assert.equal(arrivalBadge("Cancelled", "None"), null);
  assert.equal(arrivalBadge("CheckedIn", "WillCome"), null);
  assert.equal(arrivalBadge("NoShow", "WillCome"), null);
});
