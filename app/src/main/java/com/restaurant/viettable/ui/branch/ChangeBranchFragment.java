package com.restaurant.viettable.ui.branch;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.restaurant.viettable.adapter.BranchAdapter;
import com.restaurant.viettable.databinding.FragmentChangeBranchBinding;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.ArrayList;
import java.util.List;

public class ChangeBranchFragment extends BaseFragment {

    private FragmentChangeBranchBinding binding;
    private final List<Branch> otherBranches = new ArrayList<>();
    private BranchAdapter adapter;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentChangeBranchBinding.inflate(inflater, container, false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        Branch current = appStateManager.getSelectedBranch();
        if (current != null) {
            binding.tvCurrentBranchName.setText(current.getName());
            binding.tvCurrentBranchAddress.setText(current.getAddress());
        }

        boolean hasCartItems = !appStateManager.getCartItems().isEmpty();
        binding.layoutCartWarning.setVisibility(hasCartItems ? View.VISIBLE : View.GONE);

        otherBranches.clear();
        for (Branch b : appStateManager.getBranches()) {
            if (current == null || !b.getId().equals(current.getId())) {
                otherBranches.add(b);
            }
        }

        adapter = new BranchAdapter(otherBranches, new BranchAdapter.OnBranchActionListener() {
            @Override
            public void onSelectBranch(Branch branch) {
                if (hasCartItems) {
                    com.restaurant.viettable.ui.common.DialogHelper.showDestructiveDialog(
                            requireContext(),
                            "Xác nhận đổi chi nhánh",
                            "Đổi chi nhánh sẽ xóa các món hiện tại trong giỏ hàng. Bạn có chắc chắn muốn tiếp tục?",
                            "Đổi chi nhánh",
                            (dialog, which) -> {
                                appStateManager.setSelectedBranch(branch);
                                showToast("Đã đổi sang: " + branch.getName());
                                navigateBack();
                            },
                            "Hủy",
                            null
                    );
                } else {
                    appStateManager.setSelectedBranch(branch);
                    showToast("Đã đổi sang: " + branch.getName());
                    navigateBack();
                }
            }

            @Override
            public void onViewDetail(Branch branch) {
                navigateTo(BranchDetailFragment.newInstance(branch));
            }
        });

        binding.rvOtherBranches.setLayoutManager(new LinearLayoutManager(getContext()));
        binding.rvOtherBranches.setAdapter(adapter);
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
