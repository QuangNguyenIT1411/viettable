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
import com.restaurant.viettable.util.DisplayFormatters;
import com.restaurant.viettable.util.PhoneRules;
import com.restaurant.viettable.util.SafeText;

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
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (draft == null) draft = new ReservationDraft();

        binding.btnBack.setOnClickListener(v -> navigateBack());

        binding.wizardProgress.tvWizardStepName.setText(getString(R.string.res_wizard_step5));
        binding.wizardProgress.tvWizardStepFraction.setText("5/5");
        binding.wizardProgress.progressWizard.setProgress(100);

        if (draft.getBranch() != null) {
            binding.tvResSummaryHeader.setText(draft.getBranch().getName() + " • " +
                    com.restaurant.viettable.data.MobileRepository.getInstance().tableLabel(draft.getBranch().getId(), draft.getTableId()));
        }
        binding.tvResSummaryDetails.setText(draft.getTimeSlot() + ", " + DisplayFormatters.date(draft.getDate()) + " • " + draft.getGuestCount() + " khách");

        // Contact: what the guest typed before, else the profile; the server uses exactly this number.
        com.restaurant.viettable.model.User user = appStateManager.getCurrentUser();
        binding.etContactName.setText(!draft.getContactName().isEmpty() ? draft.getContactName() : user == null ? "" : SafeText.clean(user.getFullName()));
        binding.etContactPhone.setText(!draft.getContactPhone().isEmpty() ? draft.getContactPhone() : user == null ? "" : SafeText.clean(user.getPhone()));
        binding.etContactPhone.addTextChangedListener(new android.text.TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int start, int count, int after) { }
            @Override public void onTextChanged(CharSequence s, int start, int before, int count) { binding.tilContactPhone.setError(null); }
            @Override public void afterTextChanged(android.text.Editable s) { }
        });

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
            String phone = binding.etContactPhone.getText() != null ? binding.etContactPhone.getText().toString().trim() : "";
            String normalized = PhoneRules.normalize(phone);
            if (!phone.isEmpty() && normalized == null) {
                binding.tilContactPhone.setError(PhoneRules.INVALID_MESSAGE);
                binding.etContactPhone.requestFocus();
                return;
            }
            draft.setContactPhone(normalized == null ? "" : normalized);
            draft.setContactName(binding.etContactName.getText() != null ? binding.etContactName.getText().toString() : "");
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
                    binding.btnContinueOrConfirm.setEnabled(true);
                    // The server names the phone field: show it on that field, not only as a toast.
                    if ("contact_phone_invalid".equals(error.getCode()) || "contact_phone_required".equals(error.getCode())) {
                        binding.tilContactPhone.setError(apiError(error));
                        binding.etContactPhone.requestFocus();
                    } else if ("table_taken".equals(error.getCode())) {
                        // Another guest got this table first: back to the map, which reloads and clears the choice.
                        showLongToast(apiError(error));
                        navigateBack();
                    } else showToast(apiError(error));
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
