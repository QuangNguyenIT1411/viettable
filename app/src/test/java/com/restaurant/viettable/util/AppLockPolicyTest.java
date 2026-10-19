package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class AppLockPolicyTest {
    private static final long FIVE_MIN = AppLockPolicy.DEFAULT_TIMEOUT_MS;

    @Test public void default_is_five_minutes_and_a_test_build_can_override() {
        assertEquals(5 * 60 * 1000L, AppLockPolicy.timeoutMs(0));
        assertEquals(30_000L, AppLockPolicy.timeoutMs(30_000L));
    }

    @Test public void cold_start_with_a_session_and_fingerprint_locks() {
        assertTrue(AppLockPolicy.shouldLock(true, true, false, true, -1, 0, false, FIVE_MIN));
        // Even from a payment screen: after a kill the lock comes first, the payment state is restored underneath.
        assertTrue(AppLockPolicy.shouldLock(true, true, false, true, -1, 0, true, FIVE_MIN));
    }

    @Test public void no_lock_without_fingerprint_or_session_or_when_already_locked() {
        assertFalse(AppLockPolicy.shouldLock(true, false, false, true, -1, 0, false, FIVE_MIN));
        assertFalse(AppLockPolicy.shouldLock(false, true, false, true, -1, 0, false, FIVE_MIN));
        assertFalse(AppLockPolicy.shouldLock(true, true, true, true, -1, 0, false, FIVE_MIN));
    }

    @Test public void background_longer_than_five_minutes_locks_shorter_does_not() {
        long left = 1_000_000L;
        assertFalse(AppLockPolicy.shouldLock(true, true, false, false, left, left + FIVE_MIN - 1, false, FIVE_MIN));
        assertFalse(AppLockPolicy.shouldLock(true, true, false, false, left, left + FIVE_MIN, false, FIVE_MIN));
        assertTrue(AppLockPolicy.shouldLock(true, true, false, false, left, left + FIVE_MIN + 1, false, FIVE_MIN));
    }

    @Test public void rotation_or_never_backgrounded_does_not_lock() {
        assertFalse(AppLockPolicy.shouldLock(true, true, false, false, -1, 10 * FIVE_MIN, false, FIVE_MIN));
    }

    @Test public void screens_the_app_opened_itself_and_payment_waiting_never_lock() {
        long left = 1_000_000L;
        assertFalse(AppLockPolicy.shouldLock(true, true, false, false, left, left + 3 * FIVE_MIN, true, FIVE_MIN));
    }

    @Test public void own_external_screen_only_right_after_launching_it() {
        assertTrue(AppLockPolicy.leftForOwnExternalScreen(5_000, 5_400));
        assertFalse(AppLockPolicy.leftForOwnExternalScreen(5_000, 5_000 + AppLockPolicy.EXTERNAL_LAUNCH_WINDOW_MS + 1));
        assertFalse(AppLockPolicy.leftForOwnExternalScreen(-1, 5_400));
    }
}
