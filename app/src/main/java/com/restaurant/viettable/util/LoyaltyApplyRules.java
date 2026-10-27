package com.restaurant.viettable.util;

import androidx.annotation.Nullable;

import com.restaurant.viettable.network.ApiException;

import java.util.Locale;

/**
 * What the guest is told when points are applied to the checkout quote (owner, 28/10: the app used to say "Đã chọn
 * dùng N điểm" before the server had answered, so a refused or empty answer left the bill unchanged without a word).
 * Only the server's own answer counts: a discount above zero is success, everything else is explained.
 */
public final class LoyaltyApplyRules {

    public enum Kind { APPLIED, ZERO, INSUFFICIENT_POINTS, QUOTE_LOCKED, QUOTE_EXPIRED, OTHER }

    public static final class Outcome {
        public final Kind kind;
        public final long discountVnd;
        public final String title;
        public final String message;

        private Outcome(Kind kind, long discountVnd, String title, String message) {
            this.kind = kind;
            this.discountVnd = discountVnd;
            this.title = title;
            this.message = message;
        }

        public boolean applied() { return kind == Kind.APPLIED; }
    }

    /** Shown on the invoice once the payment was requested: voucher and points are fixed from then on. */
    public static final String LOCKED_EXPLANATION = "Đã yêu cầu thanh toán nên không đổi được voucher hoặc điểm. "
            + "Muốn đổi, hãy huỷ yêu cầu thanh toán (màn chờ thanh toán → Quay lại hóa đơn → Hủy yêu cầu). "
            + "Nếu đã có giao dịch đang chờ, nhờ thu ngân huỷ giao dịch đó.";

    private LoyaltyApplyRules() {}

    /** A quote is only editable while the server says OPEN; an unknown status is treated as editable (nothing to say yet). */
    public static boolean isQuoteLocked(@Nullable String status) {
        return status != null && !status.trim().isEmpty() && !"OPEN".equalsIgnoreCase(status.trim());
    }

    /** The server accepted the request: success only when it really took money off. */
    public static Outcome ofServerQuote(long loyaltyDiscountVnd) {
        if (loyaltyDiscountVnd > 0) {
            return new Outcome(Kind.APPLIED, loyaltyDiscountVnd, null,
                    String.format(Locale.GERMANY, "Đã áp dụng −%,dđ", loyaltyDiscountVnd));
        }
        return new Outcome(Kind.ZERO, 0, "Chưa dùng được điểm",
                "Hóa đơn này chưa trừ được điểm: điểm chỉ đổi theo từng bước tròn và tối đa 10% hóa đơn. "
                        + "Hóa đơn chưa thay đổi.");
    }

    /** The server refused: say why, in words the guest can act on. */
    public static Outcome ofError(@Nullable ApiException error) {
        String code = error == null ? "" : error.getCode();
        if ("insufficient_points".equals(code)) {
            return new Outcome(Kind.INSUFFICIENT_POINTS, 0, "Không đủ điểm",
                    "Số điểm khả dụng trên máy chủ không đủ cho lượt đổi này. Hóa đơn chưa thay đổi. "
                            + "Hãy quay lại, kiểm tra số dư rồi chọn lại.");
        }
        if ("state_conflict".equals(code)) {
            return new Outcome(Kind.QUOTE_LOCKED, 0, "Không đổi được điểm", LOCKED_EXPLANATION);
        }
        if ("quote_expired".equals(code)) {
            return new Outcome(Kind.QUOTE_EXPIRED, 0, "Báo giá đã hết hạn",
                    "Báo giá của hóa đơn đã hết hạn nên chưa dùng được điểm. Hãy quay lại màn thanh toán để lấy báo giá mới.");
        }
        String text = error == null || error.getMessage() == null || error.getMessage().trim().isEmpty()
                ? "Không thể áp dụng điểm lúc này." : error.getMessage();
        return new Outcome(Kind.OTHER, 0, "Chưa dùng được điểm", text + " Hóa đơn chưa thay đổi.");
    }
}
