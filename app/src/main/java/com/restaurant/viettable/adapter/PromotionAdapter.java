package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.databinding.ItemPromotionBinding;
import com.restaurant.viettable.model.Promotion;
import com.restaurant.viettable.util.PromotionRules;

import java.util.List;

/** Running promotions as plum cards ("Ưu đãi"). */
public class PromotionAdapter extends RecyclerView.Adapter<PromotionAdapter.ViewHolder> {

    public interface OnPromotionClick { void onClick(Promotion promotion); }

    private final List<Promotion> items;
    private final OnPromotionClick listener;

    public PromotionAdapter(List<Promotion> items, OnPromotionClick listener) {
        this.items = items;
        this.listener = listener;
    }

    @NonNull
    @Override
    public ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        return new ViewHolder(ItemPromotionBinding.inflate(LayoutInflater.from(parent.getContext()), parent, false));
    }

    @Override
    public void onBindViewHolder(@NonNull ViewHolder holder, int position) {
        Promotion p = items.get(position);
        holder.binding.tvPromoName.setText(p.getName());
        holder.binding.tvPromoSummary.setText(p.getSummary());
        holder.binding.tvPromoUntil.setText(PromotionRules.until(p.getEndsAtUtc()));
        holder.binding.tvPromoBadge.setText(PromotionRules.badge(p));
        holder.binding.cardPromotion.setContentDescription(p.getName() + ". " + p.getSummary());
        holder.binding.cardPromotion.setOnClickListener(v -> { if (listener != null) listener.onClick(p); });
    }

    @Override
    public int getItemCount() { return items.size(); }

    static class ViewHolder extends RecyclerView.ViewHolder {
        final ItemPromotionBinding binding;
        ViewHolder(ItemPromotionBinding binding) { super(binding.getRoot()); this.binding = binding; }
    }
}
