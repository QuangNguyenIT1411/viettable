package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.view.inputmethod.EditorInfo;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.material.chip.Chip;
import com.restaurant.viettable.R;
import com.restaurant.viettable.assistant.BookingAssistant;
import com.restaurant.viettable.assistant.BookingIntent;
import com.restaurant.viettable.assistant.BookingIntentParser;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentBookingAssistantBinding;
import com.restaurant.viettable.model.AvailabilityAlternatives;
import com.restaurant.viettable.model.AvailabilityOption;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.model.ReservationDraft;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * "Trợ lý đặt bàn" (owner 06/11): the guest types in plain Vietnamese, the assistant asks what is missing, checks
 * the real tables and books only after "Đặt bàn". Rules on the phone, no paid AI; every answer about tables comes
 * from the server.
 */
public class BookingAssistantFragment extends BaseFragment {
    private FragmentBookingAssistantBinding binding;
    private BookingAssistant assistant;
    private final Map<String, AvailabilityOption> suggestionByLabel = new HashMap<>();
    private boolean busy;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentBookingAssistantBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        binding.btnBack.setOnClickListener(v -> navigateBack());
        List<BookingIntentParser.BranchRef> refs = new ArrayList<>();
        for (Branch b : appStateManager.getBranches()) refs.add(new BookingIntentParser.BranchRef(b.getId(), b.getName()));
        assistant = new BookingAssistant(refs);
        MobileRepository.getInstance().loadBookingHours((open, last, slot) -> { if (assistant != null) assistant.setHours(open, last, slot); });
        show(assistant.greeting());
        binding.btnSend.setOnClickListener(v -> sendTyped());
        binding.etMessage.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == EditorInfo.IME_ACTION_SEND) { sendTyped(); return true; }
            return false;
        });
    }

    @Override
    protected boolean liftBottomAboveKeyboard() { return true; }

    private void sendTyped() {
        CharSequence text = binding.etMessage.getText();
        if (text == null || text.toString().trim().isEmpty()) return;
        binding.etMessage.setText("");
        send(text.toString().trim());
    }

    private void send(String text) {
        if (busy) return;
        bubble(text, true);
        AvailabilityOption option = suggestionByLabel.get(text);
        if (option != null) {
            show(assistant.choose(option.isOtherBranch() ? option.branchId : null,
                    option.localDate == null ? null : LocalDate.parse(option.localDate), LocalTime.parse(option.localTime)));
            return;
        }
        show(assistant.reply(text, LocalDateTime.now()));
    }

    private void show(BookingAssistant.Turn turn) {
        if (binding == null) return;
        bubble(turn.message, false);
        quickReplies(turn.quickReplies);
        if (turn.action == BookingAssistant.Action.CHECK_AVAILABILITY) check();
        else if (turn.action == BookingAssistant.Action.CREATE) create();
    }

    private ReservationDraft draft() {
        BookingIntent intent = assistant.intent();
        ReservationDraft draft = new ReservationDraft();
        draft.setBranch(appStateManager.findBranch(intent.branchId));
        draft.setDate(intent.date.toString());
        draft.setTimeSlot(intent.time.toString().substring(0, 5));
        draft.setGuestCount(intent.partySize);
        draft.setNote(intent.note == null ? "" : intent.note);
        draft.setTableId("0"); // the server picks the table (or pushed-together tables)
        return draft;
    }

    private void check() {
        busy = true;
        MobileRepository.getInstance().loadAvailabilityAlternatives(draft(), null, new MobileRepository.Callback<AvailabilityAlternatives>() {
            @Override public void onSuccess(AvailabilityAlternatives value) {
                busy = false;
                if (binding == null) return;
                if (value.available) { show(assistant.availableNow()); return; }
                suggestionByLabel.clear();
                List<String> labels = new ArrayList<>();
                for (AvailabilityOption o : value.times) { suggestionByLabel.put(o.label(), o); labels.add(o.label()); }
                for (AvailabilityOption o : value.branches) { suggestionByLabel.put(o.label(), o); labels.add(o.label()); }
                show(assistant.full(labels));
            }
            @Override public void onError(ApiException error) {
                busy = false;
                if (binding != null) bubble(apiError(error), false);
            }
        });
    }

    private void create() {
        busy = true;
        MobileRepository.getInstance().createReservation(draft(), new MobileRepository.Callback<Reservation>() {
            @Override public void onSuccess(Reservation value) {
                busy = false;
                if (binding == null) return;
                if (value.getStatus() == Reservation.Status.PENDING_DEPOSIT) navigateTo(ResDepositFragment.newInstance(value));
                else navigateTo(ResConfirmationFragment.newInstance(value));
            }
            @Override public void onError(ApiException error) {
                busy = false;
                if (binding == null) return;
                bubble(apiError(error), false);
                if ("reservation_slot_unavailable".equals(error.getCode()) || "table_taken".equals(error.getCode())) check();
            }
        });
    }

    private void bubble(String text, boolean mine) {
        TextView view = new TextView(requireContext());
        view.setText(text);
        view.setTextAppearance(R.style.TextAppearance_VietTable_Body);
        view.setTextColor(requireContext().getColor(mine ? R.color.pd_on_inverse : R.color.pd_text)); // pd_chip_selected is the inverse colour in both themes
        view.setBackgroundResource(mine ? R.drawable.pd_chip_selected : R.drawable.pd_card_surface);
        int pad = dp(14);
        view.setPadding(pad, dp(10), pad, dp(10));
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        params.topMargin = dp(10);
        params.gravity = mine ? Gravity.END : Gravity.START;
        if (mine) params.leftMargin = dp(48); else params.rightMargin = dp(48);
        view.setLayoutParams(params);
        binding.layoutMessages.addView(view);
        binding.scrollMessages.post(() -> { if (binding != null) binding.scrollMessages.fullScroll(View.FOCUS_DOWN); });
    }

    private void quickReplies(List<String> labels) {
        binding.chipGroupQuickReplies.removeAllViews();
        for (String label : labels) {
            Chip chip = new Chip(requireContext());
            chip.setText(label);
            chip.setOnClickListener(v -> send(label));
            binding.chipGroupQuickReplies.addView(chip);
        }
    }

    private int dp(int value) { return (int) (value * getResources().getDisplayMetrics().density); }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
