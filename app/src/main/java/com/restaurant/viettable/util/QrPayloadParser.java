package com.restaurant.viettable.util;

import androidx.annotation.Nullable;

/** Parses the raw table QR value defined by the mobile API contract. */
public final class QrPayloadParser {
    private static final int MAX_QR_CODE_LENGTH = 100;

    private QrPayloadParser() {}

    @Nullable
    public static String parse(@Nullable String rawValue) {
        if (rawValue == null) return null;
        String value = rawValue.trim();
        if (value.isEmpty() || value.length() > MAX_QR_CODE_LENGTH) return null;
        for (int i = 0; i < value.length(); i++) {
            if (Character.isISOControl(value.charAt(i))) return null;
        }
        return value;
    }
}
