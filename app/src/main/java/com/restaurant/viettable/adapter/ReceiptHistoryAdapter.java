package com.restaurant.viettable.adapter;

import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ProgressBar;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.core.widget.TextViewCompat;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.ItemInvoiceHistoryBinding;
import com.restaurant.viettable.model.Receipt;
import com.restaurant.viettable.ui.common.PdStatusPill;
import com.restaurant.viettable.util.DisplayFormatters;
import com.restaurant.viettable.util.ReceiptText;
import com.restaurant.viettable.util.StatusLabels;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * "Hoá đơn của tôi" list (owner 05/11): invoices grouped under "Tháng 11/2026", newest first, each card with branch,
 * table, date and time, dishes, total, method and status; a spinner row while the next page loads.
 */
public class ReceiptHistoryAdapter extends RecyclerView.Adapter<RecyclerView.ViewHolder> {
    public interface Listener { void onReceiptClick(Receipt receipt); }

    private static final int HEADER = 0, CARD = 1, FOOTER = 2, ACTION = 3;

    private final List<Object> rows = new ArrayList<>();
    private final Listener listener;
    private boolean loadingMore;
    private String actionLabel;
    private Runnable action;

    public ReceiptHistoryAdapter(Listener listener) { this.listener = listener; }

    /** A first row that opens the full "Hoá đơn của tôi" screen (search + filters). */
    public void setAction(String label, Runnable onClick) { actionLabel = label; action = onClick; notifyDataSetChanged(); }

    private int offset() { return action == null ? 0 : 1; }

    /** Replaces the list: month headers are inserted where the month changes. */
    public void submit(List<Receipt> receipts, boolean loadingMore) {
        rows.clear();
        String month = null;
        for (Receipt receipt : receipts) {
            String key = ReceiptText.monthKey(receipt.issuedAtUtc);
            if (!key.equals(month)) { rows.add(ReceiptText.monthTitle(key)); month = key; }
            rows.add(receipt);
        }
        this.loadingMore = loadingMore;
        notifyDataSetChanged();
    }

    @Override public int getItemViewType(int position) {
        if (position < offset()) return ACTION;
        int index = position - offset();
        if (index >= rows.size()) return FOOTER;
        return rows.get(index) instanceof String ? HEADER : CARD;
    }

    @Override public int getItemCount() { return offset() + rows.size() + (loadingMore ? 1 : 0); }

    @NonNull @Override public RecyclerView.ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        if (viewType == CARD)
            return new CardHolder(ItemInvoiceHistoryBinding.inflate(LayoutInflater.from(parent.getContext()), parent, false));
        if (viewType == ACTION) {
            com.google.android.material.button.MaterialButton button = new com.google.android.material.button.MaterialButton(
                    parent.getContext(), null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
            RecyclerView.LayoutParams lp = new RecyclerView.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(parent, 52));
            lp.bottomMargin = dp(parent, 4);
            button.setLayoutParams(lp);
            return new RecyclerView.ViewHolder(button) { };
        }
        if (viewType == FOOTER) {
            ProgressBar spinner = new ProgressBar(parent.getContext());
            spinner.setLayoutParams(new RecyclerView.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(parent, 56)));
            spinner.setContentDescription("Đang tải thêm hoá đơn");
            return new RecyclerView.ViewHolder(spinner) { };
        }
        TextView title = new TextView(parent.getContext());
        TextViewCompat.setTextAppearance(title, R.style.TextAppearance_VietTable_Display_M);
        title.setGravity(Gravity.START);
        title.setAccessibilityHeading(true);
        RecyclerView.LayoutParams lp = new RecyclerView.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.topMargin = dp(parent, 18);
        lp.bottomMargin = dp(parent, 10);
        title.setLayoutParams(lp);
        return new RecyclerView.ViewHolder(title) { };
    }

    @Override public void onBindViewHolder(@NonNull RecyclerView.ViewHolder holder, int position) {
        if (getItemViewType(position) == ACTION) {
            ((TextView) holder.itemView).setText(actionLabel);
            holder.itemView.setOnClickListener(v -> { if (action != null) action.run(); });
            return;
        }
        int index = position - offset();
        if (holder instanceof CardHolder) bind(((CardHolder) holder).binding, (Receipt) rows.get(index));
        else if (holder.itemView instanceof TextView && index < rows.size()) ((TextView) holder.itemView).setText((String) rows.get(index));
    }

    private void bind(ItemInvoiceHistoryBinding b, Receipt r) {
        b.tvHistoryInvoiceCode.setText(StatusLabels.invoiceTitle(r.invoiceId, r.invoiceCode)
                + (r.branchName == null ? "" : " • " + r.branchName));
        String status = ReceiptText.status(r.paymentStatus);
        PdStatusPill.apply(b.tvHistoryInvoiceStatus, status.toUpperCase(Locale.ROOT),
                "Đã thanh toán".equals(status) ? PdStatusPill.Tone.SUCCESS : PdStatusPill.Tone.NEUTRAL);
        String when = DisplayFormatters.dateTime(r.paidAtUtc != null ? r.paidAtUtc : r.issuedAtUtc);
        String meta = ReceiptText.cardMeta(r);
        b.tvHistoryInvoiceMeta.setText(meta.isEmpty() ? when : when + "\n" + meta);
        b.tvHistoryInvoicePayment.setVisibility(View.GONE);
        b.tvHistoryInvoiceTotal.setText("Tổng cộng  " + ReceiptText.money(r.totalAmount));
        b.cardInvoiceHistory.setContentDescription(StatusLabels.invoiceTitle(r.invoiceId, r.invoiceCode) + ", " + when + ", "
                + ReceiptText.money(r.totalAmount));
        b.cardInvoiceHistory.setOnClickListener(v -> { if (listener != null) listener.onReceiptClick(r); });
    }

    private static int dp(View view, int value) {
        return Math.round(value * view.getResources().getDisplayMetrics().density);
    }

    static final class CardHolder extends RecyclerView.ViewHolder {
        final ItemInvoiceHistoryBinding binding;
        CardHolder(ItemInvoiceHistoryBinding binding) { super(binding.getRoot()); this.binding = binding; }
    }
}
