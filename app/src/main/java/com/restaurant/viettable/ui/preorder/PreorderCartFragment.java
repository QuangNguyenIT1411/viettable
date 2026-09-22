package com.restaurant.viettable.ui.preorder;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.restaurant.viettable.adapter.CartAdapter;
import com.restaurant.viettable.databinding.FragmentPreorderCartBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.CartItem;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.List;

public class PreorderCartFragment extends BaseFragment {

    private static final String ARG_RES = "arg_res";
    private FragmentPreorderCartBinding binding;
    private Reservation reservation;
    private CartAdapter adapter;

    public static PreorderCartFragment newInstance(Reservation res) {
        PreorderCartFragment f = new PreorderCartFragment();
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
        binding = FragmentPreorderCartBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        if (reservation != null) {
            binding.tvPreorderBoundNotice.setText("Gắn với: #" + reservation.getId() + " • Bàn " + reservation.getTableId() + " • " + reservation.getBranchName());
        }

        List<CartItem> cartItems = appStateManager.getCartItems();
        adapter = new CartAdapter(cartItems, new CartAdapter.OnCartItemActionListener() {
            @Override
            public void onQuantityChanged(CartItem item, int newQuantity) {
                updateTotals();
            }

            @Override
            public void onRemoveItem(CartItem item) {
                appStateManager.removeCartItem(item);
                adapter.notifyDataSetChanged();
                updateTotals();
                if (appStateManager.getCartItems().isEmpty()) {
                    showToast("Giỏ hàng Pre-order trống.");
                    navigateBack();
                }
            }
        });

        binding.rvPreorderCartItems.setLayoutManager(new LinearLayoutManager(getContext()));
        binding.rvPreorderCartItems.setAdapter(adapter);

        binding.btnClearPreorderCart.setOnClickListener(v -> {
            appStateManager.clearCart();
            adapter.notifyDataSetChanged();
            showToast("Đã xóa giỏ hàng Pre-order.");
            navigateBack();
        });

        binding.btnSubmitPreorderCart.setOnClickListener(v -> {
            if (appStateManager.getCartItems().isEmpty()) {
                showToast("Giỏ hàng đang trống!");
                return;
            }
            if (reservation == null) { showToast("Không tìm thấy đặt bàn."); return; }
            binding.btnSubmitPreorderCart.setEnabled(false);
            MobileRepository.getInstance().submitPreOrder(reservation, new MobileRepository.Callback<org.json.JSONObject>() {
                @Override public void onSuccess(org.json.JSONObject value) {
                    if (binding == null) return;
                    binding.btnSubmitPreorderCart.setEnabled(true);
                    navigateTo(PreorderConfirmFragment.newInstance(reservation));
                }
                @Override public void onError(ApiException error) {
                    if (binding == null) return;
                    binding.btnSubmitPreorderCart.setEnabled(true);
                    showToast(apiError(error));
                }
            });
        });

        updateTotals();
    }

    private void updateTotals() {
        long subtotal = appStateManager.calculateCartSubtotal();
        String formatted = String.format(java.util.Locale.GERMANY, "%,dđ", subtotal);
        binding.tvPreorderSubtotal.setText(formatted);
        binding.tvPreorderTotal.setText(formatted);
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
