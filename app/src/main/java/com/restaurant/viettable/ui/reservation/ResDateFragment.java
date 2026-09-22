package com.restaurant.viettable.ui.reservation;

import android.graphics.Color;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.material.card.MaterialCardView;
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

        draft = new ReservationDraft();
        draft.setBranch(appStateManager.getSelectedBranch());
        DateTimeFormatter formatter = DateTimeFormatter.ofPattern("EEEE, dd/MM/yyyy", new Locale("vi", "VN"));
        for (int offset = 1; offset <= 7; offset++)
            dateOptions.add(LocalDate.now().plusDays(offset).format(formatter));

        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnHistoryShortcut.setOnClickListener(v -> navigateTo(new ResHistoryFragment()));

        // Wizard progress
        binding.wizardProgress.tvWizardStepName.setText(getString(R.string.res_wizard_step1));
        binding.wizardProgress.tvWizardStepFraction.setText("1/6");
        binding.wizardProgress.progressWizard.setProgress(16);

        if (draft.getBranch() != null) {
            binding.tvBranchHeader.setText("Chi nhánh: " + draft.getBranch().getName());
        }

        renderDateCards();

        binding.btnContinue.setOnClickListener(v -> {
            draft.setDate(dateOptions.get(selectedIndex));
            navigateTo(ResTimeFragment.newInstance(draft));
        });
    }

    private void renderDateCards() {
        binding.layoutDateCards.removeAllViews();
        for (int i = 0; i < dateOptions.size(); i++) {
            final int index = i;
            String date = dateOptions.get(i);

            MaterialCardView card = new MaterialCardView(requireContext());
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT
            );
            lp.setMargins(0, 0, 0, 24);
            card.setLayoutParams(lp);
            card.setRadius(24);
            card.setCardElevation(index == selectedIndex ? 6 : 2);
            card.setStrokeWidth(index == selectedIndex ? 4 : 1);
            card.setStrokeColor(index == selectedIndex ? getResources().getColor(R.color.primary) : getResources().getColor(R.color.divider));
            card.setCardBackgroundColor(index == selectedIndex ? getResources().getColor(R.color.primary_container) : Color.WHITE);

            LinearLayout content = new LinearLayout(requireContext());
            content.setOrientation(LinearLayout.VERTICAL);
            content.setPadding(36, 28, 36, 28);

            TextView tv = new TextView(requireContext());
            tv.setText(date);
            tv.setTextSize(15);
            tv.setTextColor(getResources().getColor(R.color.text_primary));
            tv.setTypeface(null, index == selectedIndex ? android.graphics.Typeface.BOLD : android.graphics.Typeface.NORMAL);
            content.addView(tv);

            card.addView(content);
            card.setOnClickListener(v -> {
                selectedIndex = index;
                renderDateCards();
            });

            binding.layoutDateCards.addView(card);
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
