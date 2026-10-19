package com.restaurant.viettable.ui.auth;

import android.os.Bundle;
import android.security.keystore.KeyPermanentlyInvalidatedException;
import android.text.TextUtils;
import android.util.Log;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.restaurant.viettable.BuildConfig;
import com.restaurant.viettable.databinding.FragmentLoginBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.network.BiometricTokenStore;
import com.restaurant.viettable.util.BiometricLoginPolicy;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.branch.BranchListFragment;

import javax.crypto.Cipher;

public class LoginFragment extends BaseFragment {
    private static final String GOOGLE_LOG_TAG = "GoogleSignIn";

    private FragmentLoginBinding binding;
    private final GoogleSignInCoordinator googleSignIn = new GoogleSignInCoordinator();
    private boolean authenticating;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentLoginBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnGoRegister.setOnClickListener(v -> navigateTo(new RegisterFragment()));
        binding.btnForgotPassword.setOnClickListener(v -> navigateTo(new ForgotPasswordFragment()));
        binding.btnGoogleSignIn.setOnClickListener(v -> startGoogleSignIn());
        binding.btnBiometricLogin.setOnClickListener(v -> startFingerprintLogin());
        refreshFingerprintButton();
        binding.etPassword.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == android.view.inputmethod.EditorInfo.IME_ACTION_DONE) {
                binding.btnSubmitLogin.performClick();
                return true;
            }
            return false;
        });

        binding.btnSubmitLogin.setOnClickListener(v -> {
            String email = binding.etEmail.getText() != null ? binding.etEmail.getText().toString().trim() : "";
            String pass = binding.etPassword.getText() != null ? binding.etPassword.getText().toString().trim() : "";

            binding.tilEmail.setError(null);
            binding.tilPassword.setError(null);
            binding.tvError.setVisibility(View.GONE);
            if (TextUtils.isEmpty(email)) {
                binding.tilEmail.setError("Vui lòng nhập email hoặc số điện thoại.");
                binding.etEmail.requestFocus();
                return;
            }
            if (TextUtils.isEmpty(pass)) {
                binding.tilPassword.setError("Vui lòng nhập mật khẩu.");
                binding.etPassword.requestFocus();
                return;
            }

            setAuthenticating(true, com.restaurant.viettable.R.string.auth_continue_with_google);
            MobileRepository.getInstance().login(email, pass, new MobileRepository.Callback<User>() {
                @Override public void onSuccess(User value) {
                    if (binding == null) return;
                    setAuthenticating(false, com.restaurant.viettable.R.string.auth_continue_with_google);
                    afterInteractiveLogin(value, "Đăng nhập thành công!");
                }
                @Override public void onError(ApiException error) {
                    if (binding == null) return;
                    setAuthenticating(false, com.restaurant.viettable.R.string.auth_continue_with_google);
                    binding.tvError.setVisibility(View.VISIBLE);
                    binding.tvError.setText(apiError(error));
                }
            });
        });
    }

    private void startGoogleSignIn() {
        if (authenticating || googleSignIn.isRequestInFlight()) return;
        if (!GoogleSignInConfig.isConfigured(BuildConfig.GOOGLE_WEB_CLIENT_ID)) {
            showError(getString(com.restaurant.viettable.R.string.auth_google_not_configured));
            return;
        }

        setAuthenticating(true, com.restaurant.viettable.R.string.auth_google_launching);
        googleSignIn.start(requireActivity(), BuildConfig.GOOGLE_WEB_CLIENT_ID, new GoogleSignInCoordinator.Callback() {
            @Override public void onIdToken(@NonNull String idToken) {
                if (binding == null) return;
                if (BuildConfig.DEBUG) Log.d(GOOGLE_LOG_TAG, "Google credential callback received; dispatching Gateway auth request");
                binding.btnGoogleSignIn.setText(com.restaurant.viettable.R.string.auth_google_authenticating);
                MobileRepository.getInstance().loginWithGoogle(idToken, new MobileRepository.Callback<User>() {
                    @Override public void onSuccess(User value) {
                        if (binding == null) return;
                        if (BuildConfig.DEBUG) Log.d(GOOGLE_LOG_TAG, "Gateway auth succeeded; VietTable session stored; navigating to branch selection");
                        binding.btnGoogleSignIn.setText(com.restaurant.viettable.R.string.auth_google_success_state);
                        afterInteractiveLogin(value, getString(com.restaurant.viettable.R.string.auth_google_success));
                    }

                    @Override public void onError(ApiException error) {
                        if (binding == null) return;
                        if (BuildConfig.DEBUG) Log.w(GOOGLE_LOG_TAG, "Gateway auth failed; status=" + error.getStatusCode()
                                + "; code=" + error.getCode());
                        setAuthenticating(false, com.restaurant.viettable.R.string.auth_continue_with_google);
                        showError(GoogleSignInErrorMapper.messageFor(error));
                    }
                });
            }

            @Override public void onCancelled() {
                if (binding == null) return;
                setAuthenticating(false, com.restaurant.viettable.R.string.auth_continue_with_google);
            }

            @Override public void onError(@NonNull String message) {
                if (binding == null) return;
                setAuthenticating(false, com.restaurant.viettable.R.string.auth_continue_with_google);
                showError(message);
            }
        });
    }

    @Override
    public void onResume() {
        super.onResume();
        refreshFingerprintButton(); // the guest may have added or removed fingerprints in system settings
    }

    private void refreshFingerprintButton() {
        if (binding == null) return;
        BiometricTokenStore store = BiometricTokenStore.getInstance();
        boolean show = store.sealedOwner() != null && BiometricGate.deviceReady(getContext());
        binding.btnBiometricLogin.setVisibility(show ? View.VISIBLE : View.GONE);
        if (show) binding.btnBiometricLogin.setText(BiometricLoginPolicy.loginButtonText(store.sealedLabel()));
    }

    private void startFingerprintLogin() {
        if (authenticating) return;
        BiometricTokenStore store = BiometricTokenStore.getInstance();
        String owner = store.sealedOwner();
        if (owner == null) { refreshFingerprintButton(); return; }
        Cipher cipher;
        try {
            cipher = store.decryptCipher(owner);
        } catch (KeyPermanentlyInvalidatedException changed) {
            dropFingerprint(owner, BiometricLoginPolicy.KEY_CHANGED_MESSAGE);
            return;
        } catch (Exception unusable) {
            dropFingerprint(owner, BiometricLoginPolicy.UNAVAILABLE_MESSAGE);
            return;
        }
        binding.tvError.setVisibility(View.GONE);
        BiometricGate.unlock(this, cipher, new BiometricGate.Callback() {
            @Override public void onSuccess(@Nullable Cipher unlocked) {
                if (binding == null) return;
                String refreshToken;
                try {
                    refreshToken = store.open(unlocked);
                } catch (Exception unusable) {
                    dropFingerprint(owner, unusable instanceof KeyPermanentlyInvalidatedException
                            ? BiometricLoginPolicy.KEY_CHANGED_MESSAGE : BiometricLoginPolicy.UNAVAILABLE_MESSAGE);
                    return;
                }
                setAuthenticating(true, com.restaurant.viettable.R.string.auth_continue_with_google);
                MobileRepository.getInstance().loginWithSealedToken(refreshToken, new MobileRepository.Callback<User>() {
                    @Override public void onSuccess(User value) {
                        if (binding == null) return;
                        setAuthenticating(false, com.restaurant.viettable.R.string.auth_continue_with_google);
                        showToast("Đăng nhập thành công!");
                        navigateTo(new BranchListFragment());
                    }

                    @Override public void onError(ApiException error) {
                        BiometricLoginPolicy.RefreshFailure failure =
                                BiometricLoginPolicy.classifyRefreshFailure(error.getStatusCode(), error.getCode());
                        // Expired/revoked tokens are dropped even if the screen is gone; anything else is kept for a retry.
                        if (failure == BiometricLoginPolicy.RefreshFailure.REAUTH_REQUIRED) store.disable(owner);
                        if (binding == null) return;
                        setAuthenticating(false, com.restaurant.viettable.R.string.auth_continue_with_google);
                        refreshFingerprintButton();
                        showError(failure == BiometricLoginPolicy.RefreshFailure.REAUTH_REQUIRED
                                ? BiometricLoginPolicy.SESSION_EXPIRED_MESSAGE
                                : failure == BiometricLoginPolicy.RefreshFailure.NETWORK
                                ? BiometricLoginPolicy.NETWORK_MESSAGE : apiError(error));
                    }
                });
            }

            @Override public void onError(BiometricLoginPolicy.PromptError kind, @NonNull String message) {
                // Cancel, "Dùng mật khẩu" or too many wrong scans: stay on the normal login screen, data kept.
                if (binding == null || kind == BiometricLoginPolicy.PromptError.CANCELLED) return;
                showError(message);
            }
        });
    }

    /** Fingerprints changed or the key is unusable: turn the feature off and clean up, never crash. */
    private void dropFingerprint(String owner, String message) {
        BiometricTokenStore.getInstance().disable(owner);
        refreshFingerprintButton();
        showError(message);
    }

    /** Password or Google login succeeded: never reuse a sealed token, then offer fingerprint login once. */
    private void afterInteractiveLogin(User user, String message) {
        if (binding == null) return;
        BiometricTokenStore store = BiometricTokenStore.getInstance();
        store.clearSealed();
        showToast(message);
        String accountId = user == null ? null : user.getId();
        if (accountId == null || !BiometricLoginPolicy.shouldOffer(BiometricGate.deviceReady(getContext()),
                store.isEnabled(accountId), store.wasAsked(accountId))) {
            navigateTo(new BranchListFragment());
            return;
        }
        store.markAsked(accountId);
        new MaterialAlertDialogBuilder(requireContext())
                .setTitle("Bật đăng nhập bằng vân tay?")
                .setMessage("Lần sau bạn có thể đăng nhập nhanh bằng vân tay trên máy này. "
                        + "VietTable không lưu mật khẩu của bạn. Bạn có thể bật hoặc tắt trong Hồ sơ.")
                .setCancelable(false)
                .setPositiveButton("Bật", (dialog, which) -> enableFingerprintThenContinue(accountId))
                .setNegativeButton("Để sau", (dialog, which) -> navigateTo(new BranchListFragment()))
                .show();
    }

    private void enableFingerprintThenContinue(String accountId) {
        BiometricGate.confirm(this, new BiometricGate.Callback() {
            @Override public void onSuccess(@Nullable Cipher ignored) {
                try {
                    BiometricTokenStore.getInstance().enable(accountId);
                    showToast("Đã bật đăng nhập bằng vân tay.");
                } catch (Exception failure) {
                    BiometricTokenStore.getInstance().disable(accountId);
                    showToast(BiometricLoginPolicy.UNAVAILABLE_MESSAGE);
                }
                if (binding != null) navigateTo(new BranchListFragment());
            }

            @Override public void onError(BiometricLoginPolicy.PromptError kind, @NonNull String message) {
                showToast("Chưa bật đăng nhập bằng vân tay. Bạn có thể bật trong Hồ sơ.");
                if (binding != null) navigateTo(new BranchListFragment());
            }
        });
    }

    private void setAuthenticating(boolean value, int googleButtonText) {
        authenticating = value;
        if (binding == null) return;
        binding.tvError.setVisibility(View.GONE);
        binding.btnSubmitLogin.setEnabled(!value);
        binding.btnGoogleSignIn.setEnabled(!value);
        binding.btnGoRegister.setEnabled(!value);
        binding.btnForgotPassword.setEnabled(!value);
        binding.btnBiometricLogin.setEnabled(!value);
        binding.btnGoogleSignIn.setText(googleButtonText);
        binding.btnSubmitLogin.setText(value
                ? com.restaurant.viettable.R.string.auth_logging_in
                : com.restaurant.viettable.R.string.auth_login);
    }

    private void showError(String message) {
        if (binding == null) return;
        binding.tvError.setVisibility(View.VISIBLE);
        binding.tvError.setText(message);
    }

    @Override
    public void onDestroyView() {
        googleSignIn.cancel();
        authenticating = false;
        super.onDestroyView();
        binding = null;
    }
}
