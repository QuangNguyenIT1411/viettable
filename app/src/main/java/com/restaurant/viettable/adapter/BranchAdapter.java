package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.databinding.ItemBranchCardBinding;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.util.ImageLoader;

import java.util.List;
import com.restaurant.viettable.util.SafeText;

public class BranchAdapter extends RecyclerView.Adapter<BranchAdapter.BranchViewHolder> {

    public interface OnBranchActionListener {
        void onSelectBranch(Branch branch);
        void onViewDetail(Branch branch);
    }

    private final List<Branch> branchList;
    private final OnBranchActionListener listener;

    public BranchAdapter(List<Branch> branchList, OnBranchActionListener listener) {
        this.branchList = branchList;
        this.listener = listener;
    }

    @NonNull
    @Override
    public BranchViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        ItemBranchCardBinding binding = ItemBranchCardBinding.inflate(
                LayoutInflater.from(parent.getContext()), parent, false
        );
        return new BranchViewHolder(binding);
    }

    @Override
    public void onBindViewHolder(@NonNull BranchViewHolder holder, int position) {
        Branch branch = branchList.get(position);
        holder.bind(branch, listener);
    }

    @Override
    public int getItemCount() {
        return branchList != null ? branchList.size() : 0;
    }

    public static class BranchViewHolder extends RecyclerView.ViewHolder {
        private final ItemBranchCardBinding binding;

        public BranchViewHolder(ItemBranchCardBinding binding) {
            super(binding.getRoot());
            this.binding = binding;
        }

        public void bind(Branch branch, OnBranchActionListener listener) {
            ImageLoader.getInstance().load(branch, binding.ivBranchImage);
            binding.tvBranchName.setText(branch.getName());
            binding.tvBranchAddress.setText(SafeText.clean(branch.getAddress()));
            binding.tvOpeningHours.setText(branch.getOpeningHours());
            binding.tvOpeningHours.setVisibility(isBlank(branch.getOpeningHours()) ? View.GONE : View.VISIBLE);
            boolean hasDistance = branch.getDistanceKm() > 0;
            binding.tvDistance.setVisibility(hasDistance ? View.VISIBLE : View.GONE);
            if (hasDistance) binding.tvDistance.setText(branch.getDistanceKm() + " km");
            boolean hasRating = branch.getRating() > 0 && branch.getReviewCount() > 0;
            binding.layoutRating.setVisibility(hasRating ? View.VISIBLE : View.GONE);
            if (hasRating) binding.tvRating.setText(branch.getRating() + " (" + branch.getReviewCount() + ")");

            boolean hasOpeningData = !isBlank(branch.getOpeningHours());
            binding.tvOpenStatus.setVisibility(hasOpeningData ? View.VISIBLE : View.GONE);
            if (hasOpeningData && branch.isOpen()) {
                binding.tvOpenStatus.setText("Đang mở cửa");
                binding.tvOpenStatus.setBackgroundResource(com.restaurant.viettable.R.drawable.bg_status_badge);
                binding.tvOpenStatus.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFE8F5E9));
                binding.tvOpenStatus.setTextColor(0xFF2E7D5B);
                binding.btnSelectBranch.setEnabled(true);
            } else if (hasOpeningData) {
                binding.tvOpenStatus.setText("Đã đóng cửa");
                binding.tvOpenStatus.setBackgroundResource(com.restaurant.viettable.R.drawable.bg_status_badge);
                binding.tvOpenStatus.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFFFEBEE));
                binding.tvOpenStatus.setTextColor(0xFFD32F2F);
            }
            binding.btnSelectBranch.setEnabled(true);

            binding.btnSelectBranch.setOnClickListener(v -> {
                if (listener != null) listener.onSelectBranch(branch);
            });

            binding.btnViewDetail.setOnClickListener(v -> {
                if (listener != null) listener.onViewDetail(branch);
            });

            binding.cardBranch.setOnClickListener(v -> {
                if (listener != null) listener.onViewDetail(branch);
            });
        }

        private static boolean isBlank(String value) {
            return value == null || value.trim().isEmpty();
        }
    }
}
