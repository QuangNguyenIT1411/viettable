package com.restaurant.viettable.model;

import java.io.Serializable;

/** Deposit rules from GET /api/mobile/v1/reservations/deposit-policy (docs/DEPOSIT_PLAN.md); never hard-coded. */
public final class DepositPolicy implements Serializable {
    /** Until the server answers (or when it cannot): no deposit is shown or asked. */
    public static final DepositPolicy OFF = new DepositPolicy(false, 0, 0, 0, 0);

    private final boolean enabled;
    private final int minGuests;
    private final long perGuestVnd;
    private final int paymentMinutes;
    private final int refundCutoffHours;

    public DepositPolicy(boolean enabled, int minGuests, long perGuestVnd, int paymentMinutes, int refundCutoffHours) {
        this.enabled = enabled;
        this.minGuests = minGuests;
        this.perGuestVnd = perGuestVnd;
        this.paymentMinutes = paymentMinutes;
        this.refundCutoffHours = refundCutoffHours;
    }

    public boolean isEnabled() { return enabled; }
    public int getMinGuests() { return minGuests; }
    public long getPerGuestVnd() { return perGuestVnd; }
    public int getPaymentMinutes() { return paymentMinutes; }
    public int getRefundCutoffHours() { return refundCutoffHours; }
}
