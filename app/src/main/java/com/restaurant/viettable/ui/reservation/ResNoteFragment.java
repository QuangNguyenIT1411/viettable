package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.material.chip.Chip;
import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentResNoteBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.model.ReservationDraft;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

public class ResNoteFragment extends BaseFragment {

    private static final String ARG_DRAFT = "arg_draft";
    private FragmentResNoteBinding binding;
    private ReservationDraft draft;
    private final List<String> specialTags = Arrays.asList("Sinh nhật", "Kỷ niệm", "Tiếp khách", "Cầu hôn", "Có trẻ nhỏ", "Ghế trẻ em");
    private final List<String> selectedTags = new ArrayList<>();

    public static ResNoteFragment newInstance(ReservationDraft draft) {
        ResNoteFragment f = new ResNoteFragment();
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
        binding = FragmentResNoteBinding.inflate(inflater, container, false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (draft == null) draft = new ReservationDraft();

        binding.btnBack.setOnClickListener(v -> navigateBack());

        binding.wizardProgress.tvWizardStepName.setText(getString(R.string.res_wizard_step6));
        binding.wizardProgress.tvWizardStepFraction.setText("6/6");
        binding.wizardProgress.progressWizard.setProgress(100);

        if (draft.getBranch() != null) {
            binding.tvResSummaryHeader.setText(draft.getBranch().getName() + " • Bàn " + draft.getTableId());
        }
        binding.tvResSummaryDetails.setText(draft.getTimeSlot() + ", " + draft.getDate() + " • " + draft.getGuestCount() + " khách • " + draft.getAreaName());

        if (draft.isNeedsDeposit()) {
            binding.btnContinueOrConfirm.setText("Tiếp tục đặt cọc (100.000đ)");
        } else {
            binding.btnContinueOrConfirm.setText("Xác nhận đặt bàn ngay");
        }

        binding.chipGroupSpecialTags.removeAllViews();
        for (String tag : specialTags) {
            Chip chip = new Chip(getContext());
            chip.setText(tag);
            chip.setCheckable(true);
            chip.setOnCheckedChangeListener((btn, isChecked) -> {
                if (isChecked) selectedTags.add(tag);
                else selectedTags.remove(tag);
            });
            binding.chipGroupSpecialTags.addView(chip);
        }

        binding.btnContinueOrConfirm.setOnClickListener(v -> {
            String note = binding.etReservationNote.getText() != null ? binding.etReservationNote.getText().toString().trim() : "";
            draft.setNote(note);
            draft.setSpecialTags(selectedTags);

            binding.btnContinueOrConfirm.setEnabled(false);
            MobileRepository.getInstance().createReservation(draft, new MobileRepository.Callback<Reservation>() {
                @Override public void onSuccess(Reservation value) {
                    if (binding == null) return;
                    binding.btnContinueOrConfirm.setEnabled(true);
                    navigateTo(ResConfirmationFragment.newInstance(value));
                }
                @Override public void onError(ApiException error) {
                    if (binding == null) return;
                    binding.btnContinueOrConfirm.setEnabled(true); showToast(apiError(error));
                }
            });
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
