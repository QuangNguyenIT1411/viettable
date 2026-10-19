package com.restaurant.viettable.ui.profile;

import android.os.Bundle;
import android.security.keystore.KeyPermanentlyInvalidatedException;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;

import com.restaurant.viettable.R;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentProfileBinding;
import com.restaurant.viettable.databinding.ViewActiveDiningCardBinding;
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.network.BiometricTokenStore;
import com.restaurant.viettable.ui.auth.BiometricGate;
import com.restaurant.viettable.util.BiometricLoginPolicy;
import com.restaurant.viettable.ui.auth.WelcomeFragment;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.common.ActiveDiningCardBinder;
import com.restaurant.viettable.ui.order.OrderHistoryFragment;
import com.restaurant.viettable.ui.order.CurrentOrdersFragment;
import com.restaurant.viettable.ui.reservation.ResHistoryFragment;
import com.restaurant.viettable.ui.session.DiningSessionFragment;
import com.restaurant.viettable.ui.session.DineinMenuFragment;
import com.restaurant.viettable.ui.payment.PaymentCheckoutFragment;

import java.util.Locale;
import com.restaurant.viettable.util.SafeText;

public class ProfileFragment extends BaseFragment implements AppStateManager.StateChangeListener {

    private FragmentProfileBinding binding;
    private boolean bindingFingerprintSwitch;
    private BiometricGate fingerprintGate;

    public static ProfileFragment newInstance() {
        return new ProfileFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentProfileBinding.inflate(inflater, container, false);
        setBottomNavVisible(true);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        appStateManager.addListener(this);
        fingerprintGate = new BiometricGate(this, new BiometricGate.Callback() {
            @Override public void onSuccess(@Nullable javax.crypto.Cipher ignored) { onEnableConfirmed(); }
            @Override public void onError(BiometricLoginPolicy.PromptError kind, @NonNull String message) {
                if (kind != BiometricLoginPolicy.PromptError.CANCELLED) showToast(message);
                if (binding == null) return;
                binding.switchBiometricLogin.setEnabled(true);
                setFingerprintSwitch(false);
            }
        });

        bindUserData();
        setupNavigationRows();
    }

    @Override
    public void onResume() {
        super.onResume();
        setStatusBarLightIcons(false);
        bindFingerprintRow();
        if (MobileRepository.getInstance().hasSession()) {
            MobileRepository.getInstance().loadProfile(new MobileRepository.Callback<User>() {
                @Override public void onSuccess(User value) {}
                @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
            });
        }
    }

    @Override
    public void onPause() {
        super.onPause();
        setStatusBarLightIcons(false);
    }

    private void bindUserData() {
        User user = appStateManager.getCurrentUser();
        if (user != null) {
            binding.tvProfileName.setText(SafeText.clean(user.getFullName()));
            binding.tvProfilePhone.setText(SafeText.clean(user.getPhone()));
            binding.tvProfileTierBadge.setText("HẠNG " + com.restaurant.viettable.util.StatusLabels.memberTier(user.getMembershipTier()).toUpperCase(java.util.Locale.ROOT));
            binding.tvProfilePoints.setText(String.format(Locale.GERMANY, "%,d điểm", user.getLoyaltyPoints()));
        }

        // Active Dining Session Card (persistent component)
        if (appStateManager.hasActiveDiningSession()) {
            binding.containerProfileActiveDining.setVisibility(View.VISIBLE);
            binding.containerProfileActiveDining.removeAllViews();
            ViewActiveDiningCardBinding cardBinding = ViewActiveDiningCardBinding.inflate(
                    getLayoutInflater(), binding.containerProfileActiveDining, true
            );
            ActiveDiningCardBinder.bind(cardBinding, appStateManager.getActiveDiningSession(), new ActiveDiningCardBinder.OnActionListener() {
                @Override
                public void onCardClicked() {
                    navigateTo(DiningSessionFragment.newInstance());
                }

                @Override
                public void onOrderMoreClicked() {
                    navigateTo(new DineinMenuFragment());
                }

                @Override
                public void onTrackOrdersClicked() {
                    navigateTo(new CurrentOrdersFragment());
                }

                @Override
                public void onPayClicked() {
                    navigateTo(new PaymentCheckoutFragment());
                }
            });
        } else {
            binding.containerProfileActiveDining.setVisibility(View.GONE);
        }
    }

    private void setupNavigationRows() {
        binding.btnEditProfileHeader.setOnClickListener(v -> {
            navigateTo(EditProfileFragment.newInstance());
        });

        binding.rowEditProfile.setOnClickListener(v -> {
            navigateTo(EditProfileFragment.newInstance());
        });

        binding.cardProfileLoyalty.setOnClickListener(v -> {
            navigateTo(LoyaltyFragment.newInstance());
        });

        binding.rowVouchers.setOnClickListener(v -> {
            navigateTo(CustomerVoucherFragment.newInstance());
        });

        binding.rowResHistory.setOnClickListener(v -> {
            navigateTo(ResHistoryFragment.newInstance());
        });

        binding.rowOrderHistory.setOnClickListener(v -> {
            switchTab(R.id.nav_orders);
        });

        binding.rowReviews.setOnClickListener(v -> navigateTo(ReviewHistoryFragment.newInstance()));

        binding.rowSettings.setOnClickListener(v -> {
            navigateTo(SettingsFragment.newInstance());
        });

        binding.rowSupport.setOnClickListener(v -> {
            navigateTo(SupportFragment.newInstance());
        });

        binding.switchBiometricLogin.setOnCheckedChangeListener((button, checked) -> {
            if (!bindingFingerprintSwitch) onFingerprintSwitchChanged(checked);
        });

        binding.btnLogout.setOnClickListener(v -> {
            String accountId = currentAccountId();
            // Like a banking app: with fingerprint on, "Đăng xuất" always keeps fingerprint login, no question.
            if (accountId != null && BiometricTokenStore.getInstance().isEnabled(accountId)) {
                logoutKeepingFingerprint();
                return;
            }
            com.restaurant.viettable.ui.common.DialogHelper.showDestructiveDialog(
                    requireContext(),
                    "Đăng xuất?",
                    "Bạn có chắc chắn muốn đăng xuất tài khoản?",
                    "Đăng xuất",
                    (dialog, which) -> {
                        logoutCompletely(null, "Đã đăng xuất thành công!");
                    },
                    "Hủy",
                    null
            );
        });
    }

    private String currentAccountId() {
        User user = appStateManager.getCurrentUser();
        String id = user == null ? null : user.getId();
        return id == null || id.trim().isEmpty() ? null : id;
    }

    /** The row exists only on phones with an enrolled strong fingerprint; re-checked on every resume. */
    private void bindFingerprintRow() {
        if (binding == null) return;
        String accountId = currentAccountId();
        boolean show = accountId != null && BiometricGate.deviceReady(getContext());
        binding.rowBiometricLogin.setVisibility(show ? View.VISIBLE : View.GONE);
        binding.dividerBiometricLogin.setVisibility(show ? View.VISIBLE : View.GONE);
        if (!show) return;
        BiometricTokenStore store = BiometricTokenStore.getInstance();
        boolean enabled = store.isEnabled(accountId);
        if (enabled) {
            try {
                store.decryptCipher(accountId);
            } catch (KeyPermanentlyInvalidatedException changed) {
                store.disable(accountId);
                enabled = false;
                showToast("Vân tay trên máy đã thay đổi nên đăng nhập bằng vân tay đã tắt. Bạn có thể bật lại.");
            } catch (Exception unusable) {
                store.disable(accountId);
                enabled = false;
            }
        }
        setFingerprintSwitch(enabled);
    }

    private void setFingerprintSwitch(boolean checked) {
        if (binding == null) return;
        bindingFingerprintSwitch = true;
        binding.switchBiometricLogin.setChecked(checked);
        bindingFingerprintSwitch = false;
    }

    private void onFingerprintSwitchChanged(boolean checked) {
        String accountId = currentAccountId();
        BiometricTokenStore store = BiometricTokenStore.getInstance();
        boolean enabledNow = accountId != null && store.isEnabled(accountId);
        // Ignore Android restoring an old switch state on a screen rebuilt off-screen (bug 19/10: second dialog).
        if (accountId == null || !BiometricLoginPolicy.isUserToggle(isResumed(), checked, enabledNow)) {
            setFingerprintSwitch(enabledNow);
            return;
        }
        if (!checked) {
            store.disable(accountId); // key and any sealed token for this account are deleted
            showToast("Đã tắt đăng nhập bằng vân tay.");
            return;
        }
        binding.switchBiometricLogin.setEnabled(false);
        if (!fingerprintGate.confirm()) {
            binding.switchBiometricLogin.setEnabled(true);
            setFingerprintSwitch(false);
        }
    }

    private void onEnableConfirmed() {
        String accountId = currentAccountId();
        BiometricTokenStore store = BiometricTokenStore.getInstance();
        boolean enabled = false;
        if (accountId != null) {
            try {
                store.enable(accountId);
                store.markAsked(accountId);
                enabled = true;
            } catch (Exception failure) {
                store.disable(accountId);
            }
        }
        showToast(enabled ? "Đã bật đăng nhập bằng vân tay." : BiometricLoginPolicy.UNAVAILABLE_MESSAGE);
        if (binding == null) return;
        binding.switchBiometricLogin.setEnabled(true);
        setFingerprintSwitch(enabled);
    }

    private void logoutKeepingFingerprint() {
        User user = appStateManager.getCurrentUser();
        if (user == null) { logoutCompletely(null, "Đã đăng xuất thành công!"); return; }
        try {
            MobileRepository.getInstance().logoutKeepingFingerprint(user);
        } catch (Exception failure) {
            // Could not seal (fingerprints changed, key gone, no session): fall back to a full, revoking logout.
            logoutCompletely(user.getId(), failure instanceof KeyPermanentlyInvalidatedException
                    ? BiometricLoginPolicy.KEY_CHANGED_MESSAGE
                    : "Không giữ được đăng nhập bằng vân tay nên đã đăng xuất hẳn.");
            return;
        }
        showToast("Đã đăng xuất. Lần sau bạn có thể đăng nhập bằng vân tay.");
        resetTo(WelcomeFragment.newInstance());
    }

    /** Revokes the refresh token on the server; with an account id also deletes its fingerprint key and token. */
    private void logoutCompletely(@Nullable String fingerprintAccountId, String message) {
        if (fingerprintAccountId != null) BiometricTokenStore.getInstance().disable(fingerprintAccountId);
        MobileRepository.getInstance().logout(new MobileRepository.Callback<Void>() {
            @Override public void onSuccess(Void value) {
                showToast(message);
                resetTo(WelcomeFragment.newInstance());
            }
            @Override public void onError(ApiException error) {}
        });
    }

    @Override
    public void onStateChanged() {
        if (binding != null) {
            bindUserData();
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        appStateManager.removeListener(this);
        binding = null;
    }
}
