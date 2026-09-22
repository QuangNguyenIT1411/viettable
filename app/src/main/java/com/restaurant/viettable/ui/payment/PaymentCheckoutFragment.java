package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentPaymentCheckoutBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.model.OrderItem;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.DisplayFormatters;

import java.util.ArrayList;
import java.util.List;

public class PaymentCheckoutFragment extends BaseFragment {

    private FragmentPaymentCheckoutBinding binding;

    public static PaymentCheckoutFragment newInstance() {
        return new PaymentCheckoutFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentPaymentCheckoutBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        DiningSession session = appStateManager.getActiveDiningSession();
        List<String> unfinishedNames = new ArrayList<>();
        if (session != null) {
            binding.tvCheckoutTable.setText(session.getTableId() + " - " + session.getBranchName());
            binding.tvCheckoutSessionId.setText("Phiên: #" + session.getId() + " • Bắt đầu: " + DisplayFormatters.dateTime(session.getStartTime()));
            binding.tvCheckoutOrderCount.setText(session.getOrders().size() + " đợt");

            int totalDishes = 0;
            for (Order o : session.getOrders()) {
                for (OrderItem oi : o.getItems()) {
                    totalDishes += oi.getQuantity();
                    if (oi.getStatus() != OrderItem.Status.SERVED) {
                        unfinishedNames.add(oi.getFoodName() + " (" + oi.getStatus().name() + ")");
                    }
                }
            }

            binding.tvCheckoutDishCount.setText(totalDishes + " món");
            binding.tvCheckoutSubtotal.setText(session.getFormattedSubtotal());

            if (!unfinishedNames.isEmpty()) {
                binding.cardUnfinishedWarning.setVisibility(View.VISIBLE);
                StringBuilder sb = new StringBuilder("Còn ");
                sb.append(unfinishedNames.size()).append(" món chưa hoàn thành: ");
                for (int i = 0; i < unfinishedNames.size(); i++) {
                    if (i > 0) sb.append(", ");
                    sb.append(unfinishedNames.get(i));
                }
                binding.tvUnfinishedList.setText(sb.toString());
            } else {
                binding.cardUnfinishedWarning.setVisibility(View.GONE);
            }
        } else {
            binding.btnProceedToInvoice.setEnabled(false);
            binding.cardUnfinishedWarning.setVisibility(View.GONE);
            showToast("Không tìm thấy phiên dùng bữa đang hoạt động.");
        }

        binding.btnProceedToInvoice.setOnClickListener(v -> {
            if (session != null && !unfinishedNames.isEmpty()) {
                com.restaurant.viettable.ui.common.DialogHelper.showConfirmDialog(
                        requireContext(),
                        "Món chưa hoàn thành",
                        getString(com.restaurant.viettable.R.string.payment_uncompleted_warning),
                        getString(com.restaurant.viettable.R.string.payment_btn_force_checkout),
                        (dialog, which) -> {
                            startCheckout();
                        },
                        "Tiếp tục dùng bữa",
                        null
                );
                return;
            }
            startCheckout();
        });
    }

    private void startCheckout() {
        if (binding == null) return;
        binding.btnProceedToInvoice.setEnabled(false);
        MobileRepository.getInstance().beginCheckout(new MobileRepository.Callback<Invoice>() {
            @Override public void onSuccess(Invoice value) { if (binding != null) navigateTo(InvoiceFragment.newInstance()); }
            @Override public void onError(ApiException error) { if (binding != null) { binding.btnProceedToInvoice.setEnabled(true); showToast(apiError(error)); } }
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
