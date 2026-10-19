package com.restaurant.viettable.ui.lock;

import android.os.SystemClock;

import com.restaurant.viettable.util.AppLockPolicy;

/** Process-wide memory for the app lock: when the app went to the background and why. */
public final class AppLock {
    private static final AppLock INSTANCE = new AppLock();

    private boolean processStarted;
    private long backgroundedAt = -1;
    private boolean exemptWhenLeft;
    private long expectedExternalAt = -1;

    private AppLock() { }

    public static AppLock get() { return INSTANCE; }

    /**
     * Call right before the app itself opens another app/screen (Google sign-in, payment page, share sheet,
     * maps, image picker, system settings), so coming back from it never shows the lock.
     */
    public void expectExternalReturn() { expectedExternalAt = SystemClock.elapsedRealtime(); }

    /** MainActivity.onStop (not for rotation). */
    public void onAppBackground(boolean topScreenExempt) {
        long now = SystemClock.elapsedRealtime();
        backgroundedAt = now;
        exemptWhenLeft = topScreenExempt || AppLockPolicy.leftForOwnExternalScreen(expectedExternalAt, now);
    }

    /** MainActivity.onStart: true when the lock must be shown now. */
    public boolean shouldLockOnForeground(boolean hasSession, boolean fingerprintOn, boolean alreadyLocked, long timeoutMs) {
        boolean firstStart = !processStarted;
        processStarted = true;
        boolean lock = AppLockPolicy.shouldLock(hasSession, fingerprintOn, alreadyLocked, firstStart, backgroundedAt,
                SystemClock.elapsedRealtime(), exemptWhenLeft, timeoutMs);
        backgroundedAt = -1;
        exemptWhenLeft = false;
        expectedExternalAt = -1;
        return lock;
    }
}
