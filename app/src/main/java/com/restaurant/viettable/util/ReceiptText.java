package com.restaurant.viettable.util;

import androidx.annotation.Nullable;

import com.restaurant.viettable.model.Receipt;

import java.math.BigDecimal;
import java.text.DecimalFormat;
import java.text.DecimalFormatSymbols;
import java.time.Instant;
import java.time.ZoneId;
import java.time.ZonedDateTime;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * Words and rows of an invoice for the detail screen, the history card and the "PHIẾU THANH TOÁN" PDF (owner 05/11).
 * Every number comes from {@link Receipt} (the server's snapshot); nothing is added up here. A row with nothing in it
 * (0đ discount, no fee on that bill, an old invoice with no percentage) is left out instead of showing "0đ".
 */
public final class ReceiptText {
    private ReceiptText() { }

    public static final String TITLE = "PHIẾU THANH TOÁN";
    /** The receipt is not a VAT invoice and must say so (owner 05/11). */
    public static final String NOT_VAT_INVOICE = "Không phải hoá đơn GTGT";

    public enum Kind { NORMAL, DISCOUNT, TOTAL, PAID }

    public static final class Row {
        public final String label;
        public final String value;
        public final Kind kind;

        Row(String label, String value, Kind kind) {
            this.label = label;
            this.value = value;
            this.kind = kind;
        }
    }

    private static final ZoneId VIETNAM = ZoneId.of("Asia/Ho_Chi_Minh");

    public static String money(long value) {
        DecimalFormatSymbols symbols = new DecimalFormatSymbols(Locale.ROOT);
        symbols.setGroupingSeparator('.');
        return new DecimalFormat("#,##0", symbols).format(value) + "đ";
    }

    /** "5%", "7,5%", "8%" — the percentage the bill was paid with. */
    public static String percent(double value) {
        String text = BigDecimal.valueOf(value).stripTrailingZeros().toPlainString().replace('.', ',');
        return text + "%";
    }

    /** The money rows from subtotal to what was paid, in the formula's order (docs/CHECKOUT_FORMULA.md). */
    public static List<Row> amountRows(Receipt r) {
        List<Row> rows = new ArrayList<>();
        rows.add(new Row("Tạm tính", money(r.subtotal), Kind.NORMAL));
        if (r.promotionDiscount > 0)
            rows.add(new Row(r.promotionName == null ? "Khuyến mãi" : "Khuyến mãi \"" + r.promotionName + "\"",
                    "−" + money(r.promotionDiscount), Kind.DISCOUNT));
        if (r.voucherDiscount > 0)
            rows.add(new Row(r.voucherCode == null ? "Voucher" : "Voucher " + r.voucherCode, "−" + money(r.voucherDiscount), Kind.DISCOUNT));
        if (r.manualDiscount > 0) rows.add(new Row("Giảm giá", "−" + money(r.manualDiscount), Kind.DISCOUNT));
        if (r.pointsDiscount > 0)
            rows.add(new Row("Dùng " + points(r.pointsUsed) + " điểm", "−" + money(r.pointsDiscount), Kind.DISCOUNT));
        if (r.serviceFeePercent != null && (r.serviceFeePercent > 0 || r.serviceFeeAmount > 0))
            rows.add(new Row("Phí phục vụ (" + percent(r.serviceFeePercent) + ")", money(r.serviceFeeAmount), Kind.NORMAL));
        if (r.vatPercent != null && (r.vatPercent > 0 || r.vatAmount > 0))
            rows.add(new Row("VAT (" + percent(r.vatPercent) + ")", money(r.vatAmount), Kind.NORMAL));
        if (r.depositApplied > 0) {
            rows.add(new Row("Tổng hoá đơn", money(r.grossTotal), Kind.NORMAL));
            rows.add(new Row("Đã đặt cọc", "−" + money(r.depositApplied), Kind.DISCOUNT));
        }
        rows.add(new Row("Tổng thanh toán", money(r.totalAmount), Kind.TOTAL));
        rows.add(new Row("Đã trả", money(r.amountPaid), Kind.PAID));
        if (r.changeAmount > 0) rows.add(new Row("Tiền thừa", money(r.changeAmount), Kind.NORMAL));
        return rows;
    }

    /** Payment lines: method (+ the bank's transaction code for a transfer) and the points this bill earned. */
    public static List<Row> paymentRows(Receipt r) {
        List<Row> rows = new ArrayList<>();
        String method = method(r.paymentMethod);
        if (!method.isEmpty()) rows.add(new Row("Phương thức", method, Kind.NORMAL));
        if (r.bankTransactionRef != null) rows.add(new Row("Mã giao dịch ngân hàng", r.bankTransactionRef, Kind.NORMAL));
        if (r.pointsEarned != null && r.pointsEarned > 0) rows.add(new Row("Điểm được cộng", "+" + points(r.pointsEarned) + " điểm", Kind.NORMAL));
        return rows;
    }

    public static String method(@Nullable String value) {
        if (value == null) return "";
        switch (value.trim().toUpperCase(Locale.ROOT)) {
            case "CASH": return "Tiền mặt";
            case "QR": case "TRANSFER": case "BANK_TRANSFER": return "Chuyển khoản";
            case "CARD": return "Thẻ";
            case "VNPAY": return "VNPAY";
            default: return value.trim();
        }
    }

    public static String points(long value) {
        DecimalFormatSymbols symbols = new DecimalFormatSymbols(Locale.ROOT);
        symbols.setGroupingSeparator('.');
        return new DecimalFormat("#,##0", symbols).format(value);
    }

    /** "19:02 – 20:15, 05/11/2026" (or only the payment time when the start is unknown). */
    public static String timeRange(@Nullable String startedAtUtc, @Nullable String paidAtUtc) {
        ZonedDateTime paid = parse(paidAtUtc);
        if (paid == null) return "";
        String end = two(paid.getHour()) + ":" + two(paid.getMinute()) + ", " + two(paid.getDayOfMonth()) + "/" + two(paid.getMonthValue()) + "/" + paid.getYear();
        ZonedDateTime start = parse(startedAtUtc);
        if (start == null) return end;
        return two(start.getHour()) + ":" + two(start.getMinute()) + " – " + end;
    }

    /** Group key and title of the history list: "2026-11" / "Tháng 11/2026" (Vietnam time). */
    public static String monthKey(@Nullable String issuedAtUtc) {
        ZonedDateTime at = parse(issuedAtUtc);
        return at == null ? "" : at.getYear() + "-" + two(at.getMonthValue());
    }

    public static String monthTitle(String monthKey) {
        if (monthKey == null || monthKey.length() != 7) return "Không rõ tháng";
        return "Tháng " + Integer.parseInt(monthKey.substring(5)) + "/" + monthKey.substring(0, 4);
    }

    /** Second line of a history card: "Bàn 6 • 3 món • Chuyển khoản". */
    public static String cardMeta(Receipt r) {
        StringBuilder text = new StringBuilder();
        if (r.tableName != null) text.append(r.tableName);
        if (r.itemCount > 0) text.append(text.length() > 0 ? " • " : "").append(r.itemCount).append(" món");
        String method = method(r.paymentMethod);
        if (!method.isEmpty()) text.append(text.length() > 0 ? " • " : "").append(method);
        return text.toString();
    }

    public static String status(@Nullable String paymentStatus) {
        if (paymentStatus == null) return "Đã thanh toán";
        switch (paymentStatus.trim().toUpperCase(Locale.ROOT)) {
            case "SUCCEEDED": case "PAID": case "COMPLETED": return "Đã thanh toán";
            case "REFUNDED": return "Đã hoàn tiền";
            default: return StatusLabels.paymentStatus(paymentStatus);
        }
    }

    /** File name of the PDF: "phieu-thanh-toan-44.pdf". */
    public static String fileName(Receipt r) {
        return "phieu-thanh-toan-" + (r.invoiceId > 0 ? String.valueOf(r.invoiceId) : "hoa-don") + ".pdf";
    }

    @Nullable
    private static ZonedDateTime parse(@Nullable String utc) {
        if (utc == null || utc.trim().isEmpty()) return null;
        try {
            String value = utc.trim();
            if (!value.endsWith("Z") && !value.contains("+") && value.length() >= 19) value = value + "Z";
            return Instant.parse(value).atZone(VIETNAM);
        } catch (Exception ignored) {
            return null;
        }
    }

    private static String two(int value) {
        return value < 10 ? "0" + value : String.valueOf(value);
    }
}
