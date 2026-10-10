package com.restaurant.viettable.ui.session;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentCheckinConfirmBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.DisplayFormatters;

public class CheckinConfirmFragment extends BaseFragment {

    private static final String ARG_TABLE_ID = "arg_table_id";
    private FragmentCheckinConfirmBinding binding;
    private String tableId;

    public static CheckinConfirmFragment newInstance(String tableId) {
        CheckinConfirmFragment fragment = new CheckinConfirmFragment();
        Bundle args = new Bundle();
        args.putString(ARG_TABLE_ID, tableId);
        fragment.setArguments(args);
        return fragment;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            tableId = getArguments().getString(ARG_TABLE_ID);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentCheckinConfirmBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (tableId == null || tableId.trim().isEmpty()) {
            showToast("Không nhận được mã bàn hợp lệ.");
            navigateBack();
            return;
        }

        Reservation res = null;
        for (Reservation r : appStateManager.getReservations()) {
            if (r.getStatus() == Reservation.Status.CONFIRMED) {
                res = r;
                break;
            }
        }

        if (res != null) {
            binding.tvTableTitle.setText(res.getTableLabel());
            binding.tvTableSubtitle.setText(res.getAreaName() + " • " + res.getGuestCount() + " người • " + res.getBranchName());
            binding.tvCheckinResCode.setText("#" + res.getId());
            binding.tvCheckinTime.setText(res.getTimeSlot() + " - " + DisplayFormatters.date(res.getDate()));
            binding.tvCheckinCustomer.setText(res.getContactName() + " (" + res.getContactPhone() + ")");

            if (res.hasPreOrder() || !appStateManager.getCartItems().isEmpty()) {
                binding.cardPreorderNotice.setVisibility(View.VISIBLE);
                binding.tvPreorderInfo.setText(appStateManager.getCartItems().size() + " món ăn đặt trước sẽ tự động được chuyển cho Bếp chuẩn bị.");
            } else {
                binding.cardPreorderNotice.setVisibility(View.GONE);
            }
        }

        final Reservation finalRes = res;
        binding.btnConfirmCheckin.setOnClickListener(v -> {
            if (finalRes == null) { showToast("Không tìm thấy đặt bàn phù hợp."); return; }
            binding.btnConfirmCheckin.setEnabled(false);
            MobileRepository.getInstance().checkInReservation(finalRes, tableId, false, new MobileRepository.Callback<DiningSession>() {
                @Override public void onSuccess(DiningSession value) {
                    if (binding == null) return; finalRes.setStatus(Reservation.Status.CHECKED_IN);
                    showToast("Check-in thành công! Bữa ăn đã bắt đầu."); navigateTo(DiningSessionFragment.newInstance());
                }
                @Override public void onError(ApiException error) {
                    if (binding != null) { binding.btnConfirmCheckin.setEnabled(true); showToast(apiError(error)); }
                }
            });
        });

        binding.btnCancelCheckin.setOnClickListener(v -> navigateBack());
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
