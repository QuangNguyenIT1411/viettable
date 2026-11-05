import { test } from "node:test";
import assert from "node:assert/strict";
import { amountRows, paymentRows, receiptHtml, money, percent, timeRange, RECEIPT_TITLE, NOT_VAT_INVOICE } from "./receipt.ts";

const snapshot = {
  invoiceId: 44, branchName: "Chi nhánh Quận 1", branchAddress: "123 Nguyễn Huệ", tableName: "Bàn 6",
  sessionStartedAtUtc: "2026-11-05T12:02:00Z", paidAtUtc: "2026-11-05T13:15:00Z", cashierName: "Lan",
  items: [{ dishId: 1, dishName: "Phở bò", quantity: 2, unitPrice: 65000, lineTotal: 130000 }],
  subtotal: 500000, promotionName: "Khai trương", promotionDiscount: 50000, voucherDiscount: 0, pointsUsed: 2000, pointsDiscount: 20000,
  serviceFeePercent: 5, serviceFeeAmount: 21500, vatPercent: 8, vatAmount: 36120, depositApplied: 300000, grossTotal: 487620,
  totalAmount: 187620, amountPaid: 187620, paymentMethod: "TRANSFER", bankTransactionRef: "FT2611", pointsEarned: 487,
};

test("rows follow the formula and match the app's receipt", () => {
  assert.deepEqual(amountRows(snapshot).map(r => `${r.label} = ${r.value}`), [
    "Tạm tính = 500.000đ", "Khuyến mãi \"Khai trương\" = −50.000đ", "Dùng 2.000 điểm = −20.000đ",
    "Phí phục vụ (5%) = 21.500đ", "VAT (8%) = 36.120đ", "Tổng hoá đơn = 487.620đ", "Đã đặt cọc = −300.000đ",
    "Tổng thanh toán = 187.620đ", "Đã trả = 187.620đ",
  ]);
  assert.deepEqual(paymentRows(snapshot).map(r => r.value), ["Chuyển khoản", "FT2611", "+487 điểm"]);
});

test("an old bill shows no fee or VAT row and invents no percent", () => {
  const rows = amountRows({ invoiceId: 3, items: [], subtotal: 130000, serviceFeePercent: null, vatPercent: null, totalAmount: 130000 });
  assert.ok(!rows.some(r => r.label.startsWith("Phí phục vụ") || r.label.startsWith("VAT")));
});

test("the page says PHIẾU THANH TOÁN and not a VAT invoice", () => {
  const html = receiptHtml(snapshot);
  assert.ok(html.includes(RECEIPT_TITLE));
  assert.ok(html.includes(NOT_VAT_INVOICE));
  assert.ok(!/hoá đơn điện tử|hóa đơn điện tử/i.test(html));
  assert.ok(html.includes("80mm"));
});

test("money, percent and Vietnam time", () => {
  assert.equal(money(1234567), "1.234.567đ");
  assert.equal(percent(7.5), "7,5%");
  assert.equal(timeRange("2026-11-05T12:02:00Z", "2026-11-05T13:15:00Z"), "19:02 – 20:15, 05/11/2026");
  const paidOnly = receiptHtml({ ...snapshot, sessionStartedAtUtc: null, paidAtUtc: "2026-11-05T13:15:00Z" });
  assert.ok(paidOnly.includes("Thanh toán lúc") && !paidOnly.includes("Giờ vào"));
});
