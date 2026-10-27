import { test } from "node:test";
import assert from "node:assert/strict";
import { canMarkRefunded, depositStatusLabel, depositTone, sortDeposits } from "./depositStatus.ts";

test("every deposit state has a Vietnamese label", () => {
  assert.equal(depositStatusLabel("REFUND_REQUIRED"), "Cần hoàn cọc");
  assert.equal(depositStatusLabel("refunded"), "Đã hoàn cọc");
  assert.equal(depositStatusLabel("FORFEITED"), "Mất cọc");
  assert.equal(depositStatusLabel("APPLIED"), "Đã trừ vào hóa đơn");
  assert.equal(depositStatusLabel(null), "Không rõ");
});

test("only a deposit waiting for a refund can be marked refunded", () => {
  assert.equal(canMarkRefunded("REFUND_REQUIRED"), true);
  for (const s of ["PAID", "REFUNDED", "FORFEITED", "PENDING", "APPLIED", undefined]) assert.equal(canMarkRefunded(s), false);
  assert.equal(depositTone("REFUND_REQUIRED"), "warning");
  assert.equal(depositTone("PAID"), "success");
  assert.equal(depositTone("FORFEITED"), "neutral");
});

test("refunds to do come first, then the newest", () => {
  const item = (id, status) => ({ deposit: { reservationDepositId: id, status }, reservation: null });
  const sorted = sortDeposits([item(1, "PAID"), item(2, "REFUND_REQUIRED"), item(3, "APPLIED")]);
  assert.deepEqual(sorted.map(x => x.deposit.reservationDepositId), [2, 3, 1]);
});
