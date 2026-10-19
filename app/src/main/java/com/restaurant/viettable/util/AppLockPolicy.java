package com.restaurant.viettable.util;

/** When the fingerprint app lock appears. Free of Android so it can be unit tested. */
public final class AppLockPolicy {
    private AppLockPolicy() { }

    /** Time in the background after which the app locks. Change here; test builds may override (see build.gradle). */
    public static final long DEFAULT_TIMEOUT_MS = 5 * 60 * 1000L;

    /** An app switch within this window after the app itself opened another screen counts as that screen. */
    public static final long EXTERNAL_LAUNCH_WINDOW_MS = 3_000L;

    public static long timeoutMs(long overrideMs) {
        return overrideMs > 0 ? overrideMs : DEFAULT_TIMEOUT_MS;
    }

    /**
     * @param firstStartInProcess the app process just started (opened after a swipe-kill / force stop)
     * @param backgroundedAt      elapsed-realtime when the app went to the background, or -1 if it did not
     * @param exemptWhenLeft      the app had opened something itself (Google sign-in, payment page, settings) or the
     *                            user left from a payment-waiting / QR screen, e.g. to pay VietQR in a banking app
     */
    public static boolean shouldLock(boolean hasSession, boolean fingerprintOn, boolean alreadyLocked,
                                     boolean firstStartInProcess, long backgroundedAt, long now,
                                     boolean exemptWhenLeft, long timeoutMs) {
        if (!hasSession || !fingerprintOn || alreadyLocked) return false;
        if (firstStartInProcess) return true;
        if (backgroundedAt < 0 || exemptWhenLeft) return false;
        return now - backgroundedAt > timeoutMs;
    }

    /** Whether going to the background now was caused by something the app itself just opened. */
    public static boolean leftForOwnExternalScreen(long expectedExternalAt, long stoppedAt) {
        return expectedExternalAt >= 0 && stoppedAt >= expectedExternalAt
                && stoppedAt - expectedExternalAt <= EXTERNAL_LAUNCH_WINDOW_MS;
    }
}
