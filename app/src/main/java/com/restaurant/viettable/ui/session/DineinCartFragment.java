package com.restaurant.viettable.ui.session;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.restaurant.viettable.adapter.CartAdapter;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentDineinCartBinding;
import com.restaurant.viettable.model.CartItem;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.List;

public class DineinCartFragment extends BaseFragment {

    private FragmentDineinCartBinding binding;
    private CartAdapter adapter;

    public static DineinCartFragment newInstance() {
        return new DineinCartFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentDineinCartBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        DiningSession session = appStateManager.getActiveDiningSession();
        if (session != null) {
            binding.tvDineinCartTable.setText("Gửi tới: " + session.getTableId() + " • " + session.getBranchName());
        }

        List<CartItem> cartItems = appStateManager.getCartItems();
        adapter = new CartAdapter(cartItems, new CartAdapter.OnCartItemActionListener() {
            @Override
            public void onQuantityChanged(CartItem item, int newQuantity) {
                updateTotals();
            }

            @Override
            public void onRemoveItem(CartItem item) {
                com.restaurant.viettable.ui.common.DialogHelper.showDestructiveDialog(
                        requireContext(),
                        "Xóa món?",
                        "Bạn có chắc muốn xóa món " + item.getFoodItem().getName() + " khỏi giỏ gọi món?",
                        "Xóa món",
                        (dialog, which) -> {
                            appStateManager.removeCartItem(item);
                            adapter.notifyDataSetChanged();
                            updateTotals();
                            if (appStateManager.getCartItems().isEmpty()) {
                                showToast("Giỏ món trống.");
                                navigateBack();
                            }
                        },
                        "Giữ lại",
                        null
                );
            }
        });

        binding.rvDineinCartItems.setLayoutManager(new LinearLayoutManager(requireContext()));
        binding.rvDineinCartItems.setAdapter(adapter);

        binding.btnClearDineinCart.setOnClickListener(v -> {
            com.restaurant.viettable.ui.common.DialogHelper.showDestructiveDialog(
                    requireContext(),
                    "Xóa giỏ món?",
                    "Bạn có chắc muốn xóa toàn bộ món trong giỏ gọi món?",
                    "Xóa giỏ món",
                    (dialog, which) -> {
                        appStateManager.clearCart();
                        showToast("Đã xóa giỏ gọi món.");
                        navigateBack();
                    },
                    "Giữ lại",
                    null
            );
        });

        binding.btnSubmitDineinOrder.setOnClickListener(v -> {
            if (appStateManager.getCartItems().isEmpty()) {
                showToast("Vui lòng chọn món trước khi gửi!");
                return;
            }

            DiningSession currentSession = appStateManager.getActiveDiningSession();
            if (currentSession != null && currentSession.getStatus() == DiningSession.Status.PAYMENT_REQUESTED) {
                com.restaurant.viettable.ui.common.DialogHelper.showConfirmDialog(
                        requireContext(),
                        "Không thể gửi món",
                        "Bàn đang trong trạng thái yêu cầu thanh toán. Vui lòng hủy yêu cầu thanh toán trước khi gửi thêm món.",
                        "Hủy yêu cầu thanh toán & Gửi món",
                        (dialog, which) -> {
                            MobileRepository.getInstance().cancelPaymentRequest(new MobileRepository.Callback<DiningSession>() {
                                @Override public void onSuccess(DiningSession value) { submitOrder(); }
                                @Override public void onError(ApiException error) { showToast(apiError(error)); }
                            });
                        },
                        "Đóng",
                        null
                );
                return;
            }

            submitOrder();
        });

        updateTotals();
    }

    private void submitOrder() {
        if (binding == null) return;
        binding.btnSubmitDineinOrder.setEnabled(false);
        MobileRepository.getInstance().submitCurrentCart(new MobileRepository.Callback<Order>() {
            @Override public void onSuccess(Order value) {
                if (binding == null) return;
                showToast("Đã gửi đơn gọi món thành công!"); navigateTo(OrderConfirmFragment.newInstance(value));
            }
            @Override public void onError(ApiException error) {
                if (binding != null) { binding.btnSubmitDineinOrder.setEnabled(true); showToast(apiError(error)); }
            }
        });
    }

    private void updateTotals() {
        long total = appStateManager.calculateCartSubtotal();
        binding.tvDineinCartTotal.setText(String.format(java.util.Locale.GERMANY, "%,dđ", total));
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
