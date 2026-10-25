package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentResDetailBinding;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.preorder.PreorderDetailFragment;
import com.restaurant.viettable.ui.preorder.PreorderMenuFragment;
import com.restaurant.viettable.ui.session.QrScanFragment;
import com.restaurant.viettable.util.DisplayFormatters;
import com.restaurant.viettable.util.ReservationSchedule;
import com.restaurant.viettable.util.SafeText;

public class ResDetailFragment extends BaseFragment {

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

        if (reservation != null) {
            binding.tvDetailResId.setText("#" + reservation.getId());
            binding.tvDetailBranch.setText(reservation.getBranchName());
            binding.tvDetailDateTime.setText(reservation.getTimeSlot() + " • " + DisplayFormatters.date(reservation.getDate()));
            binding.tvDetailTable.setText(SafeText.join(" • ", reservation.getTableLabel(), reservation.getAreaName(), reservation.getGuestCount() + " khách"));
            binding.tvDetailStatusBadge.setText(reservation.getStatusDisplayText());
            binding.tvDetailStatusBadge.setBackgroundResource(R.drawable.bg_status_badge);
            switch (reservation.getStatus()) {
                case CONFIRMED:
                    binding.tvDetailStatusBadge.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFE8F5E9));
                    binding.tvDetailStatusBadge.setTextColor(0xFF1B5E20);
                    break;
                case CHECKED_IN:
                    binding.tvDetailStatusBadge.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFE1F5FE));
                    binding.tvDetailStatusBadge.setTextColor(0xFF01579B);
                    break;
                case COMPLETED:
                    binding.tvDetailStatusBadge.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFF5F5F5));
                    binding.tvDetailStatusBadge.setTextColor(0xFF616161);
                    break;
                case CANCELLED:
                case NO_SHOW:
                    binding.tvDetailStatusBadge.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFFFEBEE));
                    binding.tvDetailStatusBadge.setTextColor(0xFFB71C1C);
                    break;
            }

            if (!SafeText.isBlank(reservation.getNote())) {
                binding.tvDetailNote.setText("Ghi chú: " + SafeText.clean(reservation.getNote()));
            } else {
                binding.tvDetailNote.setVisibility(View.GONE);
            }

            if (reservation.hasPreOrder()) {
                binding.tvPreOrderNotice.setText("Đã có món ăn đặt trước cho lịch đặt bàn này.");
                binding.btnGoPreOrder.setText("Xem danh sách món đặt trước");
                binding.btnGoPreOrder.setOnClickListener(v -> navigateTo(PreorderDetailFragment.newInstance(reservation)));
            } else {
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
            if (!preOrderOpen && !reservation.hasPreOrder()) binding.tvPreOrderNotice.setVisibility(View.GONE);
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
