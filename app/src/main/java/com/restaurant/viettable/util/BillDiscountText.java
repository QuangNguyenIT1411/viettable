package com.restaurant.viettable.util;

import androidx.annotation.Nullable;

import com.restaurant.viettable.model.Voucher;

import java.util.Locale;

/**
 * Texts of the bill's discount rows and of the voucher cards (owner 28/10/2026): an automatic promotion is named
 * "Khuyến mãi tự động: <tên>" so the guest never takes it for a voucher; a voucher is shown only when the guest
 * picked it; greyed vouchers say why.
 */
public final class BillDiscountText {
    private BillDiscountText() { }

    public static final String PROMOTION_DECLINE = "Không dùng khuyến mãi";
    public static final String PROMOTION_USE_AGAIN = "Dùng lại khuyến mãi";
    public static final String REMOVE_VOUCHER = "Bỏ voucher";

    public static String money(long value) { return String.format(Locale.GERMANY, "%,dđ", value); }

    public static String promotionLabel(@Nullable String name) {
        return name == null || name.trim().isEmpty() ? "Khuyến mãi tự động" : "Khuyến mãi tự động: " + name.trim();
    }

    /** "-50.000đ" when taken off; "Không dùng" when the guest declined it; "Không áp" when the voucher is better. */
    public static String promotionAmount(long applied, boolean declined) {
        if (declined) return "Không dùng";
        return applied > 0 ? "-" + money(applied) : "Không áp";
    }

    public static String promotionToggle(boolean declined) { return declined ? PROMOTION_USE_AGAIN : PROMOTION_DECLINE; }

    public static String voucherLabel(@Nullable String code) {
        return code == null || code.trim().isEmpty() ? "Voucher" : "Voucher " + code.trim();
    }

    /** "-30.000đ", or "Không áp" when the promotion is better (never both). */
    public static String voucherAmount(long applied) { return applied > 0 ? "-" + money(applied) : "Không áp"; }

    /** Second line of a voucher card: "CHAOBAN50 · Đơn từ 300.000đ" (or "mọi hoá đơn"). */
    public static String voucherDescription(Voucher voucher) {
        String min = voucher.getMinOrderAmount() > 0 ? "Đơn từ " + money(voucher.getMinOrderAmount()) : "Mọi hoá đơn";
        return voucher.getCode() + " · " + min;
    }

    /**
     * Status line: the reason when it cannot be used ("Đã dùng", "Đã hết hạn", "Chưa đủ đơn tối thiểu…"); else what it
     * takes off this bill and the uses left ("Giảm 30.000đ cho hoá đơn này · Còn 2 lượt"); null when nothing to say.
     */
    @Nullable
    public static String voucherStatus(Voucher voucher) {
        if (!voucher.isUsable()) return voucher.getReason() == null ? "Không dùng được" : voucher.getReason();
        StringBuilder text = new StringBuilder();
        if (voucher.getBillDiscount() > 0) text.append("Giảm ").append(money(voucher.getBillDiscount())).append(" cho hoá đơn này");
        if (voucher.getRemainingLabel() != null) {
            if (text.length() > 0) text.append(" · ");
            text.append(voucher.getRemainingLabel());
        }
        return text.length() == 0 ? null : text.toString();
    }
}
