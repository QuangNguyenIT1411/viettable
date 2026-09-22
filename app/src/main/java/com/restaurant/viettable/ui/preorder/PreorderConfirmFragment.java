package com.restaurant.viettable.ui.preorder;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentPreorderConfirmBinding;
import com.restaurant.viettable.model.CartItem;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.reservation.ResDetailFragment;

import java.util.List;

public class PreorderConfirmFragment extends BaseFragment {

    private static final String ARG_RES = "arg_res";
    private FragmentPreorderConfirmBinding binding;
    private Reservation reservation;

    public static PreorderConfirmFragment newInstance(Reservation res) {
        PreorderConfirmFragment f = new PreorderConfirmFragment();
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
        binding = FragmentPreorderConfirmBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (reservation != null) {
            binding.tvResCode.setText("#" + reservation.getId() + " (" + reservation.getTableId() + ")");
        }

        List<CartItem> preorders = appStateManager.getCartItems();
        int totalCount = 0;
        long totalPrice = appStateManager.calculateCartSubtotal();
        for (CartItem item : preorders) {
            totalCount += item.getQuantity();
        }

        binding.tvItemCount.setText(totalCount + " món");
        binding.tvEstimatedTotal.setText(String.format(java.util.Locale.GERMANY, "%,dđ", totalPrice));

        binding.btnViewPreorderDetail.setOnClickListener(v -> {
            navigateTo(PreorderDetailFragment.newInstance(reservation));
        });

        binding.btnBackToReservation.setOnClickListener(v -> {
            switchTab(R.id.nav_home);
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
