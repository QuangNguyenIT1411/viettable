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
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.DepositPolicy;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.util.DepositRules;
import com.restaurant.viettable.util.DisplayFormatters;

public class ResGuestCountFragment extends BaseFragment {

    private DepositPolicy policy = MobileRepository.getInstance().cachedDepositPolicy();

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
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (draft == null) draft = new ReservationDraft();

        binding.btnBack.setOnClickListener(v -> navigateBack());

        binding.wizardProgress.tvWizardStepName.setText(getString(R.string.res_wizard_step3));
        binding.wizardProgress.tvWizardStepFraction.setText("3/5");
        binding.wizardProgress.progressWizard.setProgress(60);

        binding.tvDateTimeSummary.setText("Thời gian: " + draft.getTimeSlot() + " • " + DisplayFormatters.date(draft.getDate()));

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
        MobileRepository.getInstance().loadDepositPolicy(new MobileRepository.Callback<DepositPolicy>() {
            @Override public void onSuccess(DepositPolicy value) { policy = value; if (binding != null) updateGuestViews(); }
            @Override public void onError(ApiException error) { }
        });

        binding.btnContinue.setOnClickListener(v -> {
            draft.setGuestCount(adults + children);
            long deposit = DepositRules.requiredAmount(policy, adults + children);
            draft.setDepositAmount(deposit);
            draft.setNeedsDeposit(deposit > 0);
            navigateTo(ResTableLayoutFragment.newInstance(draft)); // the area step only had "Khu vực chung"
        });
    }

    private void updateGuestViews() {
        binding.tvAdultCount.setText(String.valueOf(adults));
        binding.tvChildCount.setText(String.valueOf(children));
        int total = adults + children;
        String notice = DepositRules.notice(policy, total);
        binding.layoutDepositNotice.setVisibility(notice == null ? View.GONE : View.VISIBLE);
        binding.tvDepositNotice.setText(notice == null ? "" : notice);
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
