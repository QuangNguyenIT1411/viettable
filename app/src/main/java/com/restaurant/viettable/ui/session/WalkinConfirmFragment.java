package com.restaurant.viettable.ui.session;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentWalkinConfirmBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

public class WalkinConfirmFragment extends BaseFragment {

    private static final String ARG_TABLE_CODE = "arg_table_code";
    private FragmentWalkinConfirmBinding binding;
    private String tableCode;
    private int guestCount = 2;

    public static WalkinConfirmFragment newInstance(String tableCode) {
        WalkinConfirmFragment fragment = new WalkinConfirmFragment();
        Bundle args = new Bundle();
        args.putString(ARG_TABLE_CODE, tableCode);
        fragment.setArguments(args);
        return fragment;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            tableCode = getArguments().getString(ARG_TABLE_CODE);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentWalkinConfirmBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (tableCode == null || tableCode.trim().isEmpty()) {
            showToast("Không nhận được mã bàn hợp lệ.");
            navigateBack();
            return;
        }

        binding.tvWalkinTable.setText("Bàn " + tableCode);
        binding.tvGuestCount.setText(String.valueOf(guestCount));

        binding.btnMinusGuest.setOnClickListener(v -> {
            if (guestCount > 1) {
                guestCount--;
                binding.tvGuestCount.setText(String.valueOf(guestCount));
            }
        });

        binding.btnPlusGuest.setOnClickListener(v -> {
            if (guestCount < 20) {
                guestCount++;
                binding.tvGuestCount.setText(String.valueOf(guestCount));
            }
        });

        binding.btnStartWalkinSession.setOnClickListener(v -> {
            binding.btnStartWalkinSession.setEnabled(false);
            MobileRepository.getInstance().checkInWalkIn(tableCode, new MobileRepository.Callback<DiningSession>() {
                @Override public void onSuccess(DiningSession value) {
                    if (binding == null) return;
                    showToast("Đã mở bàn thành công! Quý khách có thể bắt đầu gọi món."); navigateTo(DiningSessionFragment.newInstance());
                }
                @Override public void onError(ApiException error) {
                    if (binding != null) { binding.btnStartWalkinSession.setEnabled(true); showToast(apiError(error)); }
                }
            });
        });

        binding.btnCancelWalkin.setOnClickListener(v -> navigateBack());
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
