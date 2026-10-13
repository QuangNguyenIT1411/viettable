package com.restaurant.viettable.util;

import androidx.annotation.Nullable;

import java.net.URI;
import java.net.URLDecoder;
import java.nio.charset.StandardCharsets;
import java.util.Locale;

/**
 * Parses a table QR value. Accepts the raw code defined by the mobile API contract, or the
 * {@code <origin>/Menu/FromQr?code=<code>} link printed by Admin Web (any host: the backend
 * still validates the extracted code).
 */
public final class QrPayloadParser {
    private static final int MAX_QR_CODE_LENGTH = 100;
    private static final int MAX_RAW_LENGTH = 2048;
    private static final String TABLE_LINK_PATH = "/menu/fromqr";

    private QrPayloadParser() {}

    @Nullable
    public static String parse(@Nullable String rawValue) {
        if (rawValue == null) return null;
        String value = rawValue.trim();
        if (value.isEmpty() || value.length() > MAX_RAW_LENGTH || hasControlCharacter(value)) return null;
        String lower = value.toLowerCase(Locale.ROOT);
        if (lower.startsWith("http://") || lower.startsWith("https://")) {
            value = codeFromTableLink(value);
            if (value == null) return null;
        }
        if (value.isEmpty() || value.length() > MAX_QR_CODE_LENGTH || hasControlCharacter(value)) return null;
        return value;
    }

    @Nullable
    private static String codeFromTableLink(String link) {
        URI uri;
        try {
            uri = new URI(link);
        } catch (Exception e) {
            return null;
        }
        String path = uri.getPath();
        String query = uri.getRawQuery();
        if (path == null || query == null) return null;
        if (path.endsWith("/")) path = path.substring(0, path.length() - 1);
        if (!path.toLowerCase(Locale.ROOT).equals(TABLE_LINK_PATH)) return null;
        for (String pair : query.split("&")) {
            int separator = pair.indexOf('=');
            if (separator <= 0 || !pair.substring(0, separator).equalsIgnoreCase("code")) continue;
            try {
                return URLDecoder.decode(pair.substring(separator + 1), StandardCharsets.UTF_8.name()).trim();
            } catch (Exception e) {
                return null;
            }
        }
        return null;
    }

    private static boolean hasControlCharacter(String value) {
        for (int i = 0; i < value.length(); i++) {
            if (Character.isISOControl(value.charAt(i))) return true;
        }
        return false;
    }
}
