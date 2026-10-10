package com.restaurant.viettable.ui.common;

import android.view.View;

import com.restaurant.viettable.databinding.ViewActiveDiningCardBinding;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.navigation.NavigationCoordinator;
import com.restaurant.viettable.util.DisplayFormatters;

public class ActiveDiningCardBinder {

    public interface OnActionListener {
        void onCardClicked();
        void onOrderMoreClicked();
        void onTrackOrdersClicked();
        void onPayClicked();
    }

    public static void bind(ViewActiveDiningCardBinding binding, DiningSession session, OnActionListener listener) {
        if (session == null || session.getStatus() == DiningSession.Status.CLOSED || session.getStatus() == DiningSession.Status.CANCELLED) {
            binding.getRoot().setVisibility(View.GONE);
            return;
        }

        binding.getRoot().setVisibility(View.VISIBLE);
        binding.tvDiningTable.setText(session.getTableLabel() + " • " + session.getBranchName());

        if (session.getStatus() == DiningSession.Status.PAYMENT_REQUESTED) {
            binding.tvDiningBadge.setText("Đang yêu cầu thanh toán");
            binding.tvDiningBadge.setBackgroundResource(com.restaurant.viettable.R.drawable.bg_status_badge);
            binding.tvDiningBadge.setBackgroundTintList(
                    android.content.res.ColorStateList.valueOf(0xFFFFF3E0)
            );
            binding.tvDiningBadge.setTextColor(0xFFD84315);
            binding.btnDiningOrderMore.setEnabled(true);
            binding.btnDiningOrderMore.setAlpha(0.8f);
        } else {
            binding.tvDiningBadge.setText("Đang dùng bữa");
            binding.tvDiningBadge.setBackgroundResource(com.restaurant.viettable.R.drawable.bg_status_badge);
            binding.tvDiningBadge.setBackgroundTintList(
                    android.content.res.ColorStateList.valueOf(0xFFE8F5E9)
            );
            binding.tvDiningBadge.setTextColor(0xFF2E7D5B);
            binding.btnDiningOrderMore.setEnabled(true);
            binding.btnDiningOrderMore.setAlpha(1.0f);
        }

        int activeOrders = session.getActiveOrdersCount();
        String summary = (activeOrders > 0 ? activeOrders + " đợt gọi món đang xử lý" : "Chưa có đợt gọi món mới")
                + " • Bắt đầu lúc " + DisplayFormatters.dateTime(session.getStartTime());
        binding.tvDiningOrdersSummary.setText(summary);

        binding.getRoot().setOnClickListener(v -> {
            if (listener != null) listener.onCardClicked();
        });

        binding.btnDiningOrderMore.setOnClickListener(v -> {
            if (session.getStatus() == DiningSession.Status.PAYMENT_REQUESTED) {
                DialogHelper.showConfirmDialog(
                        binding.getRoot().getContext(),
                        "Đang yêu cầu thanh toán",
                        "Bàn đang trong trạng thái chờ thanh toán nên không thể gọi thêm món. Bạn có muốn hủy yêu cầu thanh toán để tiếp tục gọi món?",
                        "Hủy yêu cầu & Gọi món",
                        (dialog, which) -> {
                            session.setStatus(DiningSession.Status.ACTIVE);
                            if (listener != null) listener.onOrderMoreClicked();
                        },
                        "Đóng",
                        null
                );
                return;
            }
            if (listener != null) listener.onOrderMoreClicked();
        });

        binding.btnDiningTrack.setOnClickListener(v -> {
            if (listener != null) listener.onTrackOrdersClicked();
        });

        binding.btnDiningPay.setOnClickListener(v -> {
            if (listener != null) listener.onPayClicked();
        });
    }
}
