package com.restaurant.viettable.ui.auth;

import android.content.Context;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.biometric.BiometricManager;
import androidx.biometric.BiometricPrompt;
import androidx.core.content.ContextCompat;
import androidx.fragment.app.Fragment;

import com.restaurant.viettable.util.BiometricLoginPolicy;

import javax.crypto.Cipher;

/** Thin wrapper over the system fingerprint dialog. Strong biometrics only, so the Keystore key can be used. */
public final class BiometricGate {
    private static final int STRONG = BiometricManager.Authenticators.BIOMETRIC_STRONG;

    public interface Callback {
        /** cipher is null for a plain confirmation. */
        void onSuccess(@Nullable Cipher cipher);
        void onError(BiometricLoginPolicy.PromptError kind, @NonNull String message);
    }

    private BiometricGate() { }

    /** False on phones without a fingerprint sensor or with no fingerprint enrolled: the option is hidden. */
    public static boolean deviceReady(@Nullable Context context) {
        if (context == null) return false;
        try { return BiometricManager.from(context).canAuthenticate(STRONG) == BiometricManager.BIOMETRIC_SUCCESS; }
        catch (RuntimeException ignored) { return false; }
    }

    /** Asks for a fingerprint before turning the feature on. */
    public static void confirm(@NonNull Fragment fragment, @NonNull Callback callback) {
        show(fragment, "Bật đăng nhập bằng vân tay", "Quét vân tay để xác nhận", "Hủy", null, callback);
    }

    /** Unlocks the Keystore cipher that decrypts the sealed refresh token. */
    public static void unlock(@NonNull Fragment fragment, @NonNull Cipher cipher, @NonNull Callback callback) {
        show(fragment, "Đăng nhập bằng vân tay", "Quét vân tay hoặc khuôn mặt để vào VietTable", "Dùng mật khẩu", cipher, callback);
    }

    private static void show(Fragment fragment, String title, String subtitle, String negative,
                             @Nullable Cipher cipher, Callback callback) {
        BiometricPrompt prompt = new BiometricPrompt(fragment, ContextCompat.getMainExecutor(fragment.requireContext()),
                new BiometricPrompt.AuthenticationCallback() {
                    @Override public void onAuthenticationSucceeded(@NonNull BiometricPrompt.AuthenticationResult result) {
                        BiometricPrompt.CryptoObject crypto = result.getCryptoObject();
                        if (cipher != null && (crypto == null || crypto.getCipher() == null)) {
                            callback.onError(BiometricLoginPolicy.PromptError.OTHER, BiometricLoginPolicy.UNAVAILABLE_MESSAGE);
                            return;
                        }
                        callback.onSuccess(crypto == null ? null : crypto.getCipher());
                    }

                    @Override public void onAuthenticationError(int errorCode, @NonNull CharSequence errString) {
                        BiometricLoginPolicy.PromptError kind = BiometricLoginPolicy.classifyPromptError(errorCode);
                        callback.onError(kind, kind == BiometricLoginPolicy.PromptError.LOCKED_OUT
                                ? BiometricLoginPolicy.LOCKED_OUT_MESSAGE : errString.toString());
                    }
                    // A single wrong scan is shown inside the system dialog; nothing to do here.
                });
        BiometricPrompt.PromptInfo info = new BiometricPrompt.PromptInfo.Builder()
                .setTitle(title)
                .setSubtitle(subtitle)
                .setNegativeButtonText(negative)
                .setAllowedAuthenticators(STRONG)
                .setConfirmationRequired(false)
                .build();
        if (cipher == null) prompt.authenticate(info);
        else prompt.authenticate(info, new BiometricPrompt.CryptoObject(cipher));
    }
}
