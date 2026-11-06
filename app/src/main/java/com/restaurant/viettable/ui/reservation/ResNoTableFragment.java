package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.material.button.MaterialButton;
import com.restaurant.viettable.R;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentResNoTableBinding;
import com.restaurant.viettable.model.AvailabilityAlternatives;
import com.restaurant.viettable.model.AvailabilityOption;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.model.ReservationDraft;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.DisplayFormatters;

/**
 * "Hết bàn?" (owner 06/11): real alternatives from the server. A time keeps the branch, a branch keeps the time;
 * either one opens the table map already filled in. Moving an existing booking offers times only.
 */
public class ResNoTableFragment extends BaseFragment {
    private static final String ARG_DRAFT = "draft";
    private static final String ARG_CHANGING = "changing";
    private static final String ARG_NOTE = "note";

    private FragmentResNoTableBinding binding;
    private ReservationDraft draft;
    private Reservation changing;
    private String changeNote;

    public static ResNoTableFragment newInstance(ReservationDraft draft, @Nullable Reservation changing, @Nullable String changeNote) {
        ResNoTableFragment fragment = new ResNoTableFragment();
        Bundle args = new Bundle();
        args.putSerializable(ARG_DRAFT, draft);
        args.putSerializable(ARG_CHANGING, changing);
        args.putString(ARG_NOTE, changeNote);
        fragment.setArguments(args);
        return fragment;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            draft = (ReservationDraft) getArguments().getSerializable(ARG_DRAFT);
            changing = (Reservation) getArguments().getSerializable(ARG_CHANGING);
            changeNote = getArguments().getString(ARG_NOTE, "");
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentResNoTableBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        binding.btnBack.setOnClickListener(v -> navigateBack());
        // This screen, then the table map: what is left on top is the step where the time was chosen.
        binding.btnSelectSuggestedTime.setOnClickListener(v -> { navigateBack(); navigateBack(); });
        binding.btnJoinWaitlist.setOnClickListener(v -> openWaitlist());
        if (draft == null || draft.getBranch() == null) { showState("Chưa đủ thông tin đặt bàn."); return; }
        binding.tvNoTableContext.setText(draft.getBranch().getName() + " · " + DisplayFormatters.date(draft.getDate())
                + " · " + draft.getTimeSlot() + " · " + draft.getGuestCount() + " khách");
        load();
    }

    private void load() {
        binding.progressSuggestions.setVisibility(View.VISIBLE);
        binding.tvSuggestionState.setVisibility(View.GONE);
        MobileRepository.getInstance().loadAvailabilityAlternatives(draft, changing == null ? null : changing.getId(),
                new MobileRepository.Callback<AvailabilityAlternatives>() {
                    @Override public void onSuccess(AvailabilityAlternatives value) { if (binding != null) show(value); }
                    @Override public void onError(ApiException error) {
                        if (binding == null) return;
                        binding.progressSuggestions.setVisibility(View.GONE);
                        showState(apiError(error));
                    }
                });
    }

    private void show(AvailabilityAlternatives value) {
        binding.progressSuggestions.setVisibility(View.GONE);
        binding.tvSuggestionState.setVisibility(View.GONE); // an older error must not stay above fresh suggestions
        binding.layoutTimeOptions.removeAllViews();
        binding.layoutBranchOptions.removeAllViews();
        binding.tvTimeHeading.setVisibility(value.times.isEmpty() ? View.GONE : View.VISIBLE);
        for (AvailabilityOption option : value.times) binding.layoutTimeOptions.addView(optionButton(option));
        boolean branches = changing == null && !value.branches.isEmpty();
        binding.tvBranchHeading.setVisibility(branches ? View.VISIBLE : View.GONE);
        binding.tvBranchHeading.setText("CHI NHÁNH KHÁC LÚC " + draft.getTimeSlot());
        if (branches) for (AvailabilityOption option : value.branches) binding.layoutBranchOptions.addView(optionButton(option));
        if (value.available) showState("Khung giờ này vẫn còn bàn. Quay lại sơ đồ để chọn bàn.");
        else if (value.times.isEmpty() && (!branches)) showState("Chưa còn chỗ phù hợp trong ngày này. Bạn có thể chọn ngày khác hoặc vào danh sách chờ.");
        binding.btnJoinWaitlist.setVisibility(changing == null && !value.available ? View.VISIBLE : View.GONE);
    }

    private MaterialButton optionButton(AvailabilityOption option) {
        MaterialButton button = new MaterialButton(requireContext(), null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
        button.setText(option.label());
        button.setAllCaps(false);
        button.setGravity(Gravity.START | Gravity.CENTER_VERTICAL);
        button.setMinHeight((int) (56 * getResources().getDisplayMetrics().density));
        button.setTextColor(requireContext().getColor(R.color.pd_text));
        button.setStrokeColorResource(R.color.pd_line);
        button.setIconResource(R.drawable.ic_arrow_forward);
        button.setIconGravity(MaterialButton.ICON_GRAVITY_END);
        button.setIconTintResource(R.color.pd_yellow_text);
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        params.topMargin = (int) (10 * getResources().getDisplayMetrics().density);
        button.setLayoutParams(params);
        button.setOnClickListener(v -> choose(option));
        return button;
    }

    /** Fills a copy of the booking with the alternative and opens the table map for it. */
    private void choose(AvailabilityOption option) {
        ReservationDraft next = draft.copy();
        if (option.localDate != null && !option.localDate.isEmpty()) next.setDate(option.localDate);
        next.setTimeSlot(option.localTime);
        next.setTableId(null);
        if (option.isOtherBranch()) {
            Branch branch = appStateManager.findBranch(option.branchId);
            if (branch == null) { showToast("Không tìm thấy chi nhánh này."); return; }
            next.setBranch(branch);
        }
        navigateBack();
        navigateBack();
        navigateTo(changing == null ? ResTableLayoutFragment.newInstance(next)
                : ResTableLayoutFragment.forChange(changing, next.getDate(), next.getTimeSlot(), next.getGuestCount(), changeNote));
    }

    private void openWaitlist() {
        navigateTo(WaitlistJoinFragment.newInstance(draft));
    }

    private void showState(String text) {
        binding.tvSuggestionState.setVisibility(View.VISIBLE);
        binding.tvSuggestionState.setText(text);
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
