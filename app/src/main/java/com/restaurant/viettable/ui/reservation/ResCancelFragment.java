package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentResCancelBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.DisplayFormatters;

public class ResCancelFragment extends BaseFragment {

    private static final String ARG_RES = "arg_res";
    private FragmentResCancelBinding binding;
    private Reservation reservation;

    public static ResCancelFragment newInstance(Reservation res) {
        ResCancelFragment f = new ResCancelFragment();
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
        binding = FragmentResCancelBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnKeepReservation.setOnClickListener(v -> navigateBack());

        if (reservation == null) {
            showToast("Không tìm thấy thông tin đặt bàn.");
            navigateBack();
            return;
        }

        if (reservation != null) {
            binding.tvCancelResCode.setText("Mã đặt bàn: #" + reservation.getId());
            binding.tvCancelResSummary.setText(reservation.getBranchName() + " • " + reservation.getTimeSlot() + ", " + DisplayFormatters.date(reservation.getDate()) + " • " + reservation.getTableLabel());

            if (reservation.getDepositAmount() > 0) {
                binding.tvRefundDetails.setText("Tiền cọc " + String.format(java.util.Locale.GERMANY, "%,dđ", reservation.getDepositAmount()) + " sẽ được hoàn trả về tài khoản ban đầu trong 1-3 ngày làm việc theo chính sách hoàn cọc.");
            } else {
                binding.tvRefundDetails.setText("Lịch đặt bàn này không có tiền cọc. Bạn không bị tính phí.");
            }

            binding.btnConfirmCancelDestructive.setOnClickListener(v -> {
                com.restaurant.viettable.ui.common.DialogHelper.showDestructiveDialog(
                        requireContext(),
                        "Xác nhận hủy đặt bàn?",
                        "Bạn có chắc chắn muốn hủy đặt bàn #" + reservation.getId() + " không?",
                        "Xác nhận hủy",
                        (dialog, which) -> {
                            MobileRepository.getInstance().cancelReservation(reservation, new MobileRepository.Callback<Reservation>() {
                                @Override public void onSuccess(Reservation value) { showToast("Đã hủy đặt bàn #" + value.getId()); navigateBack(); }
                                @Override public void onError(ApiException error) { showToast(apiError(error)); }
                            });
                        },
                        "Không",
                        null
                );
            });
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
