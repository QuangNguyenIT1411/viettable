package com.restaurant.viettable.ui.branch;

import android.os.Bundle;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.restaurant.viettable.R;
import com.restaurant.viettable.adapter.BranchAdapter;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentBranchListBinding;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.home.HomeFragment;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

public class BranchListFragment extends BaseFragment {

    private FragmentBranchListBinding binding;
    private final List<Branch> displayedBranches = new ArrayList<>();
    private BranchAdapter adapter;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentBranchListBinding.inflate(inflater, container, false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnOpenMap.setVisibility(View.GONE);

        displayedBranches.clear();
        displayedBranches.addAll(appStateManager.getBranches());

        adapter = new BranchAdapter(displayedBranches, new BranchAdapter.OnBranchActionListener() {
            @Override
            public void onSelectBranch(Branch branch) {
                appStateManager.setSelectedBranch(branch);
                MobileRepository.getInstance().loadMenu(branch.getId(), new MobileRepository.Callback<Void>() {
                    @Override public void onSuccess(Void value) {
                        showToast("Đã chọn: " + branch.getName());
                        setBottomNavVisible(true);
                        switchTab(R.id.nav_home);
                    }
                    @Override public void onError(ApiException error) { showToast(apiError(error)); }
                });
            }

            @Override
            public void onViewDetail(Branch branch) {
                navigateTo(BranchDetailFragment.newInstance(branch));
            }
        });

        binding.rvBranches.setLayoutManager(new LinearLayoutManager(getContext()));
        binding.rvBranches.setAdapter(adapter);
        binding.btnRetryBranches.setOnClickListener(v -> loadBranches());
        renderBranchState(displayedBranches.isEmpty(), false, null);
        loadBranches();

        binding.etSearchBranch.addTextChangedListener(new TextWatcher() {
            @Override
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {}

            @Override
            public void onTextChanged(CharSequence s, int start, int before, int count) {
                filterBranches(s.toString());
            }

            @Override
            public void afterTextChanged(Editable s) {}
        });
    }

    private void loadBranches() {
        if (binding == null) return;
        renderBranchState(displayedBranches.isEmpty(), true, null);
        MobileRepository.getInstance().loadBranches(new MobileRepository.Callback<List<Branch>>() {
            @Override public void onSuccess(List<Branch> value) {
                if (binding == null) return;
                filterBranches(binding.etSearchBranch.getText() == null
                        ? "" : binding.etSearchBranch.getText().toString());
            }
            @Override public void onError(ApiException error) {
                if (binding == null) return;
                renderBranchState(displayedBranches.isEmpty(), false, apiError(error));
            }
        });
    }

    private void filterBranches(String query) {
        displayedBranches.clear();
        String normalizedQuery = query == null ? "" : query.trim().toLowerCase(Locale.ROOT);
        for (Branch b : appStateManager.getBranches()) {
            String name = b.getName() == null ? "" : b.getName().toLowerCase(Locale.ROOT);
            String address = b.getAddress() == null ? "" : b.getAddress().toLowerCase(Locale.ROOT);
            if (name.contains(normalizedQuery) || address.contains(normalizedQuery)) {
                displayedBranches.add(b);
            }
        }
        adapter.notifyDataSetChanged();
        if (binding != null) renderBranchState(displayedBranches.isEmpty(), false, null);
    }

    private void renderBranchState(boolean empty, boolean loading, @Nullable String error) {
        if (binding == null) return;
        boolean hasContent = !displayedBranches.isEmpty();
        binding.rvBranches.setVisibility(hasContent ? View.VISIBLE : View.GONE);
        binding.progressBranches.setVisibility(loading && !hasContent ? View.VISIBLE : View.GONE);
        binding.tvBranchState.setVisibility(!loading && empty ? View.VISIBLE : View.GONE);
        binding.btnRetryBranches.setVisibility(!loading && error != null ? View.VISIBLE : View.GONE);
        if (!loading && empty) {
            binding.tvBranchState.setText(error != null ? error : getString(R.string.state_no_branches));
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
