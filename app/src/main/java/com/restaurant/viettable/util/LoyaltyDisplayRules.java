package com.restaurant.viettable.util;

import androidx.annotation.Nullable;

import com.restaurant.viettable.model.LoyaltyInfo;
import com.restaurant.viettable.model.NotificationItem;

import java.util.List;
import java.util.Locale;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/** Points shown to the customer come only from the server; nothing here makes a number up (owner, 26/10). */
public final class LoyaltyDisplayRules {

    /** Inbox event the Customers service sends once a paid invoice's points are settled (refId = paymentId). */
    public static final String POINTS_EARNED_EVENT = "loyalty.points-earned.v1";
    // Server text: "Bạn được cộng 1.234 điểm thưởng. Tổng điểm hiện có: ..." (MobileEngagementController).
    private static final Pattern EARNED = Pattern.compile("cộng\\s+([0-9][0-9.]*)\\s+điểm");

    private LoyaltyDisplayRules() {}

    /** Points this payment earned, read from its inbox message; null while it has not arrived (the line stays hidden). */
    @Nullable
    public static Integer earnedForPayment(List<NotificationItem> inbox, long paymentId) {
        if (paymentId <= 0 || inbox == null) return null;
        for (NotificationItem item : inbox) {
            if (!POINTS_EARNED_EVENT.equals(item.getEventName()) || item.getRefId() != paymentId) continue;
            Matcher m = EARNED.matcher(item.getMessage() == null ? "" : item.getMessage());
            if (!m.find()) return null;
            try { return Integer.parseInt(m.group(1).replace(".", "")); }
            catch (NumberFormatException e) { return null; }
        }
        return null;
    }

    /** Server's points conversion: dong per point, the redeem step (1.000 points) and the bill share cap (10%). */
    public static final class Conversion {
        public final long valueVnd;
        public final long step;
        public final int maxPercent;

        private Conversion(long valueVnd, long step, int maxPercent) {
            this.valueVnd = valueVnd;
            this.step = step;
            this.maxPercent = maxPercent;
        }

        /** Null unless the server sent all three numbers. */
        @Nullable
        public static Conversion of(long valueVnd, long step, int maxPercent) {
            return valueVnd > 0 && step > 0 && maxPercent > 0 ? new Conversion(valueVnd, step, maxPercent) : null;
        }

        /** The most points this balance can redeem: whole steps only (1.500 points -> 1.000). */
        public long usable(long balance) {
            return balance <= 0 ? 0 : balance / step * step;
        }

        /**
         * The most points for this balance and bill: whole steps, at most {@link #maxPercent}% of the bill
         * (mirrors LoyaltyRules.RedeemablePoints). A 245.000đ bill allows 24.500đ = 2.450 points -> 2.000.
         */
        public long usable(long balance, long payable) {
            if (payable <= 0) return 0;
            long byBill = payable * maxPercent / 100 / valueVnd;
            return usable(Math.min(balance, byBill));
        }

        public long discount(long points) {
            return Math.max(0, points) * valueVnd;
        }
    }

    public static String points(long value) {
        return String.format(Locale.GERMANY, "%,d", value);
    }

    /** "1.200 / 5.000" toward the next tier; null at the top tier (no bar then). */
    @Nullable
    public static String progressPoints(LoyaltyInfo info) {
        Integer next = info.getNextTierAtPoints();
        if (next == null || info.getNextTierCode() == null) return null;
        return points(info.getAvailablePoints()) + " / " + points(next);
    }
}
