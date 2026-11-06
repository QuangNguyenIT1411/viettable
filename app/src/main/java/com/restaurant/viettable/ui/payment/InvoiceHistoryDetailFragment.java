package com.restaurant.viettable.ui.payment;

import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.core.content.ContextCompat;
import androidx.core.widget.TextViewCompat;

import com.restaurant.viettable.R;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentInvoiceHistoryDetailBinding;
import com.restaurant.viettable.model.Receipt;
import com.restaurant.viettable.model.Review;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.common.PdStatusPill;
import com.restaurant.viettable.ui.profile.ReviewFragment;
import com.restaurant.viettable.util.ReceiptPdf;
import com.restaurant.viettable.util.ReceiptText;
import com.restaurant.viettable.util.StatusLabels;

import java.io.File;
import java.util.List;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * "Chi tiết hoá đơn" (owner 05/11): the server's snapshot of one paid bill, the "PHIẾU THANH TOÁN" PDF from it
 * (saved to Downloads, then the share sheet), and "Đánh giá" / "Xem đánh giá" (one review per invoice).
 */
public class InvoiceHistoryDetailFragment extends BaseFragment {
    private static final String ARG_INVOICE_ID = "invoice_id";
    private static final ExecutorService PDF = Executors.newSingleThreadExecutor();

    private FragmentInvoiceHistoryDetailBinding binding;
    private int invoiceId;
    private Receipt receipt;
    private Review review;
    private boolean reviewsKnown;

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
        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnRetryInvoiceDetail.setOnClickListener(v -> load());
        binding.btnExportInvoice.setOnClickListener(v -> exportPdf());
        binding.btnNeedHelp.setOnClickListener(v -> navigateTo(com.restaurant.viettable.ui.profile.SupportStartFragment.forContext("BILL_WRONG", invoiceId, 0)));
        binding.btnInvoiceReview.setOnClickListener(v -> openReview());
        load();
    }

    @Override public void onResume() {
        super.onResume();
        if (receipt != null) loadReview(); // back from "Đánh giá": the button follows the saved review
    }

    private void load() {
        if (binding == null) return;
        binding.layoutInvoiceDetailState.setVisibility(View.VISIBLE);
        binding.progressInvoiceDetail.setVisibility(View.VISIBLE);
        binding.tvInvoiceDetailState.setVisibility(View.GONE);
        binding.btnRetryInvoiceDetail.setVisibility(View.GONE);
        binding.contentInvoiceDetail.setVisibility(View.GONE);
        binding.layoutInvoiceActions.setVisibility(View.GONE);
        MobileRepository.getInstance().loadReceipt(invoiceId, new MobileRepository.Callback<Receipt>() {
            @Override public void onSuccess(Receipt value) {
                if (binding == null) return;
                receipt = value;
                render(value);
                loadReview();
            }
            @Override public void onError(ApiException error) {
                if (binding == null) return;
                binding.progressInvoiceDetail.setVisibility(View.GONE);
                binding.tvInvoiceDetailState.setVisibility(View.VISIBLE);
                binding.tvInvoiceDetailState.setText(apiError(error));
                binding.btnRetryInvoiceDetail.setVisibility(View.VISIBLE);
            }
        });
    }

    private void render(Receipt r) {
        binding.layoutInvoiceDetailState.setVisibility(View.GONE);
        binding.contentInvoiceDetail.setVisibility(View.VISIBLE);
        binding.layoutInvoiceActions.setVisibility(View.VISIBLE);
        binding.tvInvoiceDetailCode.setText(StatusLabels.invoiceTitle(r.invoiceId, r.invoiceCode));
        binding.tvInvoiceDetailBranch.setText(r.branchName == null ? "Hoá đơn" : r.branchName);
        binding.tvInvoiceDetailAddress.setVisibility(r.branchAddress == null ? View.GONE : View.VISIBLE);
        binding.tvInvoiceDetailAddress.setText(r.branchAddress == null ? "" : r.branchAddress);
        String status = ReceiptText.status(r.paymentStatus);
        PdStatusPill.apply(binding.tvInvoiceDetailStatus, status.toUpperCase(Locale.ROOT),
                "Đã thanh toán".equals(status) ? PdStatusPill.Tone.SUCCESS : PdStatusPill.Tone.NEUTRAL);

        binding.layoutInvoiceInfo.removeAllViews();
        if (r.tableName != null) row(binding.layoutInvoiceInfo, "Bàn", r.tableName, ReceiptText.Kind.NORMAL);
        String time = ReceiptText.timeRange(r.sessionStartedAtUtc, r.paidAtUtc != null ? r.paidAtUtc : r.issuedAtUtc);
        if (!time.isEmpty()) row(binding.layoutInvoiceInfo, ReceiptText.timeLabel(r.sessionStartedAtUtc), time, ReceiptText.Kind.NORMAL);
        if (r.cashierName != null) row(binding.layoutInvoiceInfo, "Thu ngân", r.cashierName, ReceiptText.Kind.NORMAL);
        binding.layoutInvoiceInfo.setVisibility(binding.layoutInvoiceInfo.getChildCount() == 0 ? View.GONE : View.VISIBLE);

        binding.layoutInvoiceDetailItems.removeAllViews();
        if (r.lines.isEmpty()) {
            note(binding.layoutInvoiceDetailItems, "Hoá đơn này không lưu danh sách món.");
        } else {
            for (Receipt.Line line : r.lines)
                row(binding.layoutInvoiceDetailItems, line.quantity + " × " + line.name + "\n" + ReceiptText.money(line.unitPrice) + " / phần",
                        ReceiptText.money(line.lineTotal), ReceiptText.Kind.NORMAL);
        }

        binding.layoutInvoiceAmounts.removeAllViews();
        for (ReceiptText.Row row : ReceiptText.amountRows(r)) row(binding.layoutInvoiceAmounts, row.label, row.value, row.kind);

        binding.layoutInvoicePayment.removeAllViews();
        List<ReceiptText.Row> payment = ReceiptText.paymentRows(r);
        for (ReceiptText.Row row : payment) row(binding.layoutInvoicePayment, row.label, row.value, row.kind);
        binding.layoutInvoicePayment.setVisibility(payment.isEmpty() ? View.GONE : View.VISIBLE);
    }

    /** "Đánh giá" when the invoice has none yet, "Xem đánh giá" when it has; hidden until the server answers. */
    private void loadReview() {
        MobileRepository.getInstance().loadReviews(new MobileRepository.Callback<List<Review>>() {
            @Override public void onSuccess(List<Review> reviews) {
                if (binding == null) return;
                review = null;
                for (Review value : reviews) if (value.getInvoiceId() == invoiceId) review = value;
                reviewsKnown = true;
                binding.btnInvoiceReview.setVisibility(View.VISIBLE);
                binding.btnInvoiceReview.setText(review == null ? "Đánh giá bữa ăn" : "Xem đánh giá");
                if (review != null && review.getReplyText() != null) {
                    binding.tvInvoiceReviewNote.setVisibility(View.VISIBLE);
                    binding.tvInvoiceReviewNote.setText("Nhà hàng đã trả lời đánh giá của bạn.");
                } else binding.tvInvoiceReviewNote.setVisibility(View.GONE);
            }
            @Override public void onError(ApiException error) {
                if (binding == null) return;
                reviewsKnown = false;
                binding.btnInvoiceReview.setVisibility(View.GONE);
            }
        });
    }

    private void openReview() {
        if (!reviewsKnown || receipt == null) return;
        navigateTo(review == null ? ReviewFragment.newInstanceForInvoice(receipt.invoiceId) : ReviewFragment.newInstanceForEdit(review));
    }

    /** PDF from the server's snapshot (not from the screen), saved to Downloads, then the share sheet. */
    private void exportPdf() {
        if (receipt == null) return;
        Receipt r = receipt;
        binding.btnExportInvoice.setEnabled(false);
        android.content.Context app = requireContext().getApplicationContext();
        PDF.execute(() -> {
            String error = null;
            Uri share = null;
            boolean saved = false;
            try {
                File pdf = ReceiptPdf.render(app, r);
                try { saved = ReceiptPdf.saveToDownloads(app, pdf) != null; } catch (Exception ignored) { }
                share = ReceiptPdf.shareUri(app, pdf);
            } catch (Exception e) {
                error = "Không tạo được phiếu thanh toán. Thử lại sau.";
            }
            final String failure = error;
            final Uri uri = share;
            final boolean inDownloads = saved;
            if (getActivity() == null) return;
            getActivity().runOnUiThread(() -> {
                if (binding == null) return;
                binding.btnExportInvoice.setEnabled(true);
                if (failure != null) { showToast(failure); return; }
                showToast(inDownloads ? "Đã lưu phiếu thanh toán vào Tải xuống/VietTable." : "Đã tạo phiếu thanh toán.");
                Intent send = new Intent(Intent.ACTION_SEND).setType("application/pdf")
                        .putExtra(Intent.EXTRA_STREAM, uri)
                        .putExtra(Intent.EXTRA_SUBJECT, ReceiptText.TITLE + " " + StatusLabels.invoiceTitle(r.invoiceId, r.invoiceCode))
                        .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
                startActivity(Intent.createChooser(send, "Gửi phiếu thanh toán"));
            });
        });
    }

    private void row(LinearLayout parent, String label, String value, ReceiptText.Kind kind) {
        LinearLayout line = new LinearLayout(requireContext());
        line.setOrientation(LinearLayout.HORIZONTAL);
        line.setGravity(Gravity.CENTER_VERTICAL);
        int pad = Math.round(7 * getResources().getDisplayMetrics().density);
        line.setPadding(0, pad, 0, pad);
        TextView left = new TextView(requireContext());
        TextView right = new TextView(requireContext());
        int style = kind == ReceiptText.Kind.TOTAL ? R.style.TextAppearance_VietTable_Body_Strong : R.style.TextAppearance_VietTable_Body;
        TextViewCompat.setTextAppearance(left, style);
        TextViewCompat.setTextAppearance(right, kind == ReceiptText.Kind.TOTAL ? R.style.TextAppearance_VietTable_Price : style);
        left.setText(label);
        right.setText(value);
        left.setTextColor(ContextCompat.getColor(requireContext(), kind == ReceiptText.Kind.TOTAL ? R.color.pd_text : R.color.pd_muted));
        int valueColor = kind == ReceiptText.Kind.DISCOUNT ? R.color.pd_mint_text
                : kind == ReceiptText.Kind.TOTAL ? R.color.pd_yellow_text : R.color.pd_text;
        right.setTextColor(ContextCompat.getColor(requireContext(), valueColor));
        right.setGravity(Gravity.END);
        line.addView(left, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.setMarginStart(pad * 2);
        line.addView(right, lp);
        parent.addView(line);
    }

    private void note(LinearLayout parent, String text) {
        TextView view = new TextView(requireContext());
        TextViewCompat.setTextAppearance(view, R.style.TextAppearance_VietTable_Caption);
        view.setTextColor(ContextCompat.getColor(requireContext(), R.color.pd_muted));
        view.setText(text);
        parent.addView(view);
    }

    @Override public void onDestroyView() { super.onDestroyView(); binding = null; }
}
