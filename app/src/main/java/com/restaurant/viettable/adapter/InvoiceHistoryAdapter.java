package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.databinding.ItemInvoiceHistoryBinding;
import com.restaurant.viettable.model.InvoiceHistoryItem;
import com.restaurant.viettable.util.DisplayFormatters;

import java.util.List;
import java.util.Locale;

public class InvoiceHistoryAdapter extends RecyclerView.Adapter<InvoiceHistoryAdapter.ViewHolder> {
    public interface OnInvoiceClickListener { void onInvoiceClick(InvoiceHistoryItem invoice); }
    private final List<InvoiceHistoryItem> invoices;
    private final OnInvoiceClickListener listener;

    public InvoiceHistoryAdapter(List<InvoiceHistoryItem> invoices, OnInvoiceClickListener listener) {
        this.invoices = invoices; this.listener = listener;
    }

    @NonNull @Override public ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        return new ViewHolder(ItemInvoiceHistoryBinding.inflate(LayoutInflater.from(parent.getContext()), parent, false));
    }
    @Override public void onBindViewHolder(@NonNull ViewHolder holder, int position) {
        InvoiceHistoryItem invoice = invoices.get(position);
        holder.binding.tvHistoryInvoiceCode.setText(invoice.getInvoiceCode() + " • Phiên " + invoice.getDiningSessionId());
        holder.binding.tvHistoryInvoiceStatus.setText(statusLabel(invoice.getPaymentStatus()));
        holder.binding.tvHistoryInvoiceMeta.setText(DisplayFormatters.dateTime(invoice.getIssuedAtUtc()) + " • "
                + invoice.getBranchName() + " • " + invoice.getTableLabel());
        holder.binding.tvHistoryInvoicePayment.setText("Thanh toán: " + paymentLabel(invoice));
        holder.binding.tvHistoryInvoiceTotal.setText(String.format(Locale.GERMANY, "Tổng cộng  %,dđ", invoice.getTotalAmount()));
        holder.binding.cardInvoiceHistory.setOnClickListener(v -> { if (listener != null) listener.onInvoiceClick(invoice); });
    }
    @Override public int getItemCount() { return invoices == null ? 0 : invoices.size(); }

    private static String statusLabel(String status) {
        if ("SUCCEEDED".equalsIgnoreCase(status)) return "ĐÃ THANH TOÁN";
        return status == null || status.isEmpty() ? "ĐÃ PHÁT HÀNH" : status;
    }
    private static String paymentLabel(InvoiceHistoryItem invoice) {
        String value = (invoice.getPaymentChannel() == null ? "" : invoice.getPaymentChannel()) + "/"
                + (invoice.getPaymentMethod() == null ? "" : invoice.getPaymentMethod());
        if (invoice.getPaymentLocation() != null && !invoice.getPaymentLocation().isEmpty()) value += " • " + invoice.getPaymentLocation();
        return value.equals("/") ? "Không rõ" : value;
    }

    static final class ViewHolder extends RecyclerView.ViewHolder {
        private final ItemInvoiceHistoryBinding binding;
        ViewHolder(ItemInvoiceHistoryBinding binding) { super(binding.getRoot()); this.binding = binding; }
    }
}
