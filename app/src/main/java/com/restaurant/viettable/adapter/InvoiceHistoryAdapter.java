package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.databinding.ItemInvoiceHistoryBinding;
import com.restaurant.viettable.ui.common.PdStatusPill;
import com.restaurant.viettable.model.InvoiceHistoryItem;
import com.restaurant.viettable.util.DisplayFormatters;
import com.restaurant.viettable.util.StatusLabels;

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
        holder.binding.tvHistoryInvoiceCode.setText(StatusLabels.invoiceTitle(invoice.getInvoiceId(), invoice.getInvoiceCode())
                + " • " + StatusLabels.sessionLabel(invoice.getDiningSessionId()));
        PdStatusPill.apply(holder.binding.tvHistoryInvoiceStatus, statusLabel(invoice.getPaymentStatus()),
                statusTone(invoice.getPaymentStatus()));
        holder.binding.tvHistoryInvoiceMeta.setText(DisplayFormatters.dateTime(invoice.getIssuedAtUtc()) + " • "
                + invoice.getBranchName() + " • " + invoice.getTableLabel());
        holder.binding.tvHistoryInvoicePayment.setText("Thanh toán: " + paymentLabel(invoice));
        holder.binding.tvHistoryInvoiceTotal.setText(String.format(Locale.GERMANY, "Tổng cộng  %,dđ", invoice.getTotalAmount()));
        holder.binding.cardInvoiceHistory.setOnClickListener(v -> { if (listener != null) listener.onInvoiceClick(invoice); });
    }
    @Override public int getItemCount() { return invoices == null ? 0 : invoices.size(); }

    /** Paid = mint, waiting = yellow, failed / cancelled = red, anything else neutral (section 5). */
    private static PdStatusPill.Tone statusTone(String status) {
        if (status == null || status.isEmpty() || "SUCCEEDED".equalsIgnoreCase(status) || "PAID".equalsIgnoreCase(status))
            return PdStatusPill.Tone.SUCCESS;
        String s = status.toUpperCase(Locale.ROOT);
        if (s.contains("FAIL") || s.contains("CANCEL") || s.contains("EXPIRE")) return PdStatusPill.Tone.ERROR;
        if (s.contains("PENDING") || s.contains("PROCESS")) return PdStatusPill.Tone.WAITING;
        return PdStatusPill.Tone.NEUTRAL;
    }

    private static String statusLabel(String status) {
        if ("SUCCEEDED".equalsIgnoreCase(status)) return "ĐÃ THANH TOÁN";
        return status == null || status.isEmpty() ? "ĐÃ PHÁT HÀNH" : StatusLabels.paymentStatus(status).toUpperCase(Locale.ROOT);
    }
    private static String paymentLabel(InvoiceHistoryItem invoice) {
        String channel = StatusLabels.paymentChannel(invoice.getPaymentChannel());
        String method = StatusLabels.paymentMethod(invoice.getPaymentMethod());
        String value = channel.isEmpty() ? method : (method.isEmpty() ? channel : channel + " • " + method);
        String location = StatusLabels.paymentLocation(invoice.getPaymentLocation());
        if (!location.isEmpty()) value += " • " + location;
        return value.isEmpty() ? "Không rõ" : value;
    }

    static final class ViewHolder extends RecyclerView.ViewHolder {
        private final ItemInvoiceHistoryBinding binding;
        ViewHolder(ItemInvoiceHistoryBinding binding) { super(binding.getRoot()); this.binding = binding; }
    }
}
