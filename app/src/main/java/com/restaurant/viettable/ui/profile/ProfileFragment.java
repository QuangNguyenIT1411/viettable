package com.restaurant.viettable.ui.profile;

import android.os.Bundle;
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

        bindUserData();
        setupNavigationRows();
    }

    @Override
    public void onResume() {
        super.onResume();
        setStatusBarLightIcons(false);
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

        binding.btnLogout.setOnClickListener(v -> {
            com.restaurant.viettable.ui.common.DialogHelper.showDestructiveDialog(
                    requireContext(),
                    "Đăng xuất?",
                    "Bạn có chắc chắn muốn đăng xuất tài khoản?",
                    "Đăng xuất",
                    (dialog, which) -> {
                        MobileRepository.getInstance().logout(new MobileRepository.Callback<Void>() {
                            @Override public void onSuccess(Void value) {
                                setBottomNavVisible(false);
                                showToast("Đã đăng xuất thành công!");
                                navigateTo(WelcomeFragment.newInstance());
                            }
                            @Override public void onError(ApiException error) {}
                        });
                    },
                    "Hủy",
                    null
            );
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
