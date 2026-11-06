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

import com.google.android.material.chip.Chip;
import com.google.android.material.chip.ChipDrawable;
import com.restaurant.viettable.R;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentSupportStartBinding;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.SupportChat;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.SupportText;

import java.time.ZoneId;
import java.util.ArrayList;
import java.util.List;

/**
 * "Chat với nhân viên hỗ trợ" (owner 06/11): pick the branch (the selected one first) and a topic; for topics about a
 * deposit, transfer, booking or invoice pick it from the customer's own data. For the two "đã chuyển khoản" topics the
 * server is asked first: already confirmed → say when; not yet → remind the right content and amount, then chat.
 * Context entry points ("Cần hỗ trợ?") open this screen with the topic and item already chosen.
 */
public class SupportStartFragment extends BaseFragment {
    private static final String ARG_TOPIC = "topic", ARG_CONTEXT = "contextId", ARG_BRANCH = "branchId";

    private FragmentSupportStartBinding binding;
    private final List<SupportChat.Topic> topics = new ArrayList<>();
    private final List<Branch> branches = new ArrayList<>();
    private SupportChat.Topic topic;
    private SupportChat.Option option;
    private SupportChat.Check check;
    private int branchId;
    private boolean busy;
    private int loads;

    public static SupportStartFragment newInstance() { return new SupportStartFragment(); }

    /** From a screen's "Cần hỗ trợ?": topic code, the item's id (0 = none) and its branch (0 = selected branch). */
    public static SupportStartFragment forContext(String topic, int contextId, int branchId) {
        SupportStartFragment f = new SupportStartFragment();
        Bundle b = new Bundle();
        b.putString(ARG_TOPIC, topic);
        b.putInt(ARG_CONTEXT, contextId);
        b.putInt(ARG_BRANCH, branchId);
        f.setArguments(b);
        return f;
    }

    @Override protected boolean liftBottomAboveKeyboard() { return true; }

    @Nullable @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentSupportStartBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnMine.setOnClickListener(v -> navigateTo(SupportListFragment.newInstance()));
        binding.btnRetry.setOnClickListener(v -> load());
        binding.btnStart.setOnClickListener(v -> start());
        binding.btnNoMoreHelp.setOnClickListener(v -> navigateBack());
        binding.btnStillNeedHelp.setOnClickListener(v -> { binding.layoutCheckActions.setVisibility(View.GONE); showMessageBox(""); });
        Bundle args = getArguments();
        branchId = args == null ? 0 : args.getInt(ARG_BRANCH);
        if (branchId <= 0) branchId = parse(appStateManager.getSelectedBranch() == null ? null : appStateManager.getSelectedBranch().getId());
        load();
    }

    private void load() {
        if (binding == null) return;
        binding.progress.setVisibility(View.VISIBLE);
        binding.tvState.setVisibility(View.GONE);
        binding.btnRetry.setVisibility(View.GONE);
        binding.layoutForm.setVisibility(View.GONE);
        loads = 2;
        MobileRepository.getInstance().loadBranches(new MobileRepository.Callback<List<Branch>>() {
            @Override public void onSuccess(List<Branch> value) { branches.clear(); branches.addAll(value); loaded(); }
            @Override public void onError(ApiException error) { failed(error); }
        });
        MobileRepository.getInstance().loadSupportTopics(new MobileRepository.Callback<List<SupportChat.Topic>>() {
            @Override public void onSuccess(List<SupportChat.Topic> value) { topics.clear(); topics.addAll(value); loaded(); }
            @Override public void onError(ApiException error) { failed(error); }
        });
    }

    private void failed(ApiException error) {
        loads = -1;
        if (binding == null) return;
        binding.progress.setVisibility(View.GONE);
        binding.tvState.setText(apiError(error));
        binding.tvState.setVisibility(View.VISIBLE);
        binding.btnRetry.setVisibility(View.VISIBLE);
    }

    private void loaded() {
        if (binding == null || --loads != 0) return;
        binding.progress.setVisibility(View.GONE);
        binding.layoutForm.setVisibility(View.VISIBLE);
        renderBranches();
        renderTopics();
        String preset = getArguments() == null ? null : getArguments().getString(ARG_TOPIC);
        if (preset != null) for (SupportChat.Topic t : topics) if (t.code.equals(preset)) { selectTopic(t); break; }
    }

    private void renderBranches() {
        binding.chipBranches.removeAllViews();
        if (branchId <= 0 && !branches.isEmpty()) branchId = parse(branches.get(0).getId());
        for (Branch b : branches) {
            int id = parse(b.getId());
            Chip chip = chip(b.getName());
            chip.setChecked(id == branchId);
            chip.setOnClickListener(v -> branchId = id);
            binding.chipBranches.addView(chip);
        }
    }

    private void renderTopics() {
        binding.layoutTopics.removeAllViews();
        for (SupportChat.Topic t : topics) binding.layoutTopics.addView(row(t.label, null, t == topic, v -> selectTopic(t)));
    }

    private void selectTopic(SupportChat.Topic t) {
        topic = t; option = null; check = null;
        renderTopics();
        binding.cardCheck.setVisibility(View.GONE);
        binding.layoutOptions.removeAllViews();
        binding.tvOptionsTitle.setVisibility(View.GONE);
        if (t.contextType == null) { showMessageBox(""); return; }
        binding.layoutMessage.setVisibility(View.GONE);
        updateStart();
        binding.tvOptionsTitle.setText(optionsTitle(t.contextType));
        binding.tvOptionsTitle.setVisibility(View.VISIBLE);
        binding.layoutOptions.addView(note("Đang tải dữ liệu của bạn..."));
        MobileRepository.getInstance().loadSupportOptions(t.code, new MobileRepository.Callback<List<SupportChat.Option>>() {
            @Override public void onSuccess(List<SupportChat.Option> value) { if (binding != null && topic == t) renderOptions(value); }
            @Override public void onError(ApiException error) {
                if (binding == null || topic != t) return;
                binding.layoutOptions.removeAllViews();
                binding.layoutOptions.addView(note(apiError(error)));
                showMessageBox("");
            }
        });
    }

    private void renderOptions(List<SupportChat.Option> options) {
        binding.layoutOptions.removeAllViews();
        if (options.isEmpty()) {
            binding.layoutOptions.addView(note("Không tìm thấy mục nào gần đây trong tài khoản của bạn. Bạn vẫn có thể mô tả vấn đề cho nhân viên."));
            showMessageBox("");
            return;
        }
        int preset = getArguments() == null ? 0 : getArguments().getInt(ARG_CONTEXT);
        for (SupportChat.Option o : options) binding.layoutOptions.addView(row(o.label, o.detail, o == option, v -> selectOption(o, options)));
        if (preset > 0 && option == null) {
            getArguments().putInt(ARG_CONTEXT, 0); // only once
            for (SupportChat.Option o : options) if (o.contextId == preset) { selectOption(o, options); return; }
        }
    }

    private void selectOption(SupportChat.Option o, List<SupportChat.Option> all) {
        option = o; check = null;
        if (o.branchId > 0 && o.branchId != branchId) { branchId = o.branchId; renderBranches(); }
        binding.layoutOptions.removeAllViews();
        for (SupportChat.Option x : all) binding.layoutOptions.addView(row(x.label, x.detail, x == option, v -> selectOption(x, all)));
        if (!topic.checksPayment) { binding.cardCheck.setVisibility(View.GONE); showMessageBox(SupportText.suggestedMessage(topic, o, null)); return; }
        binding.layoutMessage.setVisibility(View.GONE);
        updateStart();
        binding.cardCheck.setVisibility(View.VISIBLE);
        binding.layoutCheckActions.setVisibility(View.GONE);
        binding.tvCheck.setText("Đang kiểm tra trạng thái khoản này...");
        SupportChat.Topic t = topic;
        MobileRepository.getInstance().checkSupportPayment(t.code, o.contextId, new MobileRepository.Callback<SupportChat.Check>() {
            @Override public void onSuccess(SupportChat.Check value) {
                if (binding == null || option != o) return;
                check = value;
                if (value.confirmed) {
                    binding.tvCheck.setText(SupportText.confirmedText(value, ZoneId.systemDefault()));
                    binding.layoutCheckActions.setVisibility(View.VISIBLE);
                } else {
                    binding.tvCheck.setText(SupportText.notConfirmedText(value));
                    showMessageBox(SupportText.suggestedMessage(t, o, value));
                }
            }
            @Override public void onError(ApiException error) {
                if (binding == null || option != o) return;
                binding.tvCheck.setText("Chưa kiểm tra được: " + apiError(error) + " Bạn vẫn có thể nhắn cho nhân viên.");
                showMessageBox(SupportText.suggestedMessage(t, o, null));
            }
        });
    }

    private void showMessageBox(String suggested) {
        binding.layoutMessage.setVisibility(View.VISIBLE);
        CharSequence current = binding.etMessage.getText();
        if (suggested != null && !suggested.isEmpty() && (current == null || current.length() == 0)) binding.etMessage.setText(suggested);
        updateStart();
        binding.scroll.post(() -> { if (binding != null) binding.scroll.smoothScrollTo(0, binding.layoutMessage.getBottom()); });
    }

    private void updateStart() {
        binding.btnStart.setEnabled(!busy && topic != null && branchId > 0 && binding.layoutMessage.getVisibility() == View.VISIBLE);
    }

    private void start() {
        String text = binding.etMessage.getText() == null ? "" : binding.etMessage.getText().toString().trim();
        String why = SupportText.whyNotSendable(text);
        if (why != null) { binding.tilMessage.setError(why); return; }
        binding.tilMessage.setError(null);
        busy = true; updateStart();
        MobileRepository.getInstance().startSupport(branchId, topic.code, option == null ? null : option.contextId, text,
                new MobileRepository.Callback<SupportChat.Detail>() {
                    @Override public void onSuccess(SupportChat.Detail value) {
                        busy = false;
                        if (binding == null || value.conversation == null) return;
                        navigateBack();
                        navigateTo(SupportChatFragment.newInstance(value.conversation.id));
                    }
                    @Override public void onError(ApiException error) {
                        busy = false;
                        if (binding == null) return;
                        updateStart();
                        showToast(apiError(error));
                    }
                });
    }

    private static String optionsTitle(String contextType) {
        switch (contextType) {
            case "DEPOSIT": return "CHỌN KHOẢN ĐẶT CỌC";
            case "PAYMENT": return "CHỌN LẦN CHUYỂN KHOẢN";
            case "RESERVATION": return "CHỌN ĐẶT BÀN";
            case "INVOICE": return "CHỌN HOÁ ĐƠN";
            default: return "CHỌN MỤC LIÊN QUAN";
        }
    }

    private Chip chip(String text) {
        Chip chip = new Chip(requireContext());
        chip.setChipDrawable(ChipDrawable.createFromAttributes(requireContext(), null, 0, R.style.Widget_VietTable_Chip_Filter));
        chip.setText(text);
        chip.setCheckable(true);
        chip.setMinHeight(dp(48));
        return chip;
    }

    /** A tappable card row; the chosen one is drawn in the inverse colour. */
    private View row(String title, String detail, boolean selected, View.OnClickListener click) {
        LinearLayout box = new LinearLayout(requireContext());
        box.setOrientation(LinearLayout.VERTICAL);
        box.setBackgroundResource(selected ? R.drawable.pd_chip_selected : R.drawable.pd_card_surface);
        box.setPadding(dp(16), dp(12), dp(16), dp(12));
        box.setMinimumHeight(dp(52));
        box.setGravity(Gravity.CENTER_VERTICAL);
        box.setClickable(true);
        box.setFocusable(true);
        box.setOnClickListener(click);
        int color = requireContext().getColor(selected ? R.color.pd_on_inverse : R.color.pd_text);
        TextView t = new TextView(requireContext());
        t.setTextAppearance(R.style.TextAppearance_VietTable_Body_Strong);
        t.setTextColor(color);
        t.setText(title);
        box.addView(t);
        if (detail != null && !detail.isEmpty()) {
            TextView d = new TextView(requireContext());
            d.setTextAppearance(R.style.TextAppearance_VietTable_BodySmall);
            d.setTextColor(requireContext().getColor(selected ? R.color.pd_on_inverse : R.color.pd_muted));
            d.setText(detail);
            box.addView(d);
        }
        box.setContentDescription(title + (detail == null ? "" : ", " + detail) + (selected ? ", đã chọn" : ""));
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        p.topMargin = dp(8);
        box.setLayoutParams(p);
        return box;
    }

    private TextView note(String text) {
        TextView t = new TextView(requireContext());
        t.setTextAppearance(R.style.TextAppearance_VietTable_BodySmall);
        t.setTextColor(requireContext().getColor(R.color.pd_muted));
        t.setText(text);
        t.setPadding(0, dp(8), 0, 0);
        return t;
    }

    private static int parse(String id) {
        try { return id == null ? 0 : Integer.parseInt(id.trim()); } catch (NumberFormatException e) { return 0; }
    }

    private int dp(int value) { return (int) (value * getResources().getDisplayMetrics().density); }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
