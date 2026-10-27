package com.restaurant.viettable.ui.reservation;

import android.graphics.Color;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.content.ContextCompat;
import androidx.core.widget.TextViewCompat;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentResDateBinding;
import com.restaurant.viettable.model.ReservationDraft;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.time.LocalDate;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

public class ResDateFragment extends BaseFragment {

    public static ResDateFragment newInstance() {
        return new ResDateFragment();
    }

    private FragmentResDateBinding binding;
    private ReservationDraft draft;
    private final List<String> dateOptions = new ArrayList<>();
    private final List<LocalDate> dayOptions = new ArrayList<>();
    private int selectedIndex = 0;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentResDateBinding.inflate(inflater, container, false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        // Booking tab root: the floating navigation bar stays (the later steps hide it and Back returns here).
        setBottomNavVisible(true);

        draft = new ReservationDraft();
        draft.setBranch(appStateManager.getSelectedBranch());
        DateTimeFormatter formatter = DateTimeFormatter.ofPattern("EEEE, dd/MM/yyyy", new Locale("vi", "VN"));
        // Today too, while it still has a bookable slot (it used to start tomorrow).
        dateOptions.clear(); // the view is rebuilt on Back; the list must not grow
        dayOptions.clear();
        for (LocalDate day : com.restaurant.viettable.util.ReservationSchedule.bookableDays(java.time.LocalDateTime.now())) {
            dateOptions.add(day.format(formatter));
            dayOptions.add(day);
        }

        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnHistoryShortcut.setOnClickListener(v -> navigateTo(new ResHistoryFragment()));

        // Wizard progress
        binding.wizardProgress.tvWizardStepName.setText(getString(R.string.res_wizard_step1));
        binding.wizardProgress.tvWizardStepFraction.setText("1/5");
        binding.wizardProgress.progressWizard.setProgress(20);

        if (draft.getBranch() != null) {
            binding.tvBranchHeader.setText("Chi nhánh: " + draft.getBranch().getName());
        }

        renderDateCards();

        binding.btnContinue.setOnClickListener(v -> {
            draft.setDate(dateOptions.get(selectedIndex));
            navigateTo(ResTimeFragment.newInstance(draft));
        });
    }

    /** Day chips (DESIGN_PHO_DEM.md section 6 "Đặt bàn"): "Hôm nay"/weekday, day number, month; selected = yellow. */
    private void renderDateCards() {
        binding.layoutDateCards.removeAllViews();
        float density = getResources().getDisplayMetrics().density;
        LocalDate today = LocalDate.now();
        for (int i = 0; i < dayOptions.size(); i++) {
            final int index = i;
            LocalDate day = dayOptions.get(i);
            boolean selected = index == selectedIndex;
            int text = ContextCompat.getColor(requireContext(), selected ? R.color.pd_on_color : R.color.pd_cream);
            int sub = ContextCompat.getColor(requireContext(), selected ? R.color.pd_on_color : R.color.pd_muted);

            LinearLayout chip = new LinearLayout(requireContext());
            chip.setOrientation(LinearLayout.VERTICAL);
            chip.setGravity(android.view.Gravity.CENTER);
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            lp.setMarginEnd(Math.round(10 * density));
            chip.setLayoutParams(lp);
            chip.setMinimumWidth(Math.round(76 * density));
            int padH = Math.round(14 * density), padV = Math.round(14 * density);
            chip.setPadding(padH, padV, padH, padV);
            chip.setBackgroundResource(selected ? R.drawable.pd_day_chip_selected : R.drawable.pd_day_chip_idle);

            TextView label = new TextView(requireContext());
            TextViewCompat.setTextAppearance(label, R.style.TextAppearance_VietTable_Label);
            label.setTextColor(sub);
            label.setText(day.equals(today) ? "HÔM NAY" : weekdayShort(day));
            chip.addView(label);

            TextView number = new TextView(requireContext());
            TextViewCompat.setTextAppearance(number, R.style.TextAppearance_VietTable_Display_L);
            number.setTextColor(text);
            number.setText(String.valueOf(day.getDayOfMonth()));
            chip.addView(number);

            TextView month = new TextView(requireContext());
            TextViewCompat.setTextAppearance(month, R.style.TextAppearance_VietTable_Caption);
            month.setTextColor(sub);
            month.setText("Th" + day.getMonthValue());
            chip.addView(month);

            chip.setContentDescription(dateOptions.get(i));
            chip.setOnClickListener(v -> {
                selectedIndex = index;
                renderDateCards();
            });
            binding.layoutDateCards.addView(chip);
        }
        if (selectedIndex < dateOptions.size()) binding.tvSelectedDateFull.setText(dateOptions.get(selectedIndex));
    }

    private static String weekdayShort(LocalDate day) {
        switch (day.getDayOfWeek()) {
            case MONDAY: return "T2";
            case TUESDAY: return "T3";
            case WEDNESDAY: return "T4";
            case THURSDAY: return "T5";
            case FRIDAY: return "T6";
            case SATURDAY: return "T7";
            default: return "CN";
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
