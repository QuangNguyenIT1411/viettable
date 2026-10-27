package com.restaurant.viettable.util;

import com.restaurant.viettable.model.DepositPolicy;

import java.time.Duration;
import java.time.Instant;
import java.util.Locale;

/**
 * Reservation deposit texts and numbers for the booking screens (docs/DEPOSIT_PLAN.md). The amounts come from the
 * server's policy; the server decides every state, the app only explains it.
 */
public final class DepositRules {
    private DepositRules() { }

    /** 50.000đ × guests from the threshold on; 0 when the feature is off or the party is smaller. */
    public static long requiredAmount(DepositPolicy policy, int guests) {
        if (policy == null || !policy.isEnabled() || policy.getMinGuests() <= 0 || policy.getPerGuestVnd() <= 0) return 0;
        return guests >= policy.getMinGuests() ? policy.getPerGuestVnd() * guests : 0;
    }

    /** Guest-count step note; null when no deposit is asked. */
    public static String notice(DepositPolicy policy, int guests) {
        long amount = requiredAmount(policy, guests);
        if (amount <= 0) return null;
        return "Từ " + policy.getMinGuests() + " khách cần đặt cọc " + money(policy.getPerGuestVnd()) + "/khách: "
                + money(amount) + ", chuyển khoản VietQR trong " + policy.getPaymentMinutes() + " phút để giữ bàn. "
                + "Tiền cọc được trừ vào hóa đơn; hủy trước giờ đến từ " + policy.getRefundCutoffHours() + " giờ được hoàn cọc.";
    }

    public static String policyNote(DepositPolicy policy) {
        int hours = policy == null || policy.getRefundCutoffHours() <= 0 ? 2 : policy.getRefundCutoffHours();
        return "Tiền cọc được trừ vào hóa đơn khi dùng bữa. Hủy trước giờ đến từ " + hours
                + " giờ được hoàn cọc (nhà hàng hoàn thủ công); hủy sát giờ hoặc không đến sẽ mất cọc.";
    }

    /** "14:05" for the time left; "00:00" once over. */
    public static String countdown(long millisLeft) {
        long seconds = Math.max(0, (millisLeft + 999) / 1000);
        return String.format(Locale.ROOT, "%02d:%02d", seconds / 60, seconds % 60);
    }

    public static boolean isWaiting(String status) { return "PENDING".equals(status); }

    public static boolean isPaid(String status) { return "PAID".equals(status) || "APPLIED".equals(status); }

    /** The booking is over without a deposit (time ran out or cancelled before paying). */
    public static boolean isClosedUnpaid(String status) { return "EXPIRED".equals(status) || "CANCELLED".equals(status); }

    /** One line for the booking screens; null when the booking has no deposit. */
    public static String statusLine(String status, long amount) {
        if (status == null || amount <= 0) return null;
        String value = money(amount);
        switch (status) {
            case "PENDING": return "Tiền cọc " + value + ": chờ chuyển khoản";
            case "PAID": return "Tiền cọc " + value + ": đã nhận, trừ vào hóa đơn khi dùng bữa";
            case "APPLIED": return "Tiền cọc " + value + ": đã trừ vào hóa đơn";
            case "REFUND_REQUIRED": return "Tiền cọc " + value + ": nhà hàng sẽ hoàn lại cho bạn";
            case "REFUNDED": return "Tiền cọc " + value + ": đã hoàn lại";
            case "FORFEITED": return "Tiền cọc " + value + ": không được hoàn";
            case "EXPIRED": return "Tiền cọc " + value + ": quá hạn, đặt bàn đã hủy";
            case "CANCELLED": return "Tiền cọc " + value + ": chưa chuyển, đặt bàn đã hủy";
            default: return null;
        }
    }

    /**
     * What a cancel means for the money, before the guest confirms it. Paid: refunded when at least cutoffHours before
     * the reserved time, kept otherwise. Unpaid: nothing to lose.
     */
    public static String cancelNotice(String status, long amount, Instant reservedAt, Instant now, int cutoffHours) {
        int hours = cutoffHours <= 0 ? 2 : cutoffHours;
        if (amount <= 0 || status == null || "NOT_REQUIRED".equals(status))
            return "Lịch đặt bàn này không có tiền cọc. Bạn không bị tính phí.";
        if ("PENDING".equals(status)) return "Bạn chưa chuyển tiền cọc nên hủy không mất phí.";
        if (!"PAID".equals(status)) return statusLine(status, amount);
        boolean early = reservedAt != null && !Duration.between(now, reservedAt).minusHours(hours).isNegative();
        return early
                ? "Bạn hủy trước giờ đến từ " + hours + " giờ: nhà hàng sẽ hoàn " + money(amount)
                    + " tiền cọc (hoàn thủ công, nhân viên sẽ liên hệ)."
                : "Hủy dưới " + hours + " giờ trước giờ đến: tiền cọc " + money(amount) + " sẽ không được hoàn.";
    }

    public static String money(long value) {
        return String.format(Locale.GERMANY, "%,dđ", value);
    }
}
