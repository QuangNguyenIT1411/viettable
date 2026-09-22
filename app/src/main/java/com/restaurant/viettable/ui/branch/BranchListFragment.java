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
        binding.btnOpenMap.setOnClickListener(v -> navigateTo(new BranchMapFragment()));

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

        MobileRepository.getInstance().loadBranches(new MobileRepository.Callback<List<Branch>>() {
            @Override public void onSuccess(List<Branch> value) {
                if (binding == null) return;
                displayedBranches.clear();
                displayedBranches.addAll(value);
                adapter.notifyDataSetChanged();
            }
            @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
        });

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

    private void filterBranches(String query) {
        displayedBranches.clear();
        for (Branch b : appStateManager.getBranches()) {
            if (b.getName().toLowerCase().contains(query.toLowerCase()) ||
                    b.getAddress().toLowerCase().contains(query.toLowerCase())) {
                displayedBranches.add(b);
            }
        }
        adapter.notifyDataSetChanged();
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
