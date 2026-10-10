package com.restaurant.viettable.ui.session;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import com.restaurant.viettable.databinding.FragmentResMismatchBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

public class ReservationMismatchFragment extends BaseFragment {

    private static final String ARG_SCANNED_TABLE = "arg_scanned_table";
    private static final String ARG_RESERVED_TABLE = "arg_reserved_table";

    private FragmentResMismatchBinding binding;
    private String scannedTable;
    private String reservedTable;

    public static ReservationMismatchFragment newInstance(String scannedTable, String reservedTable) {
        ReservationMismatchFragment fragment = new ReservationMismatchFragment();
        Bundle args = new Bundle();
        args.putString(ARG_SCANNED_TABLE, scannedTable);
        args.putString(ARG_RESERVED_TABLE, reservedTable);
        fragment.setArguments(args);
        return fragment;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            scannedTable = getArguments().getString(ARG_SCANNED_TABLE);
            reservedTable = getArguments().getString(ARG_RESERVED_TABLE);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentResMismatchBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (scannedTable == null || scannedTable.trim().isEmpty()
                || reservedTable == null || reservedTable.trim().isEmpty()) {
            showToast("Không nhận được thông tin bàn hợp lệ.");
            navigateBack();
            return;
        }

        String scannedLabel = MobileRepository.getInstance().qrTableLabel(scannedTable);
        Reservation upcoming = appStateManager.getUpcomingReservation();
        String reservedLabel = upcoming == null ? "Chưa có thông tin bàn" : upcoming.getTableLabel();
        binding.tvScannedTable.setText(scannedLabel);
        binding.tvReservedTable.setText(reservedLabel);

        binding.btnRescan.setOnClickListener(v -> navigateBack());

        binding.btnSwitchToCurrentTable.setOnClickListener(v -> {
            com.restaurant.viettable.ui.common.DialogHelper.showConfirmDialog(
                    requireContext(),
                    "Đổi sang " + scannedLabel + "?",
                    "Hệ thống sẽ cập nhật đặt chỗ của bạn sang " + scannedLabel + " và bắt đầu phiên dùng bữa.",
                    "Đồng ý đổi bàn",
                    (dialog, which) -> {
                        Reservation reservation = appStateManager.getUpcomingReservation();
                        if (reservation == null) { showToast("Không tìm thấy đặt bàn phù hợp."); return; }
                        MobileRepository.getInstance().checkInReservation(reservation, scannedTable, true, new MobileRepository.Callback<DiningSession>() {
                            @Override public void onSuccess(DiningSession value) { reservation.setStatus(Reservation.Status.CHECKED_IN); showToast("Đổi bàn và check-in thành công!"); navigateTo(DiningSessionFragment.newInstance()); }
                            @Override public void onError(ApiException error) { showToast(apiError(error)); }
                        });
                    },
                    "Hủy",
                    null
            );
        });

        binding.btnAskStaff.setOnClickListener(v -> {
            showToast("Vui lòng liên hệ nhân viên tại nhà hàng để được hỗ trợ.");
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
