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
        assertTrue(BiometricLoginPolicy.belongsToOtherAccount("12", null));
        assertFalse(BiometricLoginPolicy.belongsToOtherAccount("", "34"));
    }

    @Test public void label_masks_email_then_phone_then_name() {
        assertEquals("q***@gmail.com", BiometricLoginPolicy.maskedLabel("quang@gmail.com", "0901234567", "Quang"));
        assertEquals("q***@gmail.com", BiometricLoginPolicy.maskedLabel("q@gmail.com", null, null));
        assertEquals("09******67", BiometricLoginPolicy.maskedLabel("", "0901234567", "Quang"));
        assertEquals("Quang", BiometricLoginPolicy.maskedLabel(null, "123", "Quang"));
        assertEquals("09******67", BiometricLoginPolicy.maskedLabel("null", "0901234567", null));
    }

    @Test public void button_text_includes_masked_account() {
        assertEquals("Đăng nhập bằng vân tay (q***@gmail.com)", BiometricLoginPolicy.loginButtonText("q***@gmail.com"));
        assertEquals("Đăng nhập bằng vân tay", BiometricLoginPolicy.loginButtonText(""));
    }
}
