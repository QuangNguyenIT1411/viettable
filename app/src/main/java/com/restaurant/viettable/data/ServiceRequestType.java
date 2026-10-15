package com.restaurant.viettable.data;

import java.util.Locale;

/** Maps existing UI choices to the Orders service contract. */
public final class ServiceRequestType {
    /** The customer asks the kitchen to hurry a dish; shown on the kitchen board. */
    public static final String KITCHEN_RUSH = "KITCHEN_RUSH";
    /** The customer asks to cancel a dish; the kitchen or cashier decides, nothing is cancelled automatically. */
    public static final String KITCHEN_CANCEL = "KITCHEN_CANCEL";

    private ServiceRequestType() { }

    public static String normalize(String value) {
        String text = value == null ? "" : value.trim();
        String code = text.toUpperCase(Locale.ROOT);
        if (code.equals("CALL_STAFF") || code.equals("WATER") || code.equals("UTENSILS") || code.equals("OTHER")
                || code.equals(KITCHEN_RUSH) || code.equals(KITCHEN_CANCEL)) return code;
        String lower = text.toLowerCase(Locale.ROOT);
        if (lower.contains("nước lọc") || lower.contains("trà đá")) return "WATER";
        if (lower.contains("bát đũa") || lower.contains("khăn")) return "UTENSILS";
        if (lower.isEmpty() || lower.equals("hỗ trợ chung")) return "CALL_STAFF";
        return "OTHER";
    }
}
