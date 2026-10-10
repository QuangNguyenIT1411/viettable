package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.databinding.ItemDiningSessionHistoryBinding;
import com.restaurant.viettable.model.DiningSessionHistoryGroup;
import com.restaurant.viettable.model.DiningSessionSummary;
import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.util.DisplayFormatters;

import java.util.List;
import java.util.Locale;

public class DiningSessionHistoryAdapter extends RecyclerView.Adapter<DiningSessionHistoryAdapter.ViewHolder> {
    private final List<DiningSessionHistoryGroup> sessions;
    private final SessionOrderAdapter.OnOrderClickListener listener;

    public DiningSessionHistoryAdapter(List<DiningSessionHistoryGroup> sessions,
                                       SessionOrderAdapter.OnOrderClickListener listener) {
        this.sessions = sessions;
        this.listener = listener;
    }

    @NonNull
    @Override
    public ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        return new ViewHolder(ItemDiningSessionHistoryBinding.inflate(
                LayoutInflater.from(parent.getContext()), parent, false));
    }

    @Override
    public void onBindViewHolder(@NonNull ViewHolder holder, int position) {
        holder.bind(sessions.get(position), listener);
    }

    @Override
    public int getItemCount() { return sessions == null ? 0 : sessions.size(); }

    static final class ViewHolder extends RecyclerView.ViewHolder {
        private final ItemDiningSessionHistoryBinding binding;

        ViewHolder(ItemDiningSessionHistoryBinding binding) {
            super(binding.getRoot());
            this.binding = binding;
        }

        void bind(DiningSessionHistoryGroup group, SessionOrderAdapter.OnOrderClickListener listener) {
            DiningSessionSummary session = group.getSession();
            String sessionId = session == null ? group.getOrders().get(0).getDiningSessionId() : session.getId();
            String table = session == null || session.getTableLabel().isEmpty()
                    ? group.getOrders().get(0).getTableLabel() : session.getTableLabel();
            binding.tvHistorySessionTitle.setText("Phiên #" + (sessionId == null ? "—" : sessionId)
                    + " • " + (table == null || table.trim().isEmpty() ? "Chưa rõ bàn" : table));
            binding.tvHistorySessionBranch.setText(group.getBranchName() == null || group.getBranchName().isEmpty()
                    ? "Chưa rõ chi nhánh" : group.getBranchName());
            String started = session == null ? null : session.getStartedAtUtc();
            String ended = session == null ? null : session.getEndedAtUtc();
            binding.tvHistorySessionTimes.setText("Bắt đầu: " + DisplayFormatters.dateTime(started)
                    + " • Kết thúc: " + (ended == null || ended.isEmpty() ? "Chưa kết thúc" : DisplayFormatters.dateTime(ended)));
            String status = session == null ? "" : session.getStatus().toUpperCase(Locale.ROOT);
            binding.tvHistorySessionStatus.setText(sessionStatusLabel(status) + " • " + group.getOrders().size() + " đợt gọi món");
            binding.tvHistorySessionTotal.setText(String.format(Locale.GERMANY, "%,dđ", group.getTotalAmount()));
            binding.rvHistoryRounds.setLayoutManager(new LinearLayoutManager(binding.getRoot().getContext()));
            binding.rvHistoryRounds.setAdapter(new SessionOrderAdapter(group.getOrders(), listener));
        }

        private String sessionStatusLabel(String status) {
            switch (status) {
                case "CLOSED": return "ĐÃ ĐÓNG";
                case "PAID": return "ĐÃ THANH TOÁN";
                case "PAYMENT_REQUESTED": return "ĐANG THANH TOÁN";
                case "CANCELLED": return "ĐÃ HỦY";
                case "ACTIVE": return "ĐANG PHỤC VỤ";
                case "CREATED": return "MỚI TẠO";
                default: return status.isEmpty() ? "PHIÊN" : status;
            }
        }
    }
}
