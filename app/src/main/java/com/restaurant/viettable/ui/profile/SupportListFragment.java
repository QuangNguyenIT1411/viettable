package com.restaurant.viettable.ui.profile;

import android.os.Bundle;
import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentSupportListBinding;
import com.restaurant.viettable.model.SupportChat;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.network.SignalRManager;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.SupportText;

import java.time.ZoneId;
import java.util.List;

/** "Hỗ trợ của tôi" (owner 06/11): every conversation with its status (Đang chờ / Đang xử lý / Đã giải quyết) and unread count. */
public class SupportListFragment extends BaseFragment implements SignalRManager.SupportListener, com.restaurant.viettable.ui.base.RequiresLogin {
    @Override public String loginPurpose() { return "các cuộc trò chuyện hỗ trợ"; }

    private FragmentSupportListBinding binding;

    public static SupportListFragment newInstance() { return new SupportListFragment(); }

    @Nullable @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentSupportListBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnRetry.setOnClickListener(v -> load());
        binding.btnNew.setOnClickListener(v -> navigateTo(SupportStartFragment.newInstance()));
    }

    @Override public void onResume() {
        super.onResume();
        SignalRManager.getInstance().addSupportListener(this);
        load();
    }

    @Override public void onPause() {
        super.onPause();
        SignalRManager.getInstance().removeSupportListener(this);
    }

    @Override public void onSupportChanged(int conversationId) { load(); }

    private void load() {
        if (binding == null) return;
        if (binding.layoutRows.getChildCount() == 0) binding.progress.setVisibility(View.VISIBLE);
        binding.tvState.setVisibility(View.GONE);
        binding.btnRetry.setVisibility(View.GONE);
        MobileRepository.getInstance().loadMySupport(new MobileRepository.Callback<List<SupportChat.Conversation>>() {
            @Override public void onSuccess(List<SupportChat.Conversation> value) {
                if (binding == null) return;
                binding.progress.setVisibility(View.GONE);
                binding.layoutRows.removeAllViews();
                if (value.isEmpty()) {
                    binding.tvState.setText("Bạn chưa có cuộc trò chuyện hỗ trợ nào.");
                    binding.tvState.setVisibility(View.VISIBLE);
                    return;
                }
                ZoneId zone = ZoneId.systemDefault();
                for (SupportChat.Conversation c : value) binding.layoutRows.addView(row(c, zone));
            }
            @Override public void onError(ApiException error) {
                if (binding == null) return;
                binding.progress.setVisibility(View.GONE);
                if (binding.layoutRows.getChildCount() > 0) return; // keep the list already shown
                binding.tvState.setText(apiError(error));
                binding.tvState.setVisibility(View.VISIBLE);
                binding.btnRetry.setVisibility(View.VISIBLE);
            }
        });
    }

    private View row(SupportChat.Conversation c, ZoneId zone) {
        LinearLayout box = new LinearLayout(requireContext());
        box.setOrientation(LinearLayout.VERTICAL);
        box.setBackgroundResource(R.drawable.pd_card_surface);
        box.setPadding(dp(16), dp(12), dp(16), dp(12));
        box.setClickable(true);
        box.setFocusable(true);
        box.setOnClickListener(v -> navigateTo(SupportChatFragment.newInstance(c.id)));
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        p.topMargin = dp(10);
        box.setLayoutParams(p);

        LinearLayout top = new LinearLayout(requireContext());
        top.setOrientation(LinearLayout.HORIZONTAL);
        top.setGravity(Gravity.CENTER_VERTICAL);
        TextView status = text(SupportText.statusLabel(c.status), R.style.TextAppearance_VietTable_Label,
                c.resolved() ? R.color.pd_mint_text : "Waiting".equals(c.status) ? R.color.pd_yellow_text : R.color.pd_red_text);
        top.addView(status, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        if (c.unread > 0) {
            TextView badge = text(c.unread + " mới", R.style.TextAppearance_VietTable_Label, R.color.pd_on_color);
            badge.setBackgroundResource(R.drawable.pd_badge_count);
            badge.setPadding(dp(8), dp(2), dp(8), dp(2));
            top.addView(badge);
        }
        box.addView(top);

        box.addView(text(c.topicLabel, R.style.TextAppearance_VietTable_Body_Strong, R.color.pd_text));
        if (c.contextLabel != null && !c.contextLabel.isEmpty()) box.addView(text(c.contextLabel, R.style.TextAppearance_VietTable_BodySmall, R.color.pd_muted));
        if (c.lastMessagePreview != null) {
            TextView preview = text(c.lastMessagePreview, R.style.TextAppearance_VietTable_BodySmall, R.color.pd_muted);
            preview.setMaxLines(2);
            preview.setEllipsize(android.text.TextUtils.TruncateAt.END);
            box.addView(preview);
        }
        String when = SupportText.at(c.lastMessageAtUtc, zone);
        if (!when.isEmpty()) box.addView(text(when, R.style.TextAppearance_VietTable_Caption, R.color.pd_muted));
        box.setContentDescription(SupportText.statusLabel(c.status) + ". " + c.topicLabel + (c.unread > 0 ? ". " + c.unread + " tin mới" : ""));
        return box;
    }

    private TextView text(String value, int style, int color) {
        TextView t = new TextView(requireContext());
        t.setTextAppearance(style);
        t.setTextColor(requireContext().getColor(color));
        t.setText(value);
        t.setPadding(0, dp(2), 0, 0);
        return t;
    }

    private int dp(int value) { return (int) (value * getResources().getDisplayMetrics().density); }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
