package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentResConfirmationBinding;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.DisplayFormatters;

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

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            reservation = (Reservation) getArguments().getSerializable(ARG_RES);
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
            binding.tvConfirmBranch.setText(reservation.getBranchName() + " (" + reservation.getBranchAddress() + ")");
            binding.tvConfirmTimeDate.setText(reservation.getTimeSlot() + " • " + DisplayFormatters.date(reservation.getDate()));
            binding.tvConfirmTableArea.setText("Bàn " + reservation.getTableId() + " • " + reservation.getAreaName() + " • " + reservation.getGuestCount() + " khách");

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
