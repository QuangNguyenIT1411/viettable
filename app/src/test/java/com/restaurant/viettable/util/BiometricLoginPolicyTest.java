package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import androidx.biometric.BiometricPrompt;

import org.junit.Test;

public class BiometricLoginPolicyTest {
    @Test public void expired_or_revoked_token_drops_fingerprint_data() {
        assertEquals(BiometricLoginPolicy.RefreshFailure.REAUTH_REQUIRED, BiometricLoginPolicy.classifyRefreshFailure(401, "refresh_token_reused"));
        assertEquals(BiometricLoginPolicy.RefreshFailure.REAUTH_REQUIRED, BiometricLoginPolicy.classifyRefreshFailure(400, "invalid_refresh_token"));
        assertEquals(BiometricLoginPolicy.RefreshFailure.REAUTH_REQUIRED, BiometricLoginPolicy.classifyRefreshFailure(403, "account_locked"));
    }

    @Test public void no_network_keeps_data_for_retry() {
        assertEquals(BiometricLoginPolicy.RefreshFailure.NETWORK, BiometricLoginPolicy.classifyRefreshFailure(0, "network_error"));
    }

    @Test public void server_trouble_keeps_data_for_retry() {
        assertEquals(BiometricLoginPolicy.RefreshFailure.RETRY_LATER, BiometricLoginPolicy.classifyRefreshFailure(500, "request_failed"));
        assertEquals(BiometricLoginPolicy.RefreshFailure.RETRY_LATER, BiometricLoginPolicy.classifyRefreshFailure(429, "rate_limited"));
        assertEquals(BiometricLoginPolicy.RefreshFailure.RETRY_LATER, BiometricLoginPolicy.classifyRefreshFailure(0, "unexpected_response"));
    }

    @Test public void cancel_and_password_button_return_silently() {
        assertEquals(BiometricLoginPolicy.PromptError.CANCELLED, BiometricLoginPolicy.classifyPromptError(BiometricPrompt.ERROR_USER_CANCELED));
        assertEquals(BiometricLoginPolicy.PromptError.CANCELLED, BiometricLoginPolicy.classifyPromptError(BiometricPrompt.ERROR_NEGATIVE_BUTTON));
        assertEquals(BiometricLoginPolicy.PromptError.CANCELLED, BiometricLoginPolicy.classifyPromptError(BiometricPrompt.ERROR_CANCELED));
    }

    @Test public void too_many_wrong_scans_is_a_lockout() {
        assertEquals(BiometricLoginPolicy.PromptError.LOCKED_OUT, BiometricLoginPolicy.classifyPromptError(BiometricPrompt.ERROR_LOCKOUT));
        assertEquals(BiometricLoginPolicy.PromptError.LOCKED_OUT, BiometricLoginPolicy.classifyPromptError(BiometricPrompt.ERROR_LOCKOUT_PERMANENT));
        assertEquals(BiometricLoginPolicy.PromptError.OTHER, BiometricLoginPolicy.classifyPromptError(BiometricPrompt.ERROR_HW_UNAVAILABLE));
    }

    @Test public void offer_once_and_only_on_a_ready_device() {
        assertTrue(BiometricLoginPolicy.shouldOffer(true, false, false));
        assertFalse(BiometricLoginPolicy.shouldOffer(false, false, false));
        assertFalse(BiometricLoginPolicy.shouldOffer(true, true, false));
        assertFalse(BiometricLoginPolicy.shouldOffer(true, false, true));
    }

    @Test public void only_another_account_wipes_fingerprint_data() {
        assertFalse(BiometricLoginPolicy.belongsToOtherAccount("12", "12"));
        assertTrue(BiometricLoginPolicy.belongsToOtherAccount("12", "34"));
        assertFalse(BiometricLoginPolicy.belongsToOtherAccount("", "34"));
    }

    // Bug 19/10 (lỗi 2): after a swipe-kill the profile is not loaded yet; an unknown/blank/"0" id must never be
    // treated as "another account", so nothing is wiped on doubt.
    @Test public void unknown_current_account_never_wipes_fingerprint_data() {
        assertFalse(BiometricLoginPolicy.belongsToOtherAccount("12", null));
        assertFalse(BiometricLoginPolicy.belongsToOtherAccount("12", ""));
        assertFalse(BiometricLoginPolicy.belongsToOtherAccount("12", "0"));
        assertFalse(BiometricLoginPolicy.belongsToOtherAccount("12", "null"));
        assertFalse(BiometricLoginPolicy.isKnownAccount("0"));
        assertTrue(BiometricLoginPolicy.isKnownAccount("26"));
    }

    // Bug 19/10 (lỗi 2): Hồ sơ and Đăng xuất asked the in-memory profile, which is empty right after a cold start, so
    // the fingerprint row vanished and logout revoked the session instead of keeping fingerprint login.
    @Test public void cold_start_uses_the_account_remembered_with_the_session() {
        assertEquals("26", BiometricLoginPolicy.resolveAccount(null, "26"));
        assertEquals("26", BiometricLoginPolicy.resolveAccount("0", "26"));
        assertEquals("25", BiometricLoginPolicy.resolveAccount("25", "26")); // a loaded profile wins
        assertEquals(null, BiometricLoginPolicy.resolveAccount(null, null));
    }

    @Test public void only_a_dead_key_wipes_fingerprint_data() {
        assertTrue(BiometricLoginPolicy.shouldWipeAfterKeyError(true));
        assertFalse(BiometricLoginPolicy.shouldWipeAfterKeyError(false));
    }

    @Test public void label_masks_email_then_phone_then_name() {
        assertEquals("q***@gmail.com", BiometricLoginPolicy.maskedLabel("quang@gmail.com", "0901234567", "Quang"));
        assertEquals("q***@gmail.com", BiometricLoginPolicy.maskedLabel("q@gmail.com", null, null));
        assertEquals("09******67", BiometricLoginPolicy.maskedLabel("", "0901234567", "Quang"));
        assertEquals("Quang", BiometricLoginPolicy.maskedLabel(null, "123", "Quang"));
        assertEquals("09******67", BiometricLoginPolicy.maskedLabel("null", "0901234567", null));
    }

    @Test public void greeting_shows_masked_account() {
        assertEquals("Xin chào, q***@gmail.com", BiometricLoginPolicy.greeting("q***@gmail.com"));
        assertEquals("Xin chào", BiometricLoginPolicy.greeting(null));
    }

    @Test public void remembered_password_login_uses_email_then_phone() {
        assertEquals("q@gmail.com", BiometricLoginPolicy.loginId("q@gmail.com", "0901234567"));
        assertEquals("0901234567", BiometricLoginPolicy.loginId("null", "0901234567"));
    }

    @Test public void login_screen_prompts_by_itself_only_once_and_never_inside_the_app() {
        assertTrue(BiometricLoginPolicy.shouldAutoPrompt(true, true, false, false));
        // Already scanned on this screen (also after rotation / back from background): no second dialog.
        assertFalse(BiometricLoginPolicy.shouldAutoPrompt(true, true, false, true));
        // Logged in already (e.g. the screen is rebuilt while the user is in the app): never.
        assertFalse(BiometricLoginPolicy.shouldAutoPrompt(true, true, true, false));
        assertFalse(BiometricLoginPolicy.shouldAutoPrompt(false, true, false, false));
        assertFalse(BiometricLoginPolicy.shouldAutoPrompt(true, false, false, false));
    }

    // Bug 19/10: after fingerprint login, picking a branch rebuilt the old Hồ sơ screen off-screen; Android restored
    // its switch to "on", which looked like a tap and opened a second fingerprint dialog.
    @Test public void restored_switch_state_is_not_a_user_tap() {
        assertFalse(BiometricLoginPolicy.isUserToggle(false, true, false)); // rebuilt off-screen
        assertFalse(BiometricLoginPolicy.isUserToggle(true, true, true));   // same as stored setting
        assertTrue(BiometricLoginPolicy.isUserToggle(true, true, false));
        assertTrue(BiometricLoginPolicy.isUserToggle(true, false, true));
    }
}
