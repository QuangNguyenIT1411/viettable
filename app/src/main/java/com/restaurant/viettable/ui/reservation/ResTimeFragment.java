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

import java.util.Arrays;
import java.util.List;

public class ResTimeFragment extends BaseFragment {

    private static final String ARG_DRAFT = "arg_draft";
    private FragmentResTimeBinding binding;
    private ReservationDraft draft;

    private final List<String> lunchSlots = Arrays.asList("11:00", "11:30", "12:00", "12:30", "13:00", "13:30");
    private final List<String> dinnerSlots = Arrays.asList("17:30", "18:00", "18:30", "19:00", "19:30", "20:00", "20:30");
    private String selectedSlot = "19:00";

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
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (draft == null) draft = new ReservationDraft();

        binding.btnBack.setOnClickListener(v -> navigateBack());

        binding.wizardProgress.tvWizardStepName.setText(getString(R.string.res_wizard_step2));
        binding.wizardProgress.tvWizardStepFraction.setText("2/6");
        binding.wizardProgress.progressWizard.setProgress(33);

        binding.tvDateSelectedDisplay.setText("Ngày đã chọn: " + draft.getDate());

        // Lunch chips
        binding.chipGroupLunch.removeAllViews();
        for (String slot : lunchSlots) {
            Chip chip = new Chip(getContext());
            chip.setText(slot);
            boolean isSoldOut = slot.contains("Hết");
            chip.setCheckable(!isSoldOut);
            chip.setEnabled(!isSoldOut);
            if (isSoldOut) {
                chip.setChipBackgroundColor(android.content.res.ColorStateList.valueOf(android.graphics.Color.parseColor("#EAE7E1")));
                chip.setTextColor(android.graphics.Color.parseColor("#7A7E7C"));
            } else {
                chip.setOnCheckedChangeListener((btn, isChecked) -> {
                    if (isChecked) {
                        selectedSlot = slot;
                        binding.chipGroupDinner.clearCheck();
                    }
                });
            }
            binding.chipGroupLunch.addView(chip);
        }

        // Dinner chips
        binding.chipGroupDinner.removeAllViews();
        for (String slot : dinnerSlots) {
            Chip chip = new Chip(getContext());
            chip.setText(slot);
            boolean isSoldOut = slot.contains("Hết");
            chip.setCheckable(!isSoldOut);
            chip.setEnabled(!isSoldOut);
            if ("19:00".equals(slot)) chip.setChecked(true);

            if (isSoldOut) {
                chip.setChipBackgroundColor(android.content.res.ColorStateList.valueOf(android.graphics.Color.parseColor("#EAE7E1")));
                chip.setTextColor(android.graphics.Color.parseColor("#7A7E7C"));
            } else {
                chip.setOnCheckedChangeListener((btn, isChecked) -> {
                    if (isChecked) {
                        selectedSlot = slot;
                        binding.chipGroupLunch.clearCheck();
                    }
                });
            }
            binding.chipGroupDinner.addView(chip);
        }

        binding.btnContinue.setOnClickListener(v -> {
            draft.setTimeSlot(selectedSlot);
            navigateTo(ResGuestCountFragment.newInstance(draft));
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
