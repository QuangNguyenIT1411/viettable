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

/**
 * Thin wrapper over the system fingerprint dialog. Strong biometrics only, so the Keystore key can be used.
 * Create one per fragment view (in onViewCreated): a recreated fragment reconnects to a dialog that survived a
 * rotation, so its result is never lost and the dialog is never shown twice.
 */
public final class BiometricGate {
    private static final int STRONG = BiometricManager.Authenticators.BIOMETRIC_STRONG;

    public interface Callback {
        /** cipher is null for a plain confirmation. */
        void onSuccess(@Nullable Cipher cipher);
        void onError(BiometricLoginPolicy.PromptError kind, @NonNull String message);
    }

    private final Fragment fragment;
    private final BiometricPrompt prompt;

    public BiometricGate(@NonNull Fragment fragment, @NonNull Callback callback) {
        this.fragment = fragment;
        prompt = new BiometricPrompt(fragment, ContextCompat.getMainExecutor(fragment.requireContext()),
                new BiometricPrompt.AuthenticationCallback() {
                    @Override public void onAuthenticationSucceeded(@NonNull BiometricPrompt.AuthenticationResult result) {
                        BiometricPrompt.CryptoObject crypto = result.getCryptoObject();
                        callback.onSuccess(crypto == null ? null : crypto.getCipher());
                    }

                    @Override public void onAuthenticationError(int errorCode, @NonNull CharSequence errString) {
                        BiometricLoginPolicy.PromptError kind = BiometricLoginPolicy.classifyPromptError(errorCode);
                        callback.onError(kind, kind == BiometricLoginPolicy.PromptError.LOCKED_OUT
                                ? BiometricLoginPolicy.LOCKED_OUT_MESSAGE : errString.toString());
                    }
                    // A single wrong scan is shown inside the system dialog; nothing to do here.
                });
    }

    /** False on phones without a fingerprint sensor or with no fingerprint enrolled: the option is hidden. */
    public static boolean deviceReady(@Nullable Context context) {
        if (context == null) return false;
        try { return BiometricManager.from(context).canAuthenticate(STRONG) == BiometricManager.BIOMETRIC_SUCCESS; }
        catch (RuntimeException ignored) { return false; }
    }

    /** Asks for a fingerprint before turning the feature on. Returns false when the screen cannot show it now. */
    public boolean confirm() {
        return show("Bật đăng nhập bằng vân tay", "Quét vân tay để xác nhận", "Hủy", null);
    }

    /** Unlocks the Keystore cipher that decrypts the sealed refresh token. */
    public boolean unlock(@NonNull Cipher cipher) {
        return show("Đăng nhập bằng vân tay", "Quét vân tay để vào VietTable", "Dùng mật khẩu", cipher);
    }

    /** App lock: same key, so changed fingerprints cannot open it. */
    public boolean unlockApp(@NonNull Cipher cipher) {
        return show("Mở khoá VietTable", "Quét vân tay để tiếp tục", "Dùng mật khẩu", cipher);
    }

    private boolean show(String title, String subtitle, String negative, @Nullable Cipher cipher) {
        // Only a visible, resumed screen may open the dialog (never a fragment being rebuilt off-screen).
        if (!fragment.isResumed() || fragment.isRemoving() || fragment.getChildFragmentManager().isStateSaved()) return false;
        BiometricPrompt.PromptInfo info = new BiometricPrompt.PromptInfo.Builder()
                .setTitle(title)
                .setSubtitle(subtitle)
                .setNegativeButtonText(negative)
                .setAllowedAuthenticators(STRONG)
                .setConfirmationRequired(false)
                .build();
        if (cipher == null) prompt.authenticate(info);
        else prompt.authenticate(info, new BiometricPrompt.CryptoObject(cipher));
        return true;
    }
}
