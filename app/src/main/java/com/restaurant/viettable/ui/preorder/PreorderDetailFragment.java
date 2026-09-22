package com.restaurant.viettable.ui.preorder;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.restaurant.viettable.adapter.CartAdapter;
import com.restaurant.viettable.databinding.FragmentPreorderDetailBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.CartItem;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.List;

public class PreorderDetailFragment extends BaseFragment {

    private static final String ARG_RES = "arg_res";
    private FragmentPreorderDetailBinding binding;
    private Reservation reservation;
    private CartAdapter adapter;

    public static PreorderDetailFragment newInstance(Reservation res) {
        PreorderDetailFragment f = new PreorderDetailFragment();
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
        binding = FragmentPreorderDetailBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        if (reservation != null) {
            binding.tvResInfo.setText("Mã: #" + reservation.getId() + " • Bàn " + reservation.getTableId() + " • " + reservation.getTimeSlot() + " - " + reservation.getDate());
        }

        setupRecyclerView();
        updateTotal();

        if (reservation != null) MobileRepository.getInstance().loadPreOrder(reservation,
                new MobileRepository.Callback<org.json.JSONObject>() {
                    @Override public void onSuccess(org.json.JSONObject value) {
                        if (binding == null) return;
                        adapter.notifyDataSetChanged();
                        updateTotal();
                        boolean editable = "DRAFT".equalsIgnoreCase(value.optString("status"));
                        binding.btnEditPreorder.setVisibility(editable ? View.VISIBLE : View.GONE);
                        binding.btnCancelPreorder.setVisibility(editable ? View.VISIBLE : View.GONE);
                    }
                    @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
                });

        binding.btnCancelPreorder.setOnClickListener(v -> showCancelConfirmDialog());

        binding.btnEditPreorder.setOnClickListener(v -> {
            navigateTo(PreorderMenuFragment.newInstance(reservation));
        });
    }

    private void setupRecyclerView() {
        List<CartItem> preorders = appStateManager.getCartItems();
        adapter = new CartAdapter(preorders, new CartAdapter.OnCartItemActionListener() {
            @Override
            public void onQuantityChanged(CartItem item, int newQuantity) {
                updateTotal();
            }

            @Override
            public void onRemoveItem(CartItem item) {
                appStateManager.removeCartItem(item);
                adapter.notifyDataSetChanged();
                updateTotal();
                if (appStateManager.getCartItems().isEmpty()) {
                    if (reservation != null) reservation.setHasPreOrder(false);
                    showToast("Đã xóa toàn bộ món đặt trước");
                    navigateBack();
                }
            }
        });
        binding.rvPreorderItems.setLayoutManager(new LinearLayoutManager(requireContext()));
        binding.rvPreorderItems.setAdapter(adapter);
    }

    private void updateTotal() {
        long total = appStateManager.calculateCartSubtotal();
        binding.tvTotalPrice.setText(String.format(java.util.Locale.GERMANY, "%,dđ", total));
    }

    private void showCancelConfirmDialog() {
        com.restaurant.viettable.ui.common.DialogHelper.showDestructiveDialog(
                requireContext(),
                "Hủy món đặt trước?",
                "Bạn có chắc muốn hủy các món đã chọn trước? Đặt bàn của bạn vẫn được giữ nguyên.",
                "Hủy món",
                (dialog, which) -> {
                    if (reservation == null) return;
                    MobileRepository.getInstance().clearPreOrder(reservation, new MobileRepository.Callback<org.json.JSONObject>() {
                        @Override public void onSuccess(org.json.JSONObject value) {
                            if (binding == null) return;
                            appStateManager.clearCart();
                            showToast("Đã hủy danh sách món đặt trước");
                            navigateBack();
                        }
                        @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
                    });
                },
                "Giữ lại",
                null
        );
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
