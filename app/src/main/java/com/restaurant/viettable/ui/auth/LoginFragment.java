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

/**
 * Normal login (e-mail/phone + password, Google), or — when this phone remembers an account with fingerprint
 * login on — a banking-style screen: "Xin chào, q***@gmail.com", the fingerprint dialog opens by itself once,
 * the password field logs in to that account, and "Đăng nhập tài khoản khác" forgets it.
 */
public class LoginFragment extends BaseFragment {
    private static final String GOOGLE_LOG_TAG = "GoogleSignIn";
    private static final String STATE_AUTO_PROMPTED = "fingerprintAutoPrompted";
    private static final String STATE_PENDING_ACTION = "fingerprintPendingAction";
    private static final String STATE_PENDING_ACCOUNT = "fingerprintPendingAccount";
    private static final String ACTION_UNLOCK = "unlock";
    private static final String ACTION_ENABLE = "enable";

    private FragmentLoginBinding binding;
    private final GoogleSignInCoordinator googleSignIn = new GoogleSignInCoordinator();
    private boolean authenticating;
    private BiometricGate fingerprintGate;
    // Saved across rotation/background so the dialog opens by itself only once and its result is never lost.
    private boolean autoPrompted;
    private String pendingAction;
    private String pendingEnableAccount;

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (savedInstanceState != null) {
            autoPrompted = savedInstanceState.getBoolean(STATE_AUTO_PROMPTED);
            pendingAction = savedInstanceState.getString(STATE_PENDING_ACTION);
            pendingEnableAccount = savedInstanceState.getString(STATE_PENDING_ACCOUNT);
        }
    }

    @Override
    public void onSaveInstanceState(@NonNull Bundle outState) {
        super.onSaveInstanceState(outState);
        outState.putBoolean(STATE_AUTO_PROMPTED, autoPrompted);
        outState.putString(STATE_PENDING_ACTION, pendingAction);
        outState.putString(STATE_PENDING_ACCOUNT, pendingEnableAccount);
    }

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
        // Created with every view: reconnects to a fingerprint dialog that survived a rotation.
        fingerprintGate = new BiometricGate(this, new BiometricGate.Callback() {
            @Override public void onSuccess(@Nullable Cipher cipher) {
                String action = pendingAction;
                pendingAction = null;
                if (ACTION_ENABLE.equals(action)) onEnableConfirmed();
                else onFingerprintUnlocked(cipher);
            }

            @Override public void onError(BiometricLoginPolicy.PromptError kind, @NonNull String message) {
                String action = pendingAction;
                pendingAction = null;
                if (ACTION_ENABLE.equals(action)) {
                    showToast("Chưa bật đăng nhập bằng vân tay. Bạn có thể bật trong Hồ sơ.");
                    if (binding != null) resetTo(new BranchListFragment());
                    return;
                }
                // Cancel / "Dùng mật khẩu": stay here and type the password. Lockout: same, with a message.
                if (binding == null || kind == BiometricLoginPolicy.PromptError.CANCELLED) return;
                showError(message);
            }
        });

        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnGoRegister.setOnClickListener(v -> navigateTo(new RegisterFragment()));
        binding.btnForgotPassword.setOnClickListener(v -> navigateTo(new ForgotPasswordFragment()));
        binding.btnGoogleSignIn.setOnClickListener(v -> startGoogleSignIn());
        binding.btnFingerprint.setOnClickListener(v -> {
            pendingAction = null; // a dialog that vanished without a result (e.g. during rotation) must not block a retry
            startFingerprintLogin();
        });
        binding.btnOtherAccount.setOnClickListener(v -> confirmOtherAccount());
        bindMode();
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

    @Override
    public void onResume() {
        super.onResume();
        bindMode(); // fingerprints may have been added or removed in system settings meanwhile
        maybeAutoPrompt();
    }

    /** The remembered account, or null when this screen is the normal login form. */
    @Nullable
    private String rememberedAccount() {
        String owner = BiometricTokenStore.getInstance().sealedOwner();
        return owner != null && BiometricGate.deviceReady(getContext()) ? owner : null;
    }

    private void bindMode() {
        if (binding == null) return;
        BiometricTokenStore store = BiometricTokenStore.getInstance();
        boolean remembered = rememberedAccount() != null;
        String loginId = remembered ? store.sealedLoginId() : "";
        binding.groupRemembered.setVisibility(remembered ? View.VISIBLE : View.GONE);
        binding.btnOtherAccount.setVisibility(remembered ? View.VISIBLE : View.GONE);
        binding.btnGoRegister.setVisibility(remembered ? View.GONE : View.VISIBLE);
        binding.tilEmail.setVisibility(remembered && !loginId.isEmpty() ? View.GONE : View.VISIBLE);
        binding.tvLoginSubtitle.setText(remembered
                ? "Quét vân tay hoặc nhập mật khẩu của tài khoản này."
                : "Đăng nhập để đặt bàn, tích điểm và nhận ưu đãi riêng.");
        if (remembered) {
            binding.tvGreeting.setText(BiometricLoginPolicy.greeting(store.sealedLabel()));
            if (!loginId.isEmpty()) binding.etEmail.setText(loginId);
        }
    }

    private void maybeAutoPrompt() {
        if (binding == null) return;
        if (!BiometricLoginPolicy.shouldAutoPrompt(rememberedAccount() != null, true,
                MobileRepository.getInstance().hasSession(), autoPrompted || pendingAction != null)) return;
        autoPrompted = true;
        // After the resume settles; a screen that is gone by then (or already logged in) never opens the dialog.
        binding.getRoot().post(() -> {
            if (binding == null || !isResumed() || MobileRepository.getInstance().hasSession()) return;
            if (!startFingerprintLogin()) autoPrompted = false;
        });
    }

    /** Opens the fingerprint dialog for the remembered account; false when nothing was shown. */
    private boolean startFingerprintLogin() {
        if (authenticating || pendingAction != null) return false;
        String owner = rememberedAccount();
        if (owner == null) { bindMode(); return false; }
        Cipher cipher;
        try {
            cipher = BiometricTokenStore.getInstance().decryptCipher(owner);
        } catch (KeyPermanentlyInvalidatedException changed) {
            dropFingerprint(owner, BiometricLoginPolicy.KEY_CHANGED_MESSAGE);
            return false;
        } catch (Exception transientFailure) {
            showError(BiometricLoginPolicy.UNAVAILABLE_MESSAGE); // delete nothing on doubt; the password still works
            return false;
        }
        binding.tvError.setVisibility(View.GONE);
        pendingAction = ACTION_UNLOCK;
        if (fingerprintGate.unlock(cipher)) return true;
        pendingAction = null;
        return false;
    }

    private void onFingerprintUnlocked(@Nullable Cipher unlocked) {
        if (binding == null) return;
        BiometricTokenStore store = BiometricTokenStore.getInstance();
        String owner = store.sealedOwner();
        if (owner == null || unlocked == null) { showError(BiometricLoginPolicy.UNAVAILABLE_MESSAGE); return; }
        String refreshToken;
        try {
            refreshToken = store.open(unlocked);
        } catch (Exception unusable) {
            if (BiometricLoginPolicy.shouldWipeAfterKeyError(unusable instanceof KeyPermanentlyInvalidatedException)) {
                dropFingerprint(owner, BiometricLoginPolicy.KEY_CHANGED_MESSAGE);
            } else {
                showError(BiometricLoginPolicy.UNAVAILABLE_MESSAGE);
            }
            return;
        }
        setAuthenticating(true, com.restaurant.viettable.R.string.auth_continue_with_google);
        MobileRepository.getInstance().loginWithSealedToken(refreshToken, new MobileRepository.Callback<User>() {
            @Override public void onSuccess(User value) {
                if (binding == null) return;
                setAuthenticating(false, com.restaurant.viettable.R.string.auth_continue_with_google);
                showToast("Đăng nhập thành công!");
                resetTo(new BranchListFragment());
            }

            @Override public void onError(ApiException error) {
                BiometricLoginPolicy.RefreshFailure failure =
                        BiometricLoginPolicy.classifyRefreshFailure(error.getStatusCode(), error.getCode());
                // Expired/revoked tokens are dropped even if the screen is gone; anything else is kept for a retry.
                if (failure == BiometricLoginPolicy.RefreshFailure.REAUTH_REQUIRED) store.disable(owner);
                if (binding == null) return;
                setAuthenticating(false, com.restaurant.viettable.R.string.auth_continue_with_google);
                bindMode();
                showError(failure == BiometricLoginPolicy.RefreshFailure.REAUTH_REQUIRED
                        ? BiometricLoginPolicy.SESSION_EXPIRED_MESSAGE
                        : failure == BiometricLoginPolicy.RefreshFailure.NETWORK
                        ? BiometricLoginPolicy.NETWORK_MESSAGE : apiError(error));
            }
        });
    }

    /**
     * "Đăng nhập tài khoản khác": deletes this account's fingerprint key, so the sealed refresh token can never be
     * decrypted again (by anyone), then shows the empty login form.
     */
    private void confirmOtherAccount() {
        String owner = BiometricTokenStore.getInstance().sealedOwner();
        String label = BiometricTokenStore.getInstance().sealedLabel();
        new MaterialAlertDialogBuilder(requireContext())
                .setTitle("Đăng nhập tài khoản khác?")
                .setMessage("Đăng nhập bằng vân tay của " + (label.isEmpty() ? "tài khoản này" : label)
                        + " sẽ bị xoá khỏi máy này.")
                .setPositiveButton("Tiếp tục", (dialog, which) -> {
                    if (owner != null) BiometricTokenStore.getInstance().disable(owner);
                    if (binding == null) return;
                    binding.etEmail.setText("");
                    binding.etPassword.setText("");
                    binding.tvError.setVisibility(View.GONE);
                    bindMode();
                    binding.etEmail.requestFocus();
                })
                .setNegativeButton("Hủy", null)
                .show();
    }

    /** Fingerprints changed or the key is unusable: turn the feature off and clean up, never crash. */
    private void dropFingerprint(String owner, String message) {
        BiometricTokenStore.getInstance().disable(owner);
        bindMode();
        showError(message);
    }

    /**
     * Password or Google login succeeded. Same account: fingerprint stays on (only the superseded sealed token is
     * dropped). Another account: the previous accounts' fingerprint data is wiped. Then offer fingerprint once.
     */
    private void afterInteractiveLogin(User user, String message) {
        if (binding == null) return;
        BiometricTokenStore store = BiometricTokenStore.getInstance();
        String accountId = user == null ? null : user.getId();
        store.keepOnlyAccount(accountId);
        showToast(message);
        if (accountId == null || !BiometricLoginPolicy.shouldOffer(BiometricGate.deviceReady(getContext()),
                store.isEnabled(accountId), store.wasAsked(accountId))) {
            resetTo(new BranchListFragment());
            return;
        }
        store.markAsked(accountId);
        new MaterialAlertDialogBuilder(requireContext())
                .setTitle("Bật đăng nhập bằng vân tay?")
                .setMessage("Lần sau bạn có thể đăng nhập nhanh bằng vân tay trên máy này. "
                        + "VietTable không lưu mật khẩu của bạn. Bạn có thể bật hoặc tắt trong Hồ sơ.")
                .setCancelable(false)
                .setPositiveButton("Bật", (dialog, which) -> {
                    pendingAction = ACTION_ENABLE;
                    pendingEnableAccount = accountId;
                    if (!fingerprintGate.confirm()) {
                        pendingAction = null;
                        resetTo(new BranchListFragment());
                    }
                })
                .setNegativeButton("Để sau", (dialog, which) -> navigateTo(new BranchListFragment()))
                .show();
    }

    private void onEnableConfirmed() {
        String accountId = pendingEnableAccount;
        pendingEnableAccount = null;
        try {
            BiometricTokenStore.getInstance().enable(accountId);
            showToast("Đã bật đăng nhập bằng vân tay.");
        } catch (Exception failure) {
            BiometricTokenStore.getInstance().disable(accountId);
            showToast(BiometricLoginPolicy.UNAVAILABLE_MESSAGE);
        }
        if (binding != null) resetTo(new BranchListFragment());
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

    private void setAuthenticating(boolean value, int googleButtonText) {
        authenticating = value;
        if (binding == null) return;
        binding.tvError.setVisibility(View.GONE);
        binding.btnSubmitLogin.setEnabled(!value);
        binding.btnGoogleSignIn.setEnabled(!value);
        binding.btnGoRegister.setEnabled(!value);
        binding.btnForgotPassword.setEnabled(!value);
        binding.btnFingerprint.setEnabled(!value);
        binding.btnOtherAccount.setEnabled(!value);
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
