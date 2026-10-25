package com.restaurant.viettable.util;

/**
 * Vietnamese phone numbers, same rule as the server (ReservationRules.NormalizePhone): 10 digits starting with 0;
 * "+84 343 000 273", "0343-000-273" and "84343000273" are accepted; letters are not.
 */
public final class PhoneRules {
    private PhoneRules() { }

    /** The 10-digit form, or null when empty or not a Vietnamese number. */
    public static String normalize(String value) {
        if (value == null) return null;
        String trimmed = value.trim();
        if (trimmed.isEmpty()) return null;
        StringBuilder digits = new StringBuilder();
        for (int i = 0; i < trimmed.length(); i++) {
            char c = trimmed.charAt(i);
            if (c >= '0' && c <= '9') digits.append(c);
            else if (!(c == ' ' || c == '.' || c == '-' || c == '(' || c == ')' || (c == '+' && i == 0))) return null;
        }
        String number = digits.toString();
        if (number.startsWith("84") && number.length() == 11) number = "0" + number.substring(2);
        return number.length() == 10 && number.charAt(0) == '0' ? number : null;
    }

    public static final String INVALID_MESSAGE = "Số điện thoại không hợp lệ. Nhập 10 số bắt đầu bằng 0 (ví dụ 0343000273) hoặc +84.";
}
