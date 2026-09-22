package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentResDepositBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.model.ReservationDraft;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

public class ResDepositFragment extends BaseFragment {

    private static final String ARG_DRAFT = "arg_draft";
    private FragmentResDepositBinding binding;
    private ReservationDraft draft;

    public static ResDepositFragment newInstance(ReservationDraft draft) {
        ResDepositFragment f = new ResDepositFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_DRAFT, draft);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            draft = (ReservationDraft) getArguments().getSerializable(ARG_DRAFT);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentResDepositBinding.inflate(inflater, container, false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (draft == null) draft = new ReservationDraft();

        binding.btnBack.setOnClickListener(v -> navigateBack());

        binding.btnConfirmDeposit.setOnClickListener(v -> {
            binding.btnConfirmDeposit.setEnabled(false);
            MobileRepository.getInstance().createReservation(draft, new MobileRepository.Callback<Reservation>() {
                @Override public void onSuccess(Reservation value) {
                    if (binding == null) return;
                    binding.btnConfirmDeposit.setEnabled(true);
                    showToast("Đặt bàn thành công; chi nhánh không yêu cầu đặt cọc.");
                    navigateTo(ResConfirmationFragment.newInstance(value));
                }
                @Override public void onError(ApiException error) {
                    if (binding == null) return;
                    binding.btnConfirmDeposit.setEnabled(true); showToast(apiError(error));
                }
            });
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
