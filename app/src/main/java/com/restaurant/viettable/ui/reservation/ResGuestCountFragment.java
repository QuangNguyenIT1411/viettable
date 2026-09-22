package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentResGuestCountBinding;
import com.restaurant.viettable.model.ReservationDraft;
import com.restaurant.viettable.ui.base.BaseFragment;

public class ResGuestCountFragment extends BaseFragment {

    private static final String ARG_DRAFT = "arg_draft";
    private FragmentResGuestCountBinding binding;
    private ReservationDraft draft;
    private int adults = 4;
    private int children = 0;

    public static ResGuestCountFragment newInstance(ReservationDraft draft) {
        ResGuestCountFragment f = new ResGuestCountFragment();
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
        binding = FragmentResGuestCountBinding.inflate(inflater, container, false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (draft == null) draft = new ReservationDraft();

        binding.btnBack.setOnClickListener(v -> navigateBack());

        binding.wizardProgress.tvWizardStepName.setText(getString(R.string.res_wizard_step3));
        binding.wizardProgress.tvWizardStepFraction.setText("3/6");
        binding.wizardProgress.progressWizard.setProgress(50);

        binding.tvDateTimeSummary.setText("Thời gian: " + draft.getTimeSlot() + " • " + draft.getDate());

        binding.btnMinusAdult.setOnClickListener(v -> {
            if (adults > 1) {
                adults--;
                updateGuestViews();
            }
        });

        binding.btnPlusAdult.setOnClickListener(v -> {
            adults++;
            updateGuestViews();
        });

        binding.btnMinusChild.setOnClickListener(v -> {
            if (children > 0) {
                children--;
                updateGuestViews();
            }
        });

        binding.btnPlusChild.setOnClickListener(v -> {
            children++;
            updateGuestViews();
        });

        updateGuestViews();

        binding.btnContinue.setOnClickListener(v -> {
            draft.setGuestCount(adults + children);
            navigateTo(ResAreaFragment.newInstance(draft));
        });
    }

    private void updateGuestViews() {
        binding.tvAdultCount.setText(String.valueOf(adults));
        binding.tvChildCount.setText(String.valueOf(children));
        int total = adults + children;
        binding.layoutDepositNotice.setVisibility(total >= 6 ? View.VISIBLE : View.GONE);
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
