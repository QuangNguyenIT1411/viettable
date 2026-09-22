package com.restaurant.viettable.ui.auth;

import androidx.annotation.Nullable;

final class GoogleSignInConfig {
    private GoogleSignInConfig() {}

    static boolean isConfigured(@Nullable String clientId) {
        if (clientId == null) return false;
        String value = clientId.trim();
        return !value.isEmpty()
                && !value.contains("YOUR_")
                && value.endsWith(".apps.googleusercontent.com");
    }
}
