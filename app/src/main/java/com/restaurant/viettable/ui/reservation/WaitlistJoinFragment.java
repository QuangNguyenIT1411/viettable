package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.material.chip.Chip;
import com.google.android.material.chip.ChipGroup;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentWaitlistJoinBinding;
import com.restaurant.viettable.model.ReservationDraft;
import com.restaurant.viettable.model.WaitlistEntry;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.reminder.WaitlistPollWorker;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.DisplayFormatters;
import com.restaurant.viettable.util.ReservationSchedule;
import com.restaurant.viettable.util.WaitlistWindow;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.util.List;

/** "Vào danh sách chờ" (owner 06/11): a window of times on the booking's day; the server offers the first free one. */
public class WaitlistJoinFragment extends BaseFragment {
    private static final String ARG_DRAFT = "draft";
    private FragmentWaitlistJoinBinding binding;
    private ReservationDraft draft;

    public static WaitlistJoinFragment newInstance(ReservationDraft draft) {
        WaitlistJoinFragment fragment = new WaitlistJoinFragment();
        Bundle args = new Bundle();
        args.putSerializable(ARG_DRAFT, draft);
        fragment.setArguments(args);
        return fragment;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) draft = (ReservationDraft) getArguments().getSerializable(ARG_DRAFT);
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentWaitlistJoinBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        binding.btnBack.setOnClickListener(v -> navigateBack());
        if (draft == null || draft.getBranch() == null) { navigateBack(); return; }
        binding.tvWaitlistContext.setText(draft.getBranch().getName() + " · " + DisplayFormatters.weekdayDate(draft.getDate())
                + " · " + draft.getGuestCount() + " khách");
        binding.tvWaitlistHow.setText("Khi có bàn trống trong khoảng giờ này, nhà hàng giữ bàn và báo bạn. Bạn có ít phút để xác nhận; "
                + "không xác nhận thì bàn được mời cho khách kế tiếp.");
        LocalDate day = ReservationSchedule.parseDate(draft.getDate(), LocalDate.now());
        List<String> slots = WaitlistWindow.slots(day, LocalDateTime.now());
        if (slots.isEmpty()) { binding.btnJoin.setEnabled(false); showToast("Ngày này không còn giờ để chờ."); return; }
        String from = WaitlistWindow.defaultFrom(slots, draft.getTimeSlot());
        fill(binding.chipGroupFrom, slots, from);
        fill(binding.chipGroupTo, slots, WaitlistWindow.defaultTo(slots, from));
        binding.btnJoin.setOnClickListener(v -> join());
    }

    private void fill(ChipGroup group, List<String> slots, String selected) {
        group.removeAllViews();
        for (String slot : slots) {
            Chip chip = new Chip(requireContext());
            chip.setText(slot);
            chip.setCheckable(true);
            chip.setId(View.generateViewId());
            group.addView(chip);
            if (slot.equals(selected)) chip.setChecked(true);
        }
    }

    private static String checked(ChipGroup group) {
        int id = group.getCheckedChipId();
        View chip = id == View.NO_ID ? null : group.findViewById(id);
        return chip instanceof Chip ? ((Chip) chip).getText().toString() : null;
    }

    private void join() {
        String from = checked(binding.chipGroupFrom);
        String to = checked(binding.chipGroupTo);
        String problem = WaitlistWindow.problem(from, to);
        if (problem != null) { showToast(problem); return; }
        binding.btnJoin.setEnabled(false);
        String note = binding.etWaitlistNote.getText() == null ? null : binding.etWaitlistNote.getText().toString().trim();
        MobileRepository.getInstance().joinWaitlist(draft, from, to, note == null || note.isEmpty() ? null : note,
                new MobileRepository.Callback<WaitlistEntry>() {
                    @Override public void onSuccess(WaitlistEntry value) {
                        WaitlistPollWorker.ensureScheduled(requireContext().getApplicationContext());
                        if (binding == null) return;
                        showToast(value.isOffered() ? "Có bàn cho bạn ngay! Xác nhận để giữ bàn." : "Đã vào danh sách chờ.");
                        navigateTo(new WaitlistFragment());
                    }
                    @Override public void onError(ApiException error) {
                        if (binding == null) return;
                        binding.btnJoin.setEnabled(true);
                        showToast(apiError(error));
                    }
                });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
