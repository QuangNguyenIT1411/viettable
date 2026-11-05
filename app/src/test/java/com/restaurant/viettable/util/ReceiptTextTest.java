package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

import com.restaurant.viettable.model.Receipt;

import org.json.JSONObject;
import org.junit.Test;

import java.util.ArrayList;
import java.util.List;

public class ReceiptTextTest {
    private static final String SNAPSHOT = "{\"invoiceId\":44,\"invoiceCode\":\"INV-abc\",\"snapshotVersion\":1,\"branchId\":1,"
            + "\"branchName\":\"Chi nhánh Quận 1\",\"branchAddress\":\"123 Nguyễn Huệ\",\"tableName\":\"Bàn 6\","
            + "\"sessionStartedAtUtc\":\"2026-11-05T12:02:00Z\",\"paidAtUtc\":\"2026-11-05T13:15:00Z\",\"cashierName\":\"Lan\","
            + "\"items\":[{\"dishId\":1,\"dishName\":\"Phở bò\",\"quantity\":2,\"unitPrice\":65000,\"lineTotal\":130000},"
            + "{\"dishId\":2,\"dishName\":\"Bún chả\",\"quantity\":1,\"unitPrice\":370000,\"lineTotal\":370000}],"
            + "\"itemCount\":3,\"subtotal\":500000,\"promotionName\":\"Khai trương\",\"promotionDiscount\":50000,"
            + "\"voucherDiscount\":0,\"pointsUsed\":2000,\"pointsDiscount\":20000,\"serviceFeePercent\":5,\"serviceFeeAmount\":21500,"
            + "\"vatPercent\":8,\"vatAmount\":36120,\"depositApplied\":300000,\"grossTotal\":487620,\"totalAmount\":187620,"
            + "\"amountPaid\":187620,\"paymentMethod\":\"TRANSFER\",\"bankTransactionRef\":\"FT2611\",\"pointsEarned\":487}";

    private static List<String> labels(List<ReceiptText.Row> rows) {
        List<String> result = new ArrayList<>();
        for (ReceiptText.Row row : rows) result.add(row.label + " = " + row.value);
        return result;
    }

    @Test
    public void everyAmountComesFromTheSnapshotInFormulaOrder() throws Exception {
        Receipt r = Receipt.fromJson(new JSONObject(SNAPSHOT));
        List<String> rows = labels(ReceiptText.amountRows(r));
        assertEquals("Tạm tính = 500.000đ", rows.get(0));
        assertEquals("Khuyến mãi \"Khai trương\" = −50.000đ", rows.get(1));
        assertEquals("Dùng 2.000 điểm = −20.000đ", rows.get(2));
        assertEquals("Phí phục vụ (5%) = 21.500đ", rows.get(3));
        assertEquals("VAT (8%) = 36.120đ", rows.get(4));
        assertEquals("Tổng hoá đơn = 487.620đ", rows.get(5));
        assertEquals("Đã đặt cọc = −300.000đ", rows.get(6));
        assertEquals("Tổng thanh toán = 187.620đ", rows.get(7));
        assertEquals("Đã trả = 187.620đ", rows.get(8));
        assertEquals(9, rows.size()); // no "Voucher 0đ", no "Tiền thừa"
    }

    @Test
    public void paymentRowsShowTransferCodeAndEarnedPoints() throws Exception {
        List<String> rows = labels(ReceiptText.paymentRows(Receipt.fromJson(new JSONObject(SNAPSHOT))));
        assertEquals("Phương thức = Chuyển khoản", rows.get(0));
        assertEquals("Mã giao dịch ngân hàng = FT2611", rows.get(1));
        assertEquals("Điểm được cộng = +487 điểm", rows.get(2));
    }

    @Test
    public void oldInvoiceHasNoFeeOrVatRowAndNoInventedPercent() throws Exception {
        Receipt r = Receipt.fromJson(new JSONObject("{\"invoiceId\":3,\"snapshotVersion\":0,\"subtotal\":130000,"
                + "\"serviceFeePercent\":null,\"serviceFeeAmount\":0,\"vatPercent\":null,\"vatAmount\":0,\"totalAmount\":130000,"
                + "\"pointsEarned\":null,\"paymentMethod\":\"CASH\"}"));
        assertNull(r.serviceFeePercent);
        for (String row : labels(ReceiptText.amountRows(r))) {
            assertFalse(row, row.startsWith("Phí phục vụ"));
            assertFalse(row, row.startsWith("VAT"));
        }
        assertEquals(1, ReceiptText.paymentRows(r).size()); // method only; no "+0 điểm"
    }

    @Test
    public void zeroPercentBranchShowsNoChargeRows() throws Exception {
        Receipt r = Receipt.fromJson(new JSONObject("{\"subtotal\":100000,\"serviceFeePercent\":0,\"vatPercent\":0,\"totalAmount\":100000}"));
        assertEquals(3, ReceiptText.amountRows(r).size()); // Tạm tính, Tổng thanh toán, Đã trả
    }

    @Test
    public void percentAndMoneyAndMonth() {
        assertEquals("5%", ReceiptText.percent(5));
        assertEquals("7,5%", ReceiptText.percent(7.5));
        assertEquals("1.234.567đ", ReceiptText.money(1_234_567));
        assertEquals("2026-11", ReceiptText.monthKey("2026-10-31T17:30:00Z")); // 00:30 on 01/11 in Vietnam
        assertEquals("Tháng 11/2026", ReceiptText.monthTitle("2026-11"));
        assertEquals("19:02 – 20:15, 05/11/2026", ReceiptText.timeRange("2026-11-05T12:02:00Z", "2026-11-05T13:15:00Z"));
        assertEquals("20:15, 05/11/2026", ReceiptText.timeRange(null, "2026-11-05T13:15:00Z"));
        assertEquals("Giờ vào – thanh toán", ReceiptText.timeLabel("2026-11-05T12:02:00Z"));
        assertEquals("Thanh toán lúc", ReceiptText.timeLabel(null));
    }

    @Test
    public void cardMetaAndFileName() throws Exception {
        Receipt r = Receipt.fromJson(new JSONObject(SNAPSHOT));
        assertEquals("Bàn 6 • 3 món • Chuyển khoản", ReceiptText.cardMeta(r));
        assertEquals("phieu-thanh-toan-44.pdf", ReceiptText.fileName(r));
        assertTrue(ReceiptText.TITLE.equals("PHIẾU THANH TOÁN"));
        assertEquals("Không phải hoá đơn GTGT", ReceiptText.NOT_VAT_INVOICE);
    }
}
