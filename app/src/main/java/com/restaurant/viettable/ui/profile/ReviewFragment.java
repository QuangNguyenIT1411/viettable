package com.restaurant.viettable.ui.profile;

import android.os.Bundle;
import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.core.content.ContextCompat;
import androidx.core.widget.TextViewCompat;

import com.google.android.material.chip.Chip;
import com.restaurant.viettable.R;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentReviewBinding;
import com.restaurant.viettable.model.Receipt;
import com.restaurant.viettable.model.Review;
import com.restaurant.viettable.model.ReviewDraft;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.common.PdStatusPill;
import com.restaurant.viettable.util.DisplayFormatters;
import com.restaurant.viettable.model.ReviewInputPolicy;
import com.restaurant.viettable.util.SafeText;
import com.restaurant.viettable.util.StatusLabels;

import java.util.List;

/**
 * "Đánh giá bữa ăn" (owner 05/11): one review per paid invoice ("Đã dùng bữa"), editable for 24 hours. The guest
 * picks overall stars and each criterion (Món ăn, Phục vụ, Không gian) separately, may star the dishes of that
 * invoice, choose quick tags (the server's list), write a comment and hide their name. The restaurant's reply and a
 * hidden review's reason are shown as the server sends them.
 */
public class ReviewFragment extends BaseFragment {
    private static final String ARG_INVOICE_ID = "invoice_id";
    private static final String ARG_REVIEW = "review";
    private static final String[] CRITERIA = {"Món ăn", "Phục vụ", "Không gian"};

    private FragmentReviewBinding binding;
    private int invoiceId;
    private Review editingReview;
    private ReviewDraft draft = new ReviewDraft();
    private Receipt receipt;
    private boolean readOnly;
    private boolean submitting;

    public static ReviewFragment newInstanceForInvoice(int invoiceId) {
        ReviewFragment fragment = new ReviewFragment(); Bundle args = new Bundle();
        args.putInt(ARG_INVOICE_ID, invoiceId); fragment.setArguments(args); return fragment;
    }

    public static ReviewFragment newInstanceForEdit(Review review) {
        ReviewFragment fragment = new ReviewFragment(); Bundle args = new Bundle();
        args.putSerializable(ARG_REVIEW, review); fragment.setArguments(args); return fragment;
    }

    @Override public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            invoiceId = getArguments().getInt(ARG_INVOICE_ID, 0);
            editingReview = (Review) getArguments().getSerializable(ARG_REVIEW);
        }
        if (editingReview != null) invoiceId = editingReview.getInvoiceId();
    }

    @Nullable @Override public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentReviewBinding.inflate(inflater, container, false); setBottomNavVisible(false); return binding.getRoot();
    }

    @Override public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.tvReviewInvoiceMeta.setText(invoiceId > 0 ? StatusLabels.invoiceTitle(invoiceId, null) : "Không tìm thấy hoá đơn");
        binding.btnSubmitReview.setOnClickListener(v -> submit());
        if (invoiceId <= 0) { binding.progressReview.setVisibility(View.GONE); return; }
        // The invoice (dishes, branch) and the guest's reviews (this invoice's review + the quick tags) from the server.
        MobileRepository.getInstance().loadReceipt(invoiceId, new MobileRepository.Callback<Receipt>() {
            @Override public void onSuccess(Receipt value) { receipt = value; loadReviews(); }
            @Override public void onError(ApiException error) { receipt = null; loadReviews(); }
        });
    }

    private void loadReviews() {
        MobileRepository.getInstance().loadReviews(new MobileRepository.Callback<List<Review>>() {
            @Override public void onSuccess(List<Review> reviews) {
                if (binding == null) return;
                for (Review value : reviews) if (value.getInvoiceId() == invoiceId) editingReview = value;
                draft = editingReview == null ? new ReviewDraft() : ReviewDraft.from(editingReview);
                readOnly = editingReview != null && !editingReview.canEdit();
                render(MobileRepository.getInstance().reviewTags());
            }
            @Override public void onError(ApiException error) {
                if (binding == null) return;
                binding.progressReview.setVisibility(View.GONE);
                binding.contentReview.setVisibility(View.VISIBLE);
                binding.tvReviewError.setVisibility(View.VISIBLE);
                binding.tvReviewError.setText(apiError(error));
            }
        });
    }

    private void render(List<String> tags) {
        binding.progressReview.setVisibility(View.GONE);
        binding.contentReview.setVisibility(View.VISIBLE);
        if (receipt != null && receipt.branchName != null)
            binding.tvReviewInvoiceMeta.setText(StatusLabels.invoiceTitle(invoiceId, null) + " · " + receipt.branchName);
        binding.tvReviewTitle.setText(editingReview == null ? "Bữa ăn thế nào?" : readOnly ? "Đánh giá của bạn" : "Sửa đánh giá");
        binding.tvDinedBadge.setVisibility(View.VISIBLE);
        PdStatusPill.apply(binding.tvDinedBadge, "ĐÃ DÙNG BỮA", PdStatusPill.Tone.SUCCESS);
        if (editingReview != null) {
            binding.tvReviewLockNote.setVisibility(View.VISIBLE);
            binding.tvReviewLockNote.setText(readOnly
                    ? "Đã quá 24 giờ kể từ khi gửi nên không sửa được nữa."
                    : "Bạn sửa được tới " + DisplayFormatters.dateTime(editingReview.getEditableUntilUtc()) + ".");
            if (editingReview.isHidden()) {
                binding.tvReviewHidden.setVisibility(View.VISIBLE);
                binding.tvReviewHidden.setText("Đánh giá này đã bị ẩn khỏi trang công khai"
                        + (editingReview.getHiddenReason() == null ? "." : ": " + editingReview.getHiddenReason()));
            }
            if (editingReview.getReplyText() != null) {
                binding.layoutReviewReply.setVisibility(View.VISIBLE);
                binding.tvReviewReply.setText(editingReview.getReplyText());
            }
        }

        binding.layoutOverallStars.removeAllViews();
        stars(binding.layoutOverallStars, draft.overall, 48, value -> { draft.overall = value; render(tags); });
        binding.tvRatingPrompt.setText(prompt(draft.overall));

        binding.layoutCriteria.removeAllViews();
        int[] values = {draft.food, draft.service, draft.space};
        for (int i = 0; i < CRITERIA.length; i++) {
            final int index = i;
            criterion(binding.layoutCriteria, CRITERIA[i], values[i], value -> {
                if (index == 0) draft.food = value; else if (index == 1) draft.service = value; else draft.space = value;
                render(tags);
            });
        }

        binding.chipGroupReviewTags.removeAllViews();
        binding.tvTagsHeading.setVisibility(tags.isEmpty() ? View.GONE : View.VISIBLE);
        for (String tag : tags) {
            Chip chip = new Chip(requireContext(), null, com.google.android.material.R.attr.chipStyle);
            chip.setText(tag);
            chip.setCheckable(true);
            chip.setChecked(draft.tags.contains(tag));
            chip.setEnabled(!readOnly);
            chip.setOnClickListener(v -> { if (draft.tags.contains(tag)) draft.tags.remove(tag); else draft.tags.add(tag); });
            binding.chipGroupReviewTags.addView(chip);
        }

        binding.layoutDishRatings.removeAllViews();
        boolean hasDishes = receipt != null && !receipt.lines.isEmpty();
        binding.tvDishHeading.setVisibility(hasDishes ? View.VISIBLE : View.GONE);
        binding.layoutDishRatings.setVisibility(hasDishes ? View.VISIBLE : View.GONE);
        if (hasDishes) {
            java.util.Set<Integer> seen = new java.util.HashSet<>();
            for (Receipt.Line line : receipt.lines) {
                if (line.dishId <= 0 || !seen.add(line.dishId)) continue;
                Integer value = draft.dishRatings.get(line.dishId);
                criterion(binding.layoutDishRatings, line.name, value == null ? 0 : value, stars -> {
                    Integer old = draft.dishRatings.get(line.dishId);
                    if (old != null && old == stars) draft.dishRatings.remove(line.dishId); // tap again = clear (optional)
                    else draft.dishRatings.put(line.dishId, stars);
                    render(tags);
                });
            }
        }

        if (binding.etReviewComment.getText() == null || binding.etReviewComment.getText().length() == 0)
            binding.etReviewComment.setText(SafeText.clean(draft.comment));
        binding.etReviewComment.setEnabled(!readOnly);
        binding.switchReviewAnonymous.setChecked(draft.anonymous);
        binding.switchReviewAnonymous.setEnabled(!readOnly);
        binding.switchReviewAnonymous.setOnCheckedChangeListener((b, checked) -> draft.anonymous = checked);

        binding.btnSubmitReview.setVisibility(readOnly ? View.GONE : View.VISIBLE);
        binding.btnSubmitReview.setText(editingReview == null ? "Gửi đánh giá" : "Lưu thay đổi");
        binding.btnSubmitReview.setEnabled(!submitting && draft.complete());
        binding.tvReviewError.setVisibility(View.GONE);
    }

    private interface Pick { void on(int stars); }

    private void criterion(LinearLayout parent, String label, int value, Pick pick) {
        LinearLayout row = new LinearLayout(requireContext());
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        TextView name = new TextView(requireContext());
        TextViewCompat.setTextAppearance(name, R.style.TextAppearance_VietTable_Body_Strong);
        name.setText(label);
        row.addView(name, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        LinearLayout stars = new LinearLayout(requireContext());
        stars(stars, value, 40, pick);
        row.addView(stars);
        parent.addView(row);
    }

    private void stars(LinearLayout parent, int value, int sizeDp, Pick pick) {
        int size = Math.round(sizeDp * getResources().getDisplayMetrics().density);
        for (int i = 1; i <= 5; i++) {
            final int stars = i;
            ImageView star = new ImageView(requireContext());
            star.setImageResource(R.drawable.ic_star);
            star.setPadding(size / 7, size / 7, size / 7, size / 7);
            star.setColorFilter(ContextCompat.getColor(requireContext(), i <= value ? R.color.pd_yellow : R.color.pd_line));
            star.setContentDescription(getString(R.string.a11y_rating_value, i));
            star.setSelected(i == value);
            if (!readOnly) { star.setOnClickListener(v -> pick.on(stars)); star.setClickable(true); star.setFocusable(true); }
            parent.addView(star, new LinearLayout.LayoutParams(size, size));
        }
    }

    private static String prompt(int stars) {
        switch (stars) {
            case 5: return "Tuyệt vời";
            case 4: return "Rất hài lòng";
            case 3: return "Bình thường";
            case 2: return "Chưa hài lòng";
            case 1: return "Tệ";
            default: return "Chạm để chấm sao";
        }
    }

    private void submit() {
        if (submitting || readOnly || invoiceId <= 0) return;
        String comment = ReviewInputPolicy.normalizeComment(binding.etReviewComment.getText() == null ? null : binding.etReviewComment.getText().toString());
        if (!ReviewInputPolicy.isCommentValid(comment)) { binding.tilReviewComment.setError("Nội dung tối đa 2.000 ký tự."); return; }
        String missing = draft.missing();
        if (missing != null) { binding.tvReviewError.setVisibility(View.VISIBLE); binding.tvReviewError.setText(missing); return; }
        binding.tilReviewComment.setError(null);
        draft.comment = comment;
        draft.anonymous = binding.switchReviewAnonymous.isChecked();
        submitting = true;
        binding.btnSubmitReview.setEnabled(false);
        binding.btnSubmitReview.setText("Đang gửi…");
        MobileRepository.Callback<Review> callback = new MobileRepository.Callback<Review>() {
            @Override public void onSuccess(Review value) {
                if (binding == null) return;
                submitting = false;
                showToast(editingReview == null ? "Đã gửi đánh giá. Cảm ơn bạn!" : "Đã cập nhật đánh giá.");
                navigateBack();
            }
            @Override public void onError(ApiException error) {
                submitting = false;
                if (binding == null) return;
                binding.btnSubmitReview.setEnabled(true);
                binding.btnSubmitReview.setText(editingReview == null ? "Gửi đánh giá" : "Lưu thay đổi");
                binding.tvReviewError.setVisibility(View.VISIBLE);
                binding.tvReviewError.setText(apiError(error));
            }
        };
        if (editingReview == null) MobileRepository.getInstance().createReview(invoiceId, draft, callback);
        else MobileRepository.getInstance().updateReview(editingReview, draft, callback);
    }

    @Override public void onDestroyView() { super.onDestroyView(); binding = null; }
}
