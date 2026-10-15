package com.restaurant.viettable.ui.profile;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ImageView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.core.content.ContextCompat;

import com.restaurant.viettable.R;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentReviewBinding;
import com.restaurant.viettable.model.Review;
import com.restaurant.viettable.model.ReviewInputPolicy;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.SafeText;

public final class ReviewFragment extends BaseFragment {
    private static final String ARG_INVOICE_ID = "invoice_id";
    private static final String ARG_REVIEW = "review";

    private FragmentReviewBinding binding;
    private int invoiceId;
    private Review editingReview;
    private int selectedRating = 5;
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
        if (editingReview != null) { invoiceId = editingReview.getInvoiceId(); selectedRating = editingReview.getOverallRating(); }
    }

    @Nullable @Override public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentReviewBinding.inflate(inflater, container, false); setBottomNavVisible(false); return binding.getRoot();
    }

    @Override public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());
        binding.toolbar.setTitle(editingReview == null ? "Đánh giá bữa ăn" : "Sửa đánh giá");
        binding.tvReviewInvoiceMeta.setText(invoiceId > 0 ? "Hóa đơn #" + invoiceId : "Không tìm thấy hóa đơn hợp lệ");
        binding.btnSubmitReview.setText(editingReview == null ? "Gửi đánh giá" : "Lưu thay đổi");
        if (editingReview != null && !SafeText.isBlank(editingReview.getComment())) binding.etReviewComment.setText(SafeText.clean(editingReview.getComment()));
        setupStars(); setRating(selectedRating, stars());
        if (invoiceId <= 0) binding.btnSubmitReview.setEnabled(false);
        binding.btnSubmitReview.setOnClickListener(v -> submit());
    }

    private void submit() {
        if (submitting || invoiceId <= 0 || !ReviewInputPolicy.isRatingValid(selectedRating)) return;
        String comment = ReviewInputPolicy.normalizeComment(binding.etReviewComment.getText() == null ? null : binding.etReviewComment.getText().toString());
        if (!ReviewInputPolicy.isCommentValid(comment)) { binding.tilReviewComment.setError("Nội dung tối đa 2.000 ký tự."); return; }
        binding.tilReviewComment.setError(null); submitting = true; binding.btnSubmitReview.setEnabled(false);
        binding.btnSubmitReview.setText("Đang gửi…");
        MobileRepository.Callback<Review> callback = new MobileRepository.Callback<Review>() {
            @Override public void onSuccess(Review value) {
                if (binding == null) return;
                submitting = false; showToast(editingReview == null ? "Đã gửi đánh giá." : "Đã cập nhật đánh giá.");
                if (editingReview == null) navigateTo(ReviewHistoryFragment.newInstance()); else navigateBack();
            }
            @Override public void onError(ApiException error) {
                submitting = false;
                if (binding != null) {
                    binding.btnSubmitReview.setEnabled(true);
                    binding.btnSubmitReview.setText(editingReview == null ? "Gửi đánh giá" : "Lưu thay đổi");
                    showToast(apiError(error));
                }
            }
        };
        if (editingReview == null) MobileRepository.getInstance().createReview(invoiceId, selectedRating, comment, callback);
        else MobileRepository.getInstance().updateReview(editingReview, selectedRating, comment, callback);
    }

    private ImageView[] stars() { return new ImageView[]{binding.star1, binding.star2, binding.star3, binding.star4, binding.star5}; }
    private void setupStars() {
        ImageView[] stars = stars();
        for (int i = 0; i < stars.length; i++) {
            final int rating = i + 1;
            stars[i].setContentDescription(getString(R.string.a11y_rating_value, rating));
            stars[i].setOnClickListener(v -> setRating(rating, stars));
        }
    }
    private void setRating(int rating, ImageView[] stars) {
        selectedRating = Math.max(1, Math.min(5, rating));
        for (int i = 0; i < stars.length; i++) {
            stars[i].setColorFilter(ContextCompat.getColor(requireContext(), i < selectedRating ? R.color.colorSecondary : R.color.colorDivider));
            stars[i].setSelected(i + 1 == selectedRating);
        }
        binding.tvRatingPrompt.setText(selectedRating == 5 ? "Tuyệt vời! (5/5)" : selectedRating == 4 ? "Rất hài lòng! (4/5)" : "Đánh giá " + selectedRating + "/5");
    }

    @Override public void onDestroyView() { super.onDestroyView(); binding = null; }
}
