package com.restaurant.viettable.ui.auth;

import org.junit.Test;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class GoogleSignInConfigTest {
    @Test
    public void acceptsGoogleWebClientId() {
        assertTrue(GoogleSignInConfig.isConfigured(
                "123456-example.apps.googleusercontent.com"));
    }

    @Test
    public void rejectsMissingAndPlaceholderConfiguration() {
        assertFalse(GoogleSignInConfig.isConfigured(null));
        assertFalse(GoogleSignInConfig.isConfigured(""));
        assertFalse(GoogleSignInConfig.isConfigured("YOUR_GOOGLE_WEB_CLIENT_ID"));
        assertFalse(GoogleSignInConfig.isConfigured("not-a-google-client-id"));
    }

    @Test
    public void singleFlightGuardRejectsDuplicateUntilFinished() {
        GoogleSignInRequestGuard guard = new GoogleSignInRequestGuard();
        assertTrue(guard.tryBegin());
        assertTrue(guard.isInFlight());
        assertFalse(guard.tryBegin());
        guard.finish();
        assertFalse(guard.isInFlight());
        assertTrue(guard.tryBegin());
    }
}
