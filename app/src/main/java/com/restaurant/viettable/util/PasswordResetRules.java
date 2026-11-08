package com.restaurant.viettable.util;

import androidx.annotation.Nullable;

import org.json.JSONObject;

/**
 * "Quên mật khẩu" (owner 08/11). The server answers 200 with a message and never the code on Production (the code is
 * e-mailed), so any 2xx answer means "sent": the app goes on to "Nhập mã + mật khẩu mới". Pure; unit tested.
 */
public final class PasswordResetRules {
    private PasswordResetRules() { }

    public static final String DEFAULT_SENT =
            "Nếu tài khoản tồn tại, mã xác nhận gồm 6 số đã được gửi tới email đã đăng ký. Mã có hiệu lực 15 phút.";

    /** A successful (2xx) answer, with or without a body, means the request was taken. */
    public static boolean accepted(@Nullable JSONObject response) { return true; }

    public static String message(@Nullable JSONObject response) {
        String m = response == null ? "" : SafeText.json(response, "message", "");
        return m == null || m.trim().isEmpty() ? DEFAULT_SENT : m.trim();
    }

    public static String digits(@Nullable String input) {
        return input == null ? "" : input.replaceAll("[^0-9]", "");
    }

    /** Null when the code may be sent; otherwise what to tell the customer. */
    @Nullable public static String whyBadCode(@Nullable String input) {
        String d = digits(input);
        if (d.isEmpty()) return "Vui lòng nhập mã xác nhận.";
        if (d.length() != 6) return "Mã xác nhận gồm 6 chữ số.";
        return null;
    }
}
