package com.restaurant.viettable.util;

/**
 * How often the waiting-for-payment screen asks the server. Realtime already announces the result, so polling is
 * only a safety net: fast at first, then slower, and it stops after 15 minutes (the guest can tap "Kiểm tra lại").
 * Before: every 3 s forever — hundreds of database reads per waiting guest (Supabase usage, 25/10).
 */
public final class PaymentPollPolicy {
    private PaymentPollPolicy() { }

    public static final long STOP_AFTER_MS = 15 * 60 * 1000L;

    /** Delay before the next check, or -1 to stop automatic checks. elapsedMs = time since the screen started waiting. */
    public static long nextDelayMs(long elapsedMs) {
        if (elapsedMs < 0) elapsedMs = 0;
        if (elapsedMs >= STOP_AFTER_MS) return -1;
        if (elapsedMs < 30_000) return 3_000;
        if (elapsedMs < 2 * 60_000) return 5_000;
        if (elapsedMs < 5 * 60_000) return 10_000;
        return 30_000;
    }

    /**
     * The QR is only for a payment still waiting for money. A finished payment (e.g. the guest went Back from
     * "Thanh toán thành công!") gets no QR request: the server answers 409 and the screen flashed an error (25/10).
     */
    public static boolean showsTransferQr(String status) {
        return !("SUCCEEDED".equals(status) || "FAILED".equals(status)
                || "CANCELLED".equals(status) || "EXPIRED".equals(status));
    }
}
