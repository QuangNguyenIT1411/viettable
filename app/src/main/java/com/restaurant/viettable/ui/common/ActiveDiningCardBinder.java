package com.restaurant.viettable.ui.common;

import android.view.View;

import com.restaurant.viettable.databinding.ViewActiveDiningCardBinding;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.navigation.NavigationCoordinator;
import com.restaurant.viettable.util.DisplayFormatters;
import com.restaurant.viettable.util.SafeText;

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
        binding.tvDiningTable.setText(SafeText.join(" • ", session.getTableLabel(), session.getBranchName()));

        if (session.getStatus() == DiningSession.Status.PAYMENT_REQUESTED) {
            binding.tvDiningBadge.setText("Đang yêu cầu thanh toán");
            tintBadge(binding, com.restaurant.viettable.R.color.pd_yellow);
            binding.btnDiningOrderMore.setEnabled(true);
            binding.btnDiningOrderMore.setAlpha(0.8f);
        } else {
            binding.tvDiningBadge.setText("Đang dùng bữa");
            tintBadge(binding, com.restaurant.viettable.R.color.pd_mint);
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

    /** Solid pill: yellow while waiting for payment, mint while dining (DESIGN_PHO_DEM.md section 5). */
    private static void tintBadge(ViewActiveDiningCardBinding binding, int colorRes) {
        android.content.Context context = binding.getRoot().getContext();
        binding.tvDiningBadge.setBackgroundTintList(android.content.res.ColorStateList.valueOf(
                androidx.core.content.ContextCompat.getColor(context, colorRes)));
        binding.tvDiningBadge.setTextColor(androidx.core.content.ContextCompat.getColor(
                context, com.restaurant.viettable.R.color.pd_on_color));
    }
}
