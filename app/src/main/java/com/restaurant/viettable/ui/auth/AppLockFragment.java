package com.restaurant.viettable.ui.auth;

import android.os.Bundle;
import android.security.keystore.KeyPermanentlyInvalidatedException;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.BuildConfig;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentAppLockBinding;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.network.BiometricTokenStore;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.BiometricLoginPolicy;

import javax.crypto.Cipher;

/**
 * Full-screen app lock drawn above every screen (MainActivity's lock container), so the screen underneath — even a
 * payment that is still waiting — keeps its state and comes back unchanged after unlocking.
 */
public class AppLockFragment extends BaseFragment {
    public static final String TAG = "app_lock";
    private static final String STATE_AUTO_PROMPTED = "lockAutoPrompted";
    private static final String STATE_SCANNING = "lockScanning";
    private static final String STATE_PASSWORD_MODE = "lockPasswordMode";

    /** Implemented by MainActivity. */
    public interface Host {
        void onAppUnlocked();
        void onLockLoggedOut();
    }

    private FragmentAppLockBinding binding;
    private BiometricGate gate;
    private final GoogleSignInCoordinator googleSignIn = new GoogleSignInCoordinator();
    private boolean autoPrompted;
    private boolean scanning;
    private boolean passwordMode;
    private boolean busy;

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (savedInstanceState != null) {
            autoPrompted = savedInstanceState.getBoolean(STATE_AUTO_PROMPTED);
            scanning = savedInstanceState.getBoolean(STATE_SCANNING);
            passwordMode = savedInstanceState.getBoolean(STATE_PASSWORD_MODE);
        }
    }

    @Override
    public void onSaveInstanceState(@NonNull Bundle outState) {
        super.onSaveInstanceState(outState);
        outState.putBoolean(STATE_AUTO_PROMPTED, autoPrompted);
        outState.putBoolean(STATE_SCANNING, scanning);
        outState.putBoolean(STATE_PASSWORD_MODE, passwordMode);
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentAppLockBinding.inflate(inflater, container, false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        // Created with every view: reconnects to a dialog that survived a rotation (one dialog per unlock).
        gate = new BiometricGate(this, new BiometricGate.Callback() {
            @Override public void onSuccess(@Nullable Cipher cipher) {
                scanning = false;
                unlock();
            }

            @Override public void onError(BiometricLoginPolicy.PromptError kind, @NonNull String message) {
                scanning = false;
                if (binding == null) return;
                if (kind == BiometricLoginPolicy.PromptError.CANCELLED) { showPasswordMode(); return; }
                showPasswordMode();
                showError(message);
            }
        });
        BiometricTokenStore store = BiometricTokenStore.getInstance();
        binding.tvLockGreeting.setText(BiometricLoginPolicy.greeting(store.sessionLabel()));
        binding.btnLockFingerprint.setOnClickListener(v -> { scanning = false; startScan(); });
        binding.btnLockUsePassword.setOnClickListener(v -> showPasswordMode());
        binding.btnLockUnlockPassword.setOnClickListener(v -> unlockWithPassword());
        binding.etLockPassword.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == android.view.inputmethod.EditorInfo.IME_ACTION_DONE) { unlockWithPassword(); return true; }
            return false;
        });
        binding.btnLockGoogle.setOnClickListener(v -> unlockWithGoogle());
        binding.btnLockLogout.setOnClickListener(v -> logout());
        if (passwordMode) showPasswordMode();
        refreshFingerprintAvailability();
    }

    @Override
    public void onResume() {
        super.onResume();
        refreshFingerprintAvailability();
        if (autoPrompted || scanning || !fingerprintUsable()) return;
        autoPrompted = true;
        binding.getRoot().post(() -> {
            if (binding == null || !isResumed()) return;
            if (!startScan()) autoPrompted = false;
        });
    }

    private boolean fingerprintUsable() {
        return BiometricTokenStore.getInstance().isEnabled(BiometricTokenStore.getInstance().sessionAccount())
                && BiometricGate.deviceReady(getContext());
    }

    private void refreshFingerprintAvailability() {
        if (binding == null) return;
        boolean usable = fingerprintUsable();
        binding.btnLockFingerprint.setVisibility(usable ? View.VISIBLE : View.GONE);
        binding.tvLockFingerprintHint.setVisibility(usable ? View.VISIBLE : View.GONE);
        if (!usable) showPasswordMode();
    }

    /** Opens one fingerprint dialog; false when nothing was shown. */
    private boolean startScan() {
        if (busy || scanning || binding == null) return false;
        BiometricTokenStore store = BiometricTokenStore.getInstance();
        String account = store.sessionAccount();
        Cipher cipher;
        try {
            cipher = store.decryptCipher(account);
        } catch (KeyPermanentlyInvalidatedException changed) {
            store.disable(account); // Android declared the key dead: fingerprints changed
            refreshFingerprintAvailability();
            showError("Vân tay trên máy đã thay đổi. Vui lòng mở khoá bằng mật khẩu hoặc Google.");
            return false;
        } catch (Exception transientFailure) {
            showPasswordMode();
            showError(BiometricLoginPolicy.UNAVAILABLE_MESSAGE);
            return false;
        }
        hideError();
        scanning = gate.unlockApp(cipher);
        return scanning;
    }

    private void showPasswordMode() {
        passwordMode = true;
        if (binding == null) return;
        binding.groupLockPassword.setVisibility(View.VISIBLE);
        binding.btnLockUsePassword.setVisibility(View.GONE);
        boolean hasLoginId = !BiometricTokenStore.getInstance().sessionLoginId().isEmpty();
        binding.tilLockPassword.setVisibility(hasLoginId ? View.VISIBLE : View.GONE);
        binding.btnLockUnlockPassword.setVisibility(hasLoginId ? View.VISIBLE : View.GONE);
    }

    private void unlockWithPassword() {
        if (busy || binding == null) return;
        String password = binding.etLockPassword.getText() == null ? "" : binding.etLockPassword.getText().toString().trim();
        binding.tilLockPassword.setError(null);
        if (TextUtils.isEmpty(password)) { binding.tilLockPassword.setError("Vui lòng nhập mật khẩu."); return; }
        setBusy(true);
        MobileRepository.getInstance().verifyPassword(BiometricTokenStore.getInstance().sessionLoginId(), password,
                verifyCallback());
    }

    private void unlockWithGoogle() {
        if (busy || googleSignIn.isRequestInFlight()) return;
        if (!GoogleSignInConfig.isConfigured(BuildConfig.GOOGLE_WEB_CLIENT_ID)) {
            showError(getString(com.restaurant.viettable.R.string.auth_google_not_configured));
            return;
        }
        setBusy(true);
        googleSignIn.start(requireActivity(), BuildConfig.GOOGLE_WEB_CLIENT_ID, new GoogleSignInCoordinator.Callback() {
            @Override public void onIdToken(@NonNull String idToken) {
                MobileRepository.getInstance().verifyGoogle(idToken, verifyCallback());
            }
            @Override public void onCancelled() { setBusy(false); }
            @Override public void onError(@NonNull String message) { setBusy(false); showError(message); }
        });
    }

    /** Opens only for the account that is locked; the current session is never replaced. */
    private MobileRepository.Callback<String> verifyCallback() {
        return new MobileRepository.Callback<String>() {
            @Override public void onSuccess(String accountId) {
                setBusy(false);
                if (accountId != null && accountId.equals(BiometricTokenStore.getInstance().sessionAccount())) {
                    unlock();
                } else {
                    showError("Đây không phải tài khoản đang đăng nhập trên máy này.");
                }
            }
            @Override public void onError(ApiException error) {
                setBusy(false);
                showError(apiError(error));
            }
        };
    }

    private void logout() {
        if (busy) return;
        BiometricTokenStore store = BiometricTokenStore.getInstance();
        String account = store.sessionAccount();
        if (store.isEnabled(account)) {
            try {
                MobileRepository.getInstance().logoutKeepingFingerprint(account, store.sessionLabel(), store.sessionLoginId());
                finishLogout("Đã đăng xuất. Lần sau bạn có thể đăng nhập bằng vân tay.");
                return;
            } catch (Exception failure) {
                if (failure instanceof KeyPermanentlyInvalidatedException) store.disable(account);
            }
        }
        setBusy(true);
        MobileRepository.getInstance().logout(new MobileRepository.Callback<Void>() {
            @Override public void onSuccess(Void value) { setBusy(false); finishLogout("Đã đăng xuất thành công!"); }
            @Override public void onError(ApiException error) { setBusy(false); finishLogout("Đã đăng xuất thành công!"); }
        });
    }

    private void finishLogout(String message) {
        showToast(message);
        if (getActivity() instanceof Host) ((Host) getActivity()).onLockLoggedOut();
    }

    private void unlock() {
        if (getActivity() instanceof Host) ((Host) getActivity()).onAppUnlocked();
    }

    private void setBusy(boolean value) {
        busy = value;
        if (binding == null) return;
        binding.btnLockUnlockPassword.setEnabled(!value);
        binding.btnLockGoogle.setEnabled(!value);
        binding.btnLockFingerprint.setEnabled(!value);
        binding.btnLockLogout.setEnabled(!value);
        if (value) hideError();
    }

    private void showError(String message) {
        if (binding == null) return;
        binding.tvLockError.setVisibility(View.VISIBLE);
        binding.tvLockError.setText(message);
    }

    private void hideError() {
        if (binding != null) binding.tvLockError.setVisibility(View.GONE);
    }

    @Override
    public void onDestroyView() {
        googleSignIn.cancel();
        busy = false;
        super.onDestroyView();
        binding = null;
    }
}
