package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.restaurant.viettable.databinding.FragmentResNoTableBinding;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.ArrayList;
import java.util.List;

/** "Hết bàn?": back to the step before the table map, or another real branch. Claims nothing the server did not say. */
public class ResNoTableFragment extends BaseFragment {
    private static final String ARG_CHANGING = "changing";

    private FragmentResNoTableBinding binding;

    /** changingBooking: moving an existing booking, which stays at its branch. */
    public static ResNoTableFragment newInstance(boolean changingBooking) {
        ResNoTableFragment fragment = new ResNoTableFragment();
        Bundle args = new Bundle();
        args.putBoolean(ARG_CHANGING, changingBooking);
        fragment.setArguments(args);
        return fragment;
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
        binding.btnSelectSuggestedTime.setOnClickListener(v -> {
            navigateBack();
            navigateBack();
        });

        List<Branch> others = new ArrayList<>();
        Branch current = appStateManager.getSelectedBranch();
        for (Branch branch : appStateManager.getBranches()) {
            if (current == null || !branch.getId().equals(current.getId())) others.add(branch);
        }
        boolean changing = getArguments() != null && getArguments().getBoolean(ARG_CHANGING);
        if (changing || others.isEmpty()) {
            binding.btnSelectSuggestedBranch.setVisibility(View.GONE);
        } else {
            binding.btnSelectSuggestedBranch.setOnClickListener(v -> {
                String[] names = new String[others.size()];
                for (int i = 0; i < names.length; i++) names[i] = others.get(i).getName();
                new MaterialAlertDialogBuilder(requireContext())
                        .setTitle("Chọn chi nhánh")
                        .setItems(names, (dialog, which) -> {
                            appStateManager.setSelectedBranch(others.get(which));
                            navigateTo(new ResDateFragment());
                        })
                        .setNegativeButton("Đóng", null)
                        .show();
            });
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
