package com.restaurant.viettable.ui.profile;

import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
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
import com.restaurant.viettable.databinding.FragmentSupportChatBinding;
import com.restaurant.viettable.model.SupportChat;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.network.SignalRManager;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.SupportText;

import java.time.ZoneId;
import java.util.ArrayList;
import java.util.List;

/**
 * One support conversation (owner 06/11). Customer bubbles on the right ("Đã gửi"/"Đã xem"), staff on the left with
 * their name, automatic messages marked "Tin nhắn tự động". New staff replies arrive by the "supportMessage" realtime
 * event; while realtime is not connected the screen reloads every 20 s. Scrolling to the top loads older messages.
 */
public class SupportChatFragment extends BaseFragment implements SignalRManager.SupportListener, com.restaurant.viettable.ui.base.RequiresLogin {
    @Override public String loginPurpose() { return "và chat với nhân viên hỗ trợ"; }

    private static final String ARG_ID = "conversationId";
    private static final long POLL_MS = 20_000;

    private FragmentSupportChatBinding binding;
    private int conversationId;
    private SupportChat.Conversation conversation;
    private List<SupportChat.Message> messages = new ArrayList<>();
    private boolean hasOlder, loadingOlder, sending, loading;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final Runnable poll = new Runnable() {
        @Override public void run() {
            if (binding == null) return;
            if (!SignalRManager.getInstance().isConnected()) refresh(false);
            handler.postDelayed(this, POLL_MS);
        }
    };

    public static SupportChatFragment newInstance(int conversationId) {
        SupportChatFragment f = new SupportChatFragment();
        Bundle b = new Bundle();
        b.putInt(ARG_ID, conversationId);
        f.setArguments(b);
        return f;
    }

    @Override protected boolean liftBottomAboveKeyboard() { return true; }

    @Nullable @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentSupportChatBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        conversationId = getArguments() == null ? 0 : getArguments().getInt(ARG_ID);
        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnRetry.setOnClickListener(v -> refresh(true));
        binding.btnSend.setOnClickListener(v -> send());
        binding.tvOlder.setOnClickListener(v -> loadOlder());
        binding.scrollMessages.setOnScrollChangeListener((v, x, y, ox, oy) -> { if (y == 0 && oy > 0) loadOlder(); });
        refresh(true);
    }

    @Override public void onResume() {
        super.onResume();
        SignalRManager.getInstance().addSupportListener(this);
        handler.postDelayed(poll, POLL_MS);
    }

    @Override public void onPause() {
        super.onPause();
        SignalRManager.getInstance().removeSupportListener(this);
        handler.removeCallbacks(poll);
    }

    @Override public void onSupportChanged(int id) { if (id == conversationId) refresh(false); }

    /** Reloads the newest page; older pages already shown are kept. */
    private void refresh(boolean showProgress) {
        if (binding == null || conversationId <= 0 || loading) return;
        loading = true;
        if (showProgress && messages.isEmpty()) {
            binding.progress.setVisibility(View.VISIBLE);
            binding.tvState.setVisibility(View.GONE);
            binding.btnRetry.setVisibility(View.GONE);
        }
        MobileRepository.getInstance().loadSupportConversation(conversationId, 0, new MobileRepository.Callback<SupportChat.Detail>() {
            @Override public void onSuccess(SupportChat.Detail value) {
                loading = false;
                if (binding == null) return;
                binding.progress.setVisibility(View.GONE);
                boolean atBottom = isAtBottom();
                long lastBefore = messages.isEmpty() ? 0 : messages.get(messages.size() - 1).id;
                boolean first = messages.isEmpty();
                if (first) hasOlder = value.hasOlder;
                messages = SupportText.mergeOlder(messages, value.messages);
                conversation = value.conversation;
                render();
                long lastNow = messages.isEmpty() ? 0 : messages.get(messages.size() - 1).id;
                if (first || atBottom || lastNow != lastBefore) scrollToBottom();
                markRead();
            }
            @Override public void onError(ApiException error) {
                loading = false;
                if (binding == null || !messages.isEmpty()) return;
                binding.progress.setVisibility(View.GONE);
                binding.tvState.setText(apiError(error));
                binding.tvState.setVisibility(View.VISIBLE);
                binding.btnRetry.setVisibility(View.VISIBLE);
            }
        });
    }

    private void loadOlder() {
        if (binding == null || !hasOlder || loadingOlder || messages.isEmpty()) return;
        loadingOlder = true;
        binding.tvOlder.setText("Đang tải tin cũ hơn...");
        long before = messages.get(0).id;
        MobileRepository.getInstance().loadSupportConversation(conversationId, before, new MobileRepository.Callback<SupportChat.Detail>() {
            @Override public void onSuccess(SupportChat.Detail value) {
                loadingOlder = false;
                if (binding == null) return;
                int oldHeight = binding.layoutMessages.getHeight();
                hasOlder = value.hasOlder;
                messages = SupportText.mergeOlder(value.messages, messages);
                render();
                // Keep the message the customer was reading in place.
                binding.layoutMessages.post(() -> {
                    if (binding != null) binding.scrollMessages.scrollTo(0, binding.layoutMessages.getHeight() - oldHeight);
                });
            }
            @Override public void onError(ApiException error) {
                loadingOlder = false;
                if (binding != null) binding.tvOlder.setText("Chưa tải được tin cũ. Chạm để thử lại.");
            }
        });
    }

    private void send() {
        if (binding == null || sending) return;
        String text = binding.etMessage.getText() == null ? "" : binding.etMessage.getText().toString().trim();
        String why = SupportText.whyNotSendable(text);
        if (why != null) { binding.tilMessage.setError(why); return; }
        binding.tilMessage.setError(null);
        sending = true;
        binding.btnSend.setEnabled(false);
        MobileRepository.getInstance().sendSupportMessage(conversationId, text, new MobileRepository.Callback<Void>() {
            @Override public void onSuccess(Void value) {
                sending = false;
                if (binding == null) return;
                binding.btnSend.setEnabled(true);
                binding.etMessage.setText("");
                refresh(false);
            }
            @Override public void onError(ApiException error) {
                sending = false;
                if (binding == null) return;
                binding.btnSend.setEnabled(true);
                binding.tilMessage.setError(apiError(error)); // the text stays so it can be sent again
            }
        });
    }

    private void markRead() {
        if (conversation == null || messages.isEmpty()) return;
        long last = messages.get(messages.size() - 1).id;
        if (last > conversation.customerLastReadMessageId) MobileRepository.getInstance().markSupportRead(conversationId, last);
    }

    private void render() {
        if (conversation != null) {
            binding.tvTitle.setText(conversation.topicLabel);
            binding.cardContext.setVisibility(View.VISIBLE);
            binding.tvStatus.setText(SupportText.statusLabel(conversation.status).toUpperCase(new java.util.Locale("vi", "VN")));
            boolean hasContext = conversation.contextLabel != null && !conversation.contextLabel.isEmpty();
            binding.tvContext.setVisibility(hasContext ? View.VISIBLE : View.GONE);
            if (hasContext) binding.tvContext.setText(conversation.contextLabel);
        }
        binding.tvOlder.setVisibility(hasOlder ? View.VISIBLE : View.GONE);
        binding.tvOlder.setText("Kéo lên hoặc chạm để xem tin cũ hơn");
        binding.layoutMessages.removeAllViews();
        ZoneId zone = ZoneId.systemDefault();
        for (SupportChat.Message m : messages) binding.layoutMessages.addView(bubble(m, zone));
        if (conversation != null && conversation.resolved()) {
            TextView t = caption("Nhân viên đã đánh dấu đã giải quyết. Nhắn thêm nếu bạn vẫn cần hỗ trợ.", Gravity.CENTER);
            binding.layoutMessages.addView(t);
        }
    }

    private View bubble(SupportChat.Message m, ZoneId zone) {
        boolean mine = m.fromCustomer();
        LinearLayout box = new LinearLayout(requireContext());
        box.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams outer = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        outer.topMargin = dp(12);
        box.setLayoutParams(outer);
        boolean auto = m.automatic();
        int side = mine ? Gravity.END : auto ? Gravity.CENTER_HORIZONTAL : Gravity.START;
        box.setGravity(side);

        // Automatic messages already start with "Tin nhắn tự động:"; they sit in the middle on a soft yellow card.
        if (!mine && !auto) box.addView(caption(SupportText.sender(m), Gravity.START));

        TextView body = new TextView(requireContext());
        body.setText(m.body);
        body.setTextIsSelectable(true);
        body.setTextAppearance(R.style.TextAppearance_VietTable_Body);
        int bg, fg;
        if (mine) { bg = R.drawable.pd_chip_selected; fg = R.color.pd_on_inverse; }
        else if (auto) { bg = R.drawable.pd_card_yellow_soft; fg = R.color.pd_text; }
        else { bg = R.drawable.pd_card_surface; fg = R.color.pd_text; }
        body.setBackgroundResource(bg);
        body.setTextColor(requireContext().getColor(fg));
        body.setPadding(dp(14), dp(10), dp(14), dp(10));
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        if (mine) p.leftMargin = dp(48); else if (auto) { p.leftMargin = dp(24); p.rightMargin = dp(24); } else p.rightMargin = dp(48);
        p.topMargin = dp(2);
        if (auto) body.setTextAppearance(R.style.TextAppearance_VietTable_BodySmall);
        if (auto) body.setTextColor(requireContext().getColor(fg));
        body.setLayoutParams(p);
        box.addView(body);

        String meta = SupportText.clock(m.createdAtUtc, zone);
        String delivery = SupportText.delivery(m, conversation);
        if (!delivery.isEmpty()) meta = meta.isEmpty() ? delivery : meta + " · " + delivery;
        if (!meta.isEmpty()) box.addView(caption(meta, side));
        box.setContentDescription(SupportText.spoken(m, meta));
        return box;
    }

    private TextView caption(String text, int gravity) {
        TextView t = new TextView(requireContext());
        t.setText(text);
        t.setTextAppearance(R.style.TextAppearance_VietTable_Caption);
        t.setTextColor(requireContext().getColor(R.color.pd_muted));
        t.setGravity(gravity);
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        p.topMargin = dp(4);
        t.setLayoutParams(p);
        return t;
    }

    private boolean isAtBottom() {
        View child = binding.scrollMessages.getChildAt(0);
        return child == null || binding.scrollMessages.getScrollY() + binding.scrollMessages.getHeight() >= child.getHeight() - dp(24);
    }

    private void scrollToBottom() {
        binding.scrollMessages.post(() -> { if (binding != null) binding.scrollMessages.fullScroll(View.FOCUS_DOWN); });
    }

    private int dp(int value) { return (int) (value * getResources().getDisplayMetrics().density); }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        handler.removeCallbacks(poll);
        binding = null;
    }
}
