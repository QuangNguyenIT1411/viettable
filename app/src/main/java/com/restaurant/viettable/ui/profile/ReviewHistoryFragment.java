package com.restaurant.viettable.ui.profile;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.restaurant.viettable.adapter.ReviewAdapter;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentReviewHistoryBinding;
import com.restaurant.viettable.model.Review;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.common.DialogHelper;

import java.util.List;

public final class ReviewHistoryFragment extends BaseFragment {
    private FragmentReviewHistoryBinding binding;
    private ReviewAdapter adapter;
    private boolean loading;
    public static ReviewHistoryFragment newInstance() { return new ReviewHistoryFragment(); }

    @Nullable @Override public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle state) {
        binding = FragmentReviewHistoryBinding.inflate(inflater, container, false); setBottomNavVisible(false); return binding.getRoot();
    }
    @Override public void onViewCreated(@NonNull View view, @Nullable Bundle state) {
        super.onViewCreated(view, state); binding.toolbar.setNavigationOnClickListener(v -> navigateBack());
        adapter = new ReviewAdapter(new ReviewAdapter.Listener() {
            @Override public void onEdit(Review review) { navigateTo(ReviewFragment.newInstanceForEdit(review)); }
            @Override public void onDelete(Review review) { confirmDelete(review); }
        });
        binding.rvReviews.setLayoutManager(new LinearLayoutManager(requireContext())); binding.rvReviews.setAdapter(adapter);
        binding.btnRetryReviews.setOnClickListener(v -> loadReviews()); loadReviews();
    }
    @Override public void onResume() { super.onResume(); if (binding != null && !loading) loadReviews(); }

    private void loadReviews() {
        if (binding == null || loading) return; loading = true;
        binding.progressReviews.setVisibility(View.VISIBLE); binding.layoutReviewState.setVisibility(View.GONE);
        MobileRepository.getInstance().loadReviews(new MobileRepository.Callback<List<Review>>() {
            @Override public void onSuccess(List<Review> values) {
                loading = false; if (binding == null) return; binding.progressReviews.setVisibility(View.GONE); adapter.submit(values);
                binding.rvReviews.setVisibility(values.isEmpty() ? View.GONE : View.VISIBLE);
                if (values.isEmpty()) showState("Bạn chưa gửi đánh giá nào.", false);
            }
            @Override public void onError(ApiException error) {
                loading = false; if (binding == null) return; binding.progressReviews.setVisibility(View.GONE); binding.rvReviews.setVisibility(View.GONE); showState(apiError(error), true);
            }
        });
    }
    private void showState(String message, boolean retry) {
        binding.tvReviewState.setText(message); binding.layoutReviewState.setVisibility(View.VISIBLE); binding.btnRetryReviews.setVisibility(retry ? View.VISIBLE : View.GONE);
    }
    private void confirmDelete(Review review) {
        DialogHelper.showDestructiveDialog(requireContext(), "Xóa đánh giá?", "Đánh giá của hóa đơn #" + review.getInvoiceId() + " sẽ bị xóa khỏi máy chủ.", "Xóa",
                (dialog, which) -> MobileRepository.getInstance().deleteReview(review.getReviewId(), new MobileRepository.Callback<Void>() {
                    @Override public void onSuccess(Void value) { if (binding != null) { showToast("Đã xóa đánh giá."); loadReviews(); } }
                    @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
                }), "Hủy", null);
    }
    @Override public void onDestroyView() { super.onDestroyView(); binding = null; }
}
