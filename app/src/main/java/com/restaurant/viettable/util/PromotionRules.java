package com.restaurant.viettable.util;

import com.restaurant.viettable.model.Promotion;

import java.time.Instant;
import java.time.ZoneId;
import java.time.format.DateTimeFormatter;
import java.util.Locale;

/** Texts for the promotion banner, the "Ưu đãi" list and the detail (the server decides who gets what). */
public final class PromotionRules {
    private PromotionRules() { }

    private static final ZoneId VIETNAM = ZoneId.of("Asia/Ho_Chi_Minh");

    /** Big badge on the card: "−10%" or "−30.000đ". */
    public static String badge(Promotion p) {
        if ("FIXED".equalsIgnoreCase(p.getDiscountType())) return "−" + DepositRules.money(Math.round(p.getDiscountValue()));
        double v = p.getDiscountValue();
        return "−" + (v == Math.rint(v) ? String.valueOf((long) v) : String.format(Locale.ROOT, "%.1f", v)) + "%";
    }

    /** "Đến 23:59, 30/10/2026" in Vietnam time; empty when unknown. */
    public static String until(String endsAtUtc) {
        Instant end = DisplayFormatters.instant(endsAtUtc);
        if (end == null) return "";
        return "Đến " + end.atZone(VIETNAM).format(DateTimeFormatter.ofPattern("HH:mm, dd/MM/yyyy"));
    }

    /** "Áp dụng: Mọi chi nhánh" or "Áp dụng tại chi nhánh đang chọn". */
    public static String where(Promotion p) {
        return p.getBranchIds().isEmpty() ? "Áp dụng: mọi chi nhánh" : "Áp dụng tại chi nhánh đang chọn";
    }

    /** The detail's condition line. */
    public static String condition(Promotion p) {
        String min = p.getMinSubtotal() > 0 ? "Hóa đơn từ " + DepositRules.money(p.getMinSubtotal()) : "Mọi hóa đơn";
        String cap = "PERCENT".equalsIgnoreCase(p.getDiscountType()) && p.getMaxDiscount() > 0
                ? " · giảm tối đa " + DepositRules.money(p.getMaxDiscount()) : "";
        return min + cap + ". Tự áp vào hóa đơn khi đủ điều kiện; mỗi hóa đơn 1 khuyến mãi tốt nhất, không cộng dồn với voucher.";
    }
}
