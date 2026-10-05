package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentInvoiceHistoryDetailBinding;
import com.restaurant.viettable.model.InvoiceHistoryItem;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.DisplayFormatters;

import java.util.Locale;

public class InvoiceHistoryDetailFragment extends BaseFragment {
    private static final String ARG_INVOICE_ID = "invoice_id";
    private FragmentInvoiceHistoryDetailBinding binding;
    private int invoiceId;

    public static InvoiceHistoryDetailFragment newInstance(int invoiceId) {
        InvoiceHistoryDetailFragment fragment = new InvoiceHistoryDetailFragment();
        Bundle args = new Bundle(); args.putInt(ARG_INVOICE_ID, invoiceId); fragment.setArguments(args);
        return fragment;
    }

    @Nullable @Override public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container,
                                                  @Nullable Bundle savedInstanceState) {
        binding = FragmentInvoiceHistoryDetailBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        invoiceId = getArguments() == null ? 0 : getArguments().getInt(ARG_INVOICE_ID);
        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());
        binding.btnRetryInvoiceDetail.setOnClickListener(v -> load());
        load();
    }

    private void load() {
        if (binding == null) return;
        binding.progressInvoiceDetail.setVisibility(View.VISIBLE);
        binding.tvInvoiceDetailState.setVisibility(View.GONE);
        binding.btnRetryInvoiceDetail.setVisibility(View.GONE);
        binding.contentInvoiceDetail.setVisibility(View.GONE);
        MobileRepository.getInstance().loadInvoiceHistoryItem(invoiceId, new MobileRepository.Callback<InvoiceHistoryItem>() {
            @Override public void onSuccess(InvoiceHistoryItem invoice) { if (binding != null) render(invoice); }
            @Override public void onError(ApiException error) {
                if (binding == null) return;
                binding.progressInvoiceDetail.setVisibility(View.GONE);
                binding.tvInvoiceDetailState.setVisibility(View.VISIBLE);
                binding.tvInvoiceDetailState.setText(apiError(error));
                binding.btnRetryInvoiceDetail.setVisibility(View.VISIBLE);
            }
        });
    }

    private void render(InvoiceHistoryItem invoice) {
        binding.progressInvoiceDetail.setVisibility(View.GONE);
        binding.contentInvoiceDetail.setVisibility(View.VISIBLE);
        binding.tvInvoiceDetailCode.setText(invoice.getInvoiceCode());
        String paymentStatus = invoice.getPaymentStatus() == null ? "" : invoice.getPaymentStatus();
        String paymentMethod = (invoice.getPaymentChannel() == null ? "" : invoice.getPaymentChannel()) + "/"
                + (invoice.getPaymentMethod() == null ? "" : invoice.getPaymentMethod());
        binding.tvInvoiceDetailMeta.setText("Thời gian: " + DisplayFormatters.dateTime(invoice.getIssuedAtUtc())
                + "\nChi nhánh: " + invoice.getBranchName() + "\n" + invoice.getTableId()
                + " • Phiên " + invoice.getDiningSessionId() + " (" + invoice.getDiningSessionCode() + ")"
                + "\nThanh toán: " + paymentMethod + " • " + paymentStatus
                + "\nMã giao dịch: " + invoice.getPaymentCode());
        binding.layoutInvoiceDetailItems.removeAllViews();
        for (InvoiceHistoryItem.Line item : invoice.getItems()) {
            TextView row = new TextView(requireContext());
            row.setText(item.getQuantity() + "x " + item.getName() + "     "
                    + String.format(Locale.GERMANY, "%,dđ", item.getTotal()));
            row.setTextColor(requireContext().getColor(R.color.text_secondary));
            row.setTextSize(14);
            row.setPadding(0, 7, 0, 7);
            binding.layoutInvoiceDetailItems.addView(row);
        }
        if (invoice.getItems().isEmpty()) {
            TextView row = new TextView(requireContext());
            row.setText("Không có chi tiết món trong dữ liệu hóa đơn.");
            row.setTextColor(requireContext().getColor(R.color.text_secondary));
            binding.layoutInvoiceDetailItems.addView(row);
        }
        binding.tvInvoiceDetailTotal.setText(String.format(Locale.GERMANY, "Tổng cộng  %,dđ", invoice.getTotalAmount()));
    }

    @Override public void onDestroyView() { binding = null; super.onDestroyView(); }
}
