import { test } from "node:test";
import assert from "node:assert/strict";
import { paymentMethodGroup, paymentMethodLabel } from "./paymentMethod.ts";

test("cash and bank transfer are the two methods staff see", () => {
  assert.equal(paymentMethodLabel("CASH"), "Tiền mặt");
  for (const code of ["QR", "TRANSFER", "transfer", "BANK_TRANSFER"]) assert.equal(paymentMethodLabel(code), "Chuyển khoản");
  assert.equal(paymentMethodGroup("QR"), "TRANSFER");
  assert.equal(paymentMethodGroup(" cash "), "CASH");
});

test("older codes keep a readable label and count as other", () => {
  assert.equal(paymentMethodLabel("CARD"), "Thẻ");
  assert.equal(paymentMethodLabel("VNPAY"), "VNPAY");
  assert.equal(paymentMethodLabel(null), "Khác");
  assert.equal(paymentMethodGroup("VNPAY"), "OTHER");
});
