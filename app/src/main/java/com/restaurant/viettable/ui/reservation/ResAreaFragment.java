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
import com.restaurant.viettable.databinding.FragmentResAreaBinding;
import com.restaurant.viettable.model.ReservationDraft;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.Arrays;
import java.util.List;

public class ResAreaFragment extends BaseFragment {

    private static final String ARG_DRAFT = "arg_draft";
    private FragmentResAreaBinding binding;
    private ReservationDraft draft;

    private static class AreaItem {
        String name;
        String desc;
        boolean available;
        AreaItem(String name, String desc, boolean available) {
            this.name = name;
            this.desc = desc;
            this.available = available;
        }
    }

    private final List<AreaItem> areaList = Arrays.asList(
            new AreaItem("Khu vực chung", "Các bàn đang hoạt động của chi nhánh; bước tiếp theo sẽ kiểm tra sức chứa và lịch trống trực tiếp từ máy chủ.", true)
    );
    private int selectedIndex = 0;

    public static ResAreaFragment newInstance(ReservationDraft draft) {
        ResAreaFragment f = new ResAreaFragment();
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
        binding = FragmentResAreaBinding.inflate(inflater, container, false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (draft == null) draft = new ReservationDraft();

        binding.btnBack.setOnClickListener(v -> navigateBack());

        binding.wizardProgress.tvWizardStepName.setText(getString(R.string.res_wizard_step4));
        binding.wizardProgress.tvWizardStepFraction.setText("4/6");
        binding.wizardProgress.progressWizard.setProgress(66);

        renderAreaCards();

        binding.btnContinue.setOnClickListener(v -> {
            draft.setAreaName(areaList.get(selectedIndex).name);
            navigateTo(ResTableLayoutFragment.newInstance(draft));
        });
    }

    private void renderAreaCards() {
        binding.layoutAreaCards.removeAllViews();
        for (int i = 0; i < areaList.size(); i++) {
            final int index = i;
            AreaItem item = areaList.get(i);

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

            TextView tvName = new TextView(requireContext());
            tvName.setText(item.name + (item.available ? " • (Còn bàn)" : " • (Tạm hết)"));
            tvName.setTextSize(16);
            tvName.setTextColor(item.available ? getResources().getColor(R.color.primary) : getResources().getColor(R.color.text_tertiary));
            tvName.setTypeface(null, android.graphics.Typeface.BOLD);
            content.addView(tvName);

            TextView tvDesc = new TextView(requireContext());
            tvDesc.setText(item.desc);
            tvDesc.setTextSize(13);
            tvDesc.setTextColor(getResources().getColor(R.color.text_secondary));
            tvDesc.setPadding(0, 8, 0, 0);
            content.addView(tvDesc);

            card.addView(content);

            if (item.available) {
                card.setOnClickListener(v -> {
                    selectedIndex = index;
                    renderAreaCards();
                });
            } else {
                card.setAlpha(0.6f);
            }

            binding.layoutAreaCards.addView(card);
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
