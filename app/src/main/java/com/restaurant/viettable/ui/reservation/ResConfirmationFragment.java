package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentResConfirmationBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.reminder.ReservationReminders;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.DisplayFormatters;
import com.restaurant.viettable.util.SafeText;

public class ResConfirmationFragment extends BaseFragment {

    private static final String ARG_RES = "arg_res";
    private FragmentResConfirmationBinding binding;
    private Reservation reservation;

    public static ResConfirmationFragment newInstance(Reservation res) {
        ResConfirmationFragment f = new ResConfirmationFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_RES, res);
        f.setArguments(b);
        return f;
    }

    // Android 13+: booking reminders need the notification permission; asked once, a refusal changes nothing else.
    private final androidx.activity.result.ActivityResultLauncher<String> notificationPermission =
            registerForActivityResult(new androidx.activity.result.contract.ActivityResultContracts.RequestPermission(), granted -> {
                if (granted) MobileRepository.getInstance().loadReservations(new MobileRepository.Callback<java.util.List<Reservation>>() {
                    @Override public void onSuccess(java.util.List<Reservation> value) {}
                    @Override public void onError(com.restaurant.viettable.network.ApiException error) {}
                });
            });

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            reservation = (Reservation) getArguments().getSerializable(ARG_RES);
        }
        if (savedInstanceState == null && ReservationReminders.shouldAskPermission(requireContext())) {
            ReservationReminders.markPermissionAsked(requireContext());
            notificationPermission.launch(android.Manifest.permission.POST_NOTIFICATIONS);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentResConfirmationBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (reservation == null) {
            showToast("Không tìm thấy thông tin đặt bàn.");
            setBottomNavVisible(true);
            switchTab(R.id.nav_home);
            return;
        }

        if (reservation != null) {
            binding.tvConfirmCode.setText("#" + reservation.getId());
            if (reservation.getStatus() != null) {
                binding.tvConfirmStatus.setVisibility(android.view.View.VISIBLE);
                com.restaurant.viettable.ui.common.PdStatusPill.apply(binding.tvConfirmStatus, reservation.getStatusDisplayText(),
                        com.restaurant.viettable.ui.common.PdStatusPill.toneFor(reservation.getStatus()));
            }
            binding.tvConfirmBranch.setText(SafeText.isBlank(reservation.getBranchAddress()) ? SafeText.clean(reservation.getBranchName())
                    : SafeText.join(" ", SafeText.clean(reservation.getBranchName()), "(" + SafeText.clean(reservation.getBranchAddress()) + ")"));
            binding.tvConfirmTimeDate.setText(reservation.getTimeSlot() + " • " + DisplayFormatters.date(reservation.getDate()));
            binding.tvConfirmTableArea.setText(SafeText.join(" • ", reservation.getTableLabel(), reservation.getAreaName(), reservation.getGuestCount() + " khách"));

            if (reservation.getDepositAmount() > 0) {
                binding.tvConfirmDeposit.setText("Tiền cọc: Đã thanh toán " + String.format(java.util.Locale.GERMANY, "%,dđ", reservation.getDepositAmount()));
            } else {
                binding.tvConfirmDeposit.setText("Tiền cọc: Không yêu cầu");
            }
        }

        binding.btnViewReservationDetail.setOnClickListener(v -> {
            navigateTo(ResDetailFragment.newInstance(reservation));
        });

        binding.btnGoHome.setOnClickListener(v -> {
            setBottomNavVisible(true);
            switchTab(R.id.nav_home);
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
