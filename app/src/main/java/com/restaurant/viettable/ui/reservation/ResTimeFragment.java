package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.material.chip.Chip;
import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentResTimeBinding;
import com.restaurant.viettable.model.ReservationDraft;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.DisplayFormatters;
import com.restaurant.viettable.util.ReservationSchedule;

public class ResTimeFragment extends BaseFragment {

    private static final String ARG_DRAFT = "arg_draft";
    private FragmentResTimeBinding binding;
    private ReservationDraft draft;

    private String selectedSlot;

    public static ResTimeFragment newInstance(ReservationDraft draft) {
        ResTimeFragment f = new ResTimeFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_DRAFT, draft);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            draft = (ReservationDraft) getArguments().getSerializable(ARG_DRAFT);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentResTimeBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (draft == null) draft = new ReservationDraft();

        binding.btnBack.setOnClickListener(v -> navigateBack());

        binding.wizardProgress.tvWizardStepName.setText(getString(R.string.res_wizard_step2));
        binding.wizardProgress.tvWizardStepFraction.setText("2/5");
        binding.wizardProgress.progressWizard.setProgress(40);

        binding.tvDateSelectedDisplay.setText("Ngày đã chọn: " + DisplayFormatters.weekdayDate(draft.getDate()));

        // One slot table (10:00–21:30, as the server accepts); today hides the slots less than 15 minutes away.
        java.time.LocalDateTime now = java.time.LocalDateTime.now();
        java.time.LocalDate day = ReservationSchedule.parseDate(draft.getDate(), now.toLocalDate());
        if (day == null) day = now.toLocalDate();
        selectedSlot = ReservationSchedule.firstSelectable(day, now, draft.getTimeSlot() != null && !draft.getTimeSlot().isEmpty() ? draft.getTimeSlot() : "19:00");
        binding.chipGroupLunch.removeAllViews();
        binding.chipGroupDinner.removeAllViews();
        for (String slot : ReservationSchedule.slots()) {
            boolean lunch = ReservationSchedule.isLunch(slot);
            boolean bookable = ReservationSchedule.selectable(day, slot, now);
            Chip chip = new Chip(getContext());
            chip.setText(slot);
            chip.setCheckable(bookable);
            chip.setEnabled(bookable);
            if (!bookable) {
                chip.setChipBackgroundColor(android.content.res.ColorStateList.valueOf(getResources().getColor(R.color.pd_surface_2)));
                chip.setTextColor(getResources().getColor(R.color.pd_dim));
            } else {
                if (slot.equals(selectedSlot)) chip.setChecked(true);
                chip.setOnCheckedChangeListener((btn, isChecked) -> {
                    if (isChecked) {
                        selectedSlot = slot;
                        (lunch ? binding.chipGroupDinner : binding.chipGroupLunch).clearCheck();
                    }
                });
            }
            (lunch ? binding.chipGroupLunch : binding.chipGroupDinner).addView(chip);
        }
        sizeChipsInThreeColumns(binding.chipGroupLunch);
        sizeChipsInThreeColumns(binding.chipGroupDinner);
        binding.btnContinue.setEnabled(selectedSlot != null);
        if (selectedSlot == null) showToast("Hôm nay đã hết giờ nhận đặt bàn. Vui lòng chọn ngày khác.");

        binding.btnContinue.setOnClickListener(v -> {
            if (selectedSlot == null) { showToast("Vui lòng chọn giờ đến."); return; }
            draft.setTimeSlot(selectedSlot);
            navigateTo(ResGuestCountFragment.newInstance(draft));
        });
    }

    /** Time chips sit in a 3-column grid (DESIGN_PHO_DEM.md section 6 "Đặt bàn": "lưới chip 3 cột"). */
    private static void sizeChipsInThreeColumns(com.google.android.material.chip.ChipGroup group) {
        group.post(() -> {
            int width = group.getWidth() - group.getPaddingLeft() - group.getPaddingRight();
            if (width <= 0) return;
            int cell = (width - 2 * group.getChipSpacingHorizontal()) / 3;
            for (int i = 0; i < group.getChildCount(); i++) {
                View chip = group.getChildAt(i);
                ViewGroup.LayoutParams lp = chip.getLayoutParams();
                lp.width = cell;
                chip.setLayoutParams(lp);
                if (chip instanceof com.google.android.material.chip.Chip)
                    ((com.google.android.material.chip.Chip) chip).setTextAlignment(View.TEXT_ALIGNMENT_CENTER);
            }
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
