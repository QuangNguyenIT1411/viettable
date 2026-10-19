package com.restaurant.viettable.util;

import androidx.biometric.BiometricPrompt;

/** Fingerprint-login decisions kept free of Android so they can be unit tested. */
public final class BiometricLoginPolicy {
    private BiometricLoginPolicy() { }

    public static final String KEY_CHANGED_MESSAGE =
            "Vân tay trên máy đã thay đổi, vui lòng đăng nhập lại bằng mật khẩu.";
    public static final String SESSION_EXPIRED_MESSAGE =
            "Phiên đăng nhập bằng vân tay đã hết hạn, vui lòng đăng nhập lại bằng mật khẩu.";
    public static final String NETWORK_MESSAGE =
            "Không thể kết nối đến VietTable. Vui lòng kiểm tra mạng rồi thử lại đăng nhập bằng vân tay.";
    public static final String LOCKED_OUT_MESSAGE =
            "Quét sai quá nhiều lần. Vui lòng đăng nhập bằng mật khẩu.";
    public static final String UNAVAILABLE_MESSAGE =
            "Không dùng được đăng nhập bằng vân tay, vui lòng đăng nhập bằng mật khẩu.";

    /** What to do with the sealed token after /auth/refresh failed. */
    public enum RefreshFailure {
        /** Expired, revoked or reused: the token is useless, remove fingerprint data. */
        REAUTH_REQUIRED,
        /** No connection: keep everything so the guest can try again. */
        NETWORK,
        /** Server busy or other temporary error: keep everything. */
        RETRY_LATER
    }

    public static RefreshFailure classifyRefreshFailure(int status, String code) {
        if (status == 400 || status == 401 || status == 403) return RefreshFailure.REAUTH_REQUIRED;
        if (status == 0 && "network_error".equals(code)) return RefreshFailure.NETWORK;
        return RefreshFailure.RETRY_LATER;
    }

    /** How the login screen reacts when the system fingerprint dialog closes with an error. */
    public enum PromptError {
        /** Cancel / "Dùng mật khẩu" / dialog dismissed: back to the normal login screen, no message. */
        CANCELLED,
        /** Too many wrong scans: back to the login screen with a message, data kept. */
        LOCKED_OUT,
        /** Sensor or enrolment problem: show the system message, data kept. */
        OTHER
    }

    public static PromptError classifyPromptError(int errorCode) {
        switch (errorCode) {
            case BiometricPrompt.ERROR_USER_CANCELED:
            case BiometricPrompt.ERROR_NEGATIVE_BUTTON:
            case BiometricPrompt.ERROR_CANCELED:
                return PromptError.CANCELLED;
            case BiometricPrompt.ERROR_LOCKOUT:
            case BiometricPrompt.ERROR_LOCKOUT_PERMANENT:
                return PromptError.LOCKED_OUT;
            default:
                return PromptError.OTHER;
        }
    }

    /** Ask "Bật đăng nhập bằng vân tay?" once per account, only on a device with a strong fingerprint. */
    public static boolean shouldOffer(boolean deviceReady, boolean enabled, boolean alreadyAsked) {
        return deviceReady && !enabled && !alreadyAsked;
    }

    /** Fingerprint data of owner must be deleted when currentAccount logs in with a password or Google. */
    public static boolean belongsToOtherAccount(String owner, String currentAccount) {
        if (owner == null || owner.trim().isEmpty()) return false;
        return currentAccount == null || !owner.equals(currentAccount);
    }

    /** "q***@gmail.com"; phone "09******78" when there is no e-mail; otherwise the name. */
    public static String maskedLabel(String email, String phone, String name) {
        String mail = SafeText.clean(email).trim();
        int at = mail.indexOf('@');
        if (at > 0 && at < mail.length() - 1) return mail.charAt(0) + "***" + mail.substring(at);
        String digits = SafeText.clean(phone).trim();
        if (digits.length() >= 6) {
            StringBuilder masked = new StringBuilder(digits.substring(0, 2));
            for (int i = 2; i < digits.length() - 2; i++) masked.append('*');
            return masked.append(digits.substring(digits.length() - 2)).toString();
        }
        return SafeText.clean(name).trim();
    }

    public static String greeting(String label) {
        String clean = SafeText.clean(label).trim();
        return clean.isEmpty() ? "Xin chào" : "Xin chào, " + clean;
    }

    /** E-mail, else phone: what the password field of the remembered account logs in with. */
    public static String loginId(String email, String phone) {
        String mail = SafeText.clean(email).trim();
        return mail.isEmpty() ? SafeText.clean(phone).trim() : mail;
    }

    /**
     * The login screen opens the fingerprint dialog by itself exactly once per screen: only for a remembered
     * account, never once a session exists (already inside the app), never again after rotation or returning
     * from the background (alreadyPrompted is kept in the saved state).
     */
    public static boolean shouldAutoPrompt(boolean rememberedAccount, boolean deviceReady, boolean hasSession,
                                           boolean alreadyPrompted) {
        return rememberedAccount && deviceReady && !hasSession && !alreadyPrompted;
    }

    /**
     * A change of the Hồ sơ switch is the user's own tap only while the screen is resumed and the new value differs
     * from the stored setting. Android restoring a rebuilt screen's old switch state must not open a dialog.
     */
    public static boolean isUserToggle(boolean screenResumed, boolean checked, boolean enabledNow) {
        return screenResumed && checked != enabledNow;
    }
}
