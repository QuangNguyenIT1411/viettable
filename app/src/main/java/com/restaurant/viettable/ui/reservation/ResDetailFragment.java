package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentResDetailBinding;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.preorder.PreorderDetailFragment;
import com.restaurant.viettable.ui.preorder.PreorderMenuFragment;
import com.restaurant.viettable.ui.session.QrScanFragment;
import com.restaurant.viettable.util.DisplayFormatters;
import com.restaurant.viettable.util.ReservationSchedule;
import com.restaurant.viettable.util.SafeText;

import java.util.List;

public class ResDetailFragment extends BaseFragment implements AppStateManager.StateChangeListener {

    private static final String ARG_RES = "arg_res";
    private FragmentResDetailBinding binding;
    private Reservation reservation;

    public static ResDetailFragment newInstance(Reservation res) {
        ResDetailFragment f = new ResDetailFragment();
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
        binding = FragmentResDetailBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        if (reservation == null) {
            showToast("Không tìm thấy thông tin đặt bàn.");
            navigateBack();
            return;
        }

        bind();
        appStateManager.addListener(this);
        // The restaurant may have confirmed or rejected it meanwhile; the list refresh re-binds through onStateChanged.
        MobileRepository.getInstance().loadReservations(new MobileRepository.Callback<List<Reservation>>() {
            @Override public void onSuccess(List<Reservation> value) {}
            @Override public void onError(ApiException error) {}
        });
    }

    /** A newer copy of this reservation arrived (realtime "reservationChanged" or a list refresh). */
    @Override
    public void onStateChanged() {
        if (binding == null || reservation == null) return;
        for (Reservation r : appStateManager.getReservations()) {
            if (r.getId() != null && r.getId().equals(reservation.getId())) {
                reservation = r;
                bind();
                return;
            }
        }
    }

    private void bind() {
        binding.tvDetailResId.setText("#" + reservation.getId());
        binding.tvDetailBranch.setText(reservation.getBranchName());
        binding.tvDetailDateTime.setText(reservation.getTimeSlot() + " • " + DisplayFormatters.date(reservation.getDate()));
        binding.tvDetailTable.setText(SafeText.join(" • ", reservation.getTableLabel(), reservation.getAreaName(), reservation.getGuestCount() + " khách"));
        com.restaurant.viettable.ui.common.PdStatusPill.Tone tone =
                com.restaurant.viettable.ui.common.PdStatusPill.toneFor(reservation.getStatus());
        com.restaurant.viettable.ui.common.PdStatusPill.apply(binding.tvDetailStatusBadge, reservation.getStatusDisplayText(), tone);
        // Rejection / cancellation reason sits on soft red, other notes on soft yellow (section 6).
        binding.tvDetailStatusInfo.setBackgroundResource(tone == com.restaurant.viettable.ui.common.PdStatusPill.Tone.ERROR
                ? R.drawable.pd_card_red_soft : R.drawable.pd_card_yellow_soft);

        if (!SafeText.isBlank(reservation.getNote())) {
            binding.tvDetailNote.setVisibility(View.VISIBLE);
            binding.tvDetailNote.setText("Ghi chú: " + SafeText.clean(reservation.getNote()));
        } else {
            binding.tvDetailNote.setVisibility(View.GONE);
        }
        String statusInfo = ReservationSchedule.statusInfo(reservation.getStatus(), reservation.getStatusReason());
        binding.tvDetailStatusInfo.setVisibility(statusInfo == null ? View.GONE : View.VISIBLE);
        binding.tvDetailStatusInfo.setText(statusInfo == null ? "" : statusInfo);

        if (reservation.hasPreOrder()) {
            binding.tvPreOrderNotice.setText("Đã có món ăn đặt trước cho lịch đặt bàn này.");
            binding.btnGoPreOrder.setText("Xem danh sách món đặt trước");
            binding.btnGoPreOrder.setOnClickListener(v -> navigateTo(PreorderDetailFragment.newInstance(reservation)));
        } else {
            binding.tvPreOrderNotice.setText("Đặt món trước giúp bếp chuẩn bị kịp thời ngay khi bạn vừa ngồi vào bàn.");
            binding.btnGoPreOrder.setOnClickListener(v -> navigateTo(PreorderMenuFragment.newInstance(reservation)));
        }

        binding.btnScanQrCheckin.setOnClickListener(v -> navigateTo(new QrScanFragment()));
        binding.btnChangeRes.setOnClickListener(v -> navigateTo(ResChangeFragment.newInstance(reservation)));
        binding.btnCancelRes.setOnClickListener(v -> navigateTo(ResCancelFragment.newInstance(reservation)));

        // Only the actions the server would accept now (before: always shown, even when cancelled or checked in).
        java.time.Instant reservedAt = DisplayFormatters.instant(reservation.getReservedAt());
        java.time.Instant now = java.time.Instant.now();
        Reservation.Status status = reservation.getStatus();
        binding.btnScanQrCheckin.setVisibility(ReservationSchedule.canCheckIn(status, reservedAt, now) ? View.VISIBLE : View.GONE);
        binding.btnChangeRes.setVisibility(ReservationSchedule.canChange(status, reservedAt, now) ? View.VISIBLE : View.GONE);
        binding.btnCancelRes.setVisibility(ReservationSchedule.canCancel(status, reservedAt, now) ? View.VISIBLE : View.GONE);
        boolean preOrderOpen = ReservationSchedule.canPreOrder(status, reservedAt, now);
        binding.btnGoPreOrder.setVisibility(preOrderOpen || reservation.hasPreOrder() ? View.VISIBLE : View.GONE);
        if (preOrderOpen || reservation.hasPreOrder()) {
            binding.tvPreOrderNotice.setVisibility(View.VISIBLE);
        } else if (status == Reservation.Status.PENDING && reservedAt != null && now.isBefore(reservedAt)) {
            binding.tvPreOrderNotice.setVisibility(View.VISIBLE);
            binding.tvPreOrderNotice.setText("Đặt món trước sẽ mở sau khi nhà hàng xác nhận đặt bàn.");
        } else {
            binding.tvPreOrderNotice.setVisibility(View.GONE);
        }
    }

    @Override
    public void onDestroyView() {
        appStateManager.removeListener(this);
        super.onDestroyView();
        binding = null;
    }
}
