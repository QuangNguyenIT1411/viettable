package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.core.content.ContextCompat;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.ItemServiceRequestBinding;
import com.restaurant.viettable.model.ServiceRequest;
import com.restaurant.viettable.util.DisplayFormatters;

import java.util.List;

public class ServiceRequestAdapter extends RecyclerView.Adapter<ServiceRequestAdapter.ViewHolder> {

    public interface Listener { void onCancel(ServiceRequest request); }

    private final List<ServiceRequest> requests;
    private final Listener listener;

    public ServiceRequestAdapter(List<ServiceRequest> requests, Listener listener) {
        this.requests = requests;
        this.listener = listener;
    }

    @NonNull
    @Override
    public ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        ItemServiceRequestBinding binding = ItemServiceRequestBinding.inflate(
                LayoutInflater.from(parent.getContext()), parent, false
        );
        return new ViewHolder(binding);
    }

    @Override
    public void onBindViewHolder(@NonNull ViewHolder holder, int position) {
        holder.bind(requests.get(position), listener);
    }

    @Override
    public int getItemCount() {
        return requests != null ? requests.size() : 0;
    }

    public static class ViewHolder extends RecyclerView.ViewHolder {
        private final ItemServiceRequestBinding binding;

        public ViewHolder(ItemServiceRequestBinding binding) {
            super(binding.getRoot());
            this.binding = binding;
        }

        public void bind(ServiceRequest req, Listener listener) {
            binding.tvRequestType.setText(req.getType());
            binding.tvRequestNote.setText("Chi tiết: " + req.getNote());
            binding.tvRequestTime.setText("Thời gian gửi: " + DisplayFormatters.dateTime(req.getCreatedAt()));

            binding.tvRequestStatusBadge.setBackgroundResource(R.drawable.bg_status_badge);
            switch (req.getStatus()) {
                case REQUESTED:
                    binding.tvRequestStatusBadge.setText("CHỜ TIẾP NHẬN");
                    binding.tvRequestStatusBadge.setBackgroundTintList(
                            android.content.res.ColorStateList.valueOf(0xFFE1F5FE)
                    );
                    binding.tvRequestStatusBadge.setTextColor(0xFF01579B);
                    break;
                case ACCEPTED:
                    binding.tvRequestStatusBadge.setText("ĐANG XỬ LÝ");
                    binding.tvRequestStatusBadge.setBackgroundTintList(
                            android.content.res.ColorStateList.valueOf(0xFFFFF3E0)
                    );
                    binding.tvRequestStatusBadge.setTextColor(0xFFD84315);
                    break;
                case COMPLETED:
                    binding.tvRequestStatusBadge.setText("ĐÃ HOÀN THÀNH");
                    binding.tvRequestStatusBadge.setBackgroundTintList(
                            android.content.res.ColorStateList.valueOf(0xFFE8F5E9)
                    );
                    binding.tvRequestStatusBadge.setTextColor(0xFF1B5E20);
                    break;
                case CANCELLED:
                    binding.tvRequestStatusBadge.setText("ĐÃ HỦY");
                    binding.tvRequestStatusBadge.setBackgroundTintList(
                            android.content.res.ColorStateList.valueOf(0xFFFFEBEE)
                    );
                    binding.tvRequestStatusBadge.setTextColor(0xFFB71C1C);
                    break;
            }
            boolean cancellable = req.getStatus() == ServiceRequest.Status.REQUESTED;
            binding.btnCancelRequest.setVisibility(cancellable ? android.view.View.VISIBLE : android.view.View.GONE);
            binding.btnCancelRequest.setOnClickListener(cancellable && listener != null ? v -> listener.onCancel(req) : null);
        }
    }
}
