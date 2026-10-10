package com.restaurant.viettable.data;

import java.util.Locale;

/** Maps existing UI choices to the Orders service contract. */
public final class ServiceRequestType {
    private ServiceRequestType() { }

    public static String normalize(String value) {
        String text = value == null ? "" : value.trim();
        String code = text.toUpperCase(Locale.ROOT);
        if (code.equals("CALL_STAFF") || code.equals("WATER") || code.equals("UTENSILS") || code.equals("OTHER")) return code;
        String lower = text.toLowerCase(Locale.ROOT);
        if (lower.contains("nước lọc") || lower.contains("trà đá")) return "WATER";
        if (lower.contains("bát đũa") || lower.contains("khăn")) return "UTENSILS";
        if (lower.isEmpty() || lower.equals("hỗ trợ chung")) return "CALL_STAFF";
        return "OTHER";
    }
}
