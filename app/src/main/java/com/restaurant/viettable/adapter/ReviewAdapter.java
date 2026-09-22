package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.databinding.ItemCustomerReviewBinding;
import com.restaurant.viettable.model.Review;

import java.util.ArrayList;
import java.util.List;

public final class ReviewAdapter extends RecyclerView.Adapter<ReviewAdapter.Holder> {
    public interface Listener { void onEdit(Review review); void onDelete(Review review); }
    private final List<Review> items = new ArrayList<>();
    private final Listener listener;
    public ReviewAdapter(Listener listener) { this.listener = listener; }
    public void submit(List<Review> values) { items.clear(); items.addAll(values); notifyDataSetChanged(); }

    @NonNull @Override public Holder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        return new Holder(ItemCustomerReviewBinding.inflate(LayoutInflater.from(parent.getContext()), parent, false));
    }
    @Override public void onBindViewHolder(@NonNull Holder holder, int position) { holder.bind(items.get(position)); }
    @Override public int getItemCount() { return items.size(); }

    final class Holder extends RecyclerView.ViewHolder {
        private final ItemCustomerReviewBinding binding;
        Holder(ItemCustomerReviewBinding binding) { super(binding.getRoot()); this.binding = binding; }
        void bind(Review review) {
            binding.tvReviewInvoice.setText("Hóa đơn #" + review.getInvoiceId());
            binding.tvReviewStars.setText(stars(review.getOverallRating()) + "  " + review.getOverallRating() + "/5");
            binding.tvReviewDetails.setText("Món ăn " + review.getFoodRating() + "/5 • Phục vụ " + review.getServiceRating() + "/5 • Không gian " + review.getSpaceRating() + "/5");
            binding.tvReviewComment.setText(review.getComment() == null || review.getComment().isEmpty() ? "Không có nhận xét." : review.getComment());
            binding.btnEditReview.setOnClickListener(v -> listener.onEdit(review));
            binding.btnDeleteReview.setOnClickListener(v -> listener.onDelete(review));
        }
    }
    private static String stars(int rating) { StringBuilder value = new StringBuilder(); for (int i = 1; i <= 5; i++) value.append(i <= rating ? '★' : '☆'); return value.toString(); }
}
