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
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.R;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.ArrayList;
import java.util.List;

public class ChangeBranchFragment extends BaseFragment implements AppStateManager.StateChangeListener {

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
        appStateManager.addListener(this);

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
        renderBranches();
        binding.btnRetryBranchChoice.setOnClickListener(v -> loadBranches());
        if (appStateManager.getBranches().isEmpty()) loadBranches();
    }

    private void loadBranches() {
        if (binding == null) return;
        binding.tvBranchChoiceState.setText(R.string.catalog_loading);
        binding.tvBranchChoiceState.setVisibility(View.VISIBLE);
        binding.btnRetryBranchChoice.setVisibility(View.GONE);
        MobileRepository.getInstance().loadBranches(new MobileRepository.Callback<List<Branch>>() {
            @Override public void onSuccess(List<Branch> value) { renderBranches(); }
            @Override public void onError(ApiException error) {
                if (binding == null) return;
                binding.tvBranchChoiceState.setText(apiError(error));
                binding.tvBranchChoiceState.setVisibility(View.VISIBLE);
                binding.btnRetryBranchChoice.setVisibility(View.VISIBLE);
            }
        });
    }

    private void renderBranches() {
        if (binding == null || adapter == null) return;
        Branch current = appStateManager.getSelectedBranch();
        binding.tvCurrentBranchName.setText(current == null ? getString(R.string.catalog_select_branch) : current.getName());
        binding.tvCurrentBranchAddress.setText(current == null ? "" : current.getAddress());
        otherBranches.clear();
        for (Branch branch : appStateManager.getBranches())
            if (current == null || !branch.getId().equals(current.getId())) otherBranches.add(branch);
        adapter.notifyDataSetChanged();
        binding.tvBranchChoiceState.setVisibility(otherBranches.isEmpty() ? View.VISIBLE : View.GONE);
        binding.tvBranchChoiceState.setText(R.string.state_no_branches);
        binding.btnRetryBranchChoice.setVisibility(otherBranches.isEmpty() ? View.VISIBLE : View.GONE);
    }

    @Override public void onStateChanged() { renderBranches(); }

    @Override
    public void onDestroyView() {
        appStateManager.removeListener(this);
        super.onDestroyView();
        binding = null;
    }
}
