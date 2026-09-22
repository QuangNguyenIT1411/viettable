package com.restaurant.viettable.ui.home;

import android.os.Bundle;
import android.text.Editable;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.view.inputmethod.EditorInfo;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.material.chip.Chip;
import com.restaurant.viettable.databinding.FragmentSearchBinding;
import com.restaurant.viettable.model.Category;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

public class SearchFragment extends BaseFragment {

    private FragmentSearchBinding binding;
    private final List<String> recentKeywords = new ArrayList<>(Arrays.asList("Phở bò tái lăn", "Bún chả", "Lẩu riêu cua", "Trà sen"));
    private final List<String> popularKeywords = Arrays.asList("Phở bò", "Bún chả", "Nem rán", "Chả cá", "Lẩu cua", "Cơm tấm", "Cà phê trứng");

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentSearchBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        binding.etSearchQuery.addTextChangedListener(new TextWatcher() {
            @Override
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {}

            @Override
            public void onTextChanged(CharSequence s, int start, int before, int count) {
                binding.btnClearQuery.setVisibility(s.length() > 0 ? View.VISIBLE : View.GONE);
            }

            @Override
            public void afterTextChanged(Editable s) {}
        });

        binding.btnClearQuery.setOnClickListener(v -> binding.etSearchQuery.setText(""));

        binding.btnDoSearch.setOnClickListener(v -> submitSearch(binding.etSearchQuery.getText().toString().trim()));

        binding.etSearchQuery.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == EditorInfo.IME_ACTION_SEARCH) {
                submitSearch(binding.etSearchQuery.getText().toString().trim());
                return true;
            }
            return false;
        });

        binding.btnClearHistory.setOnClickListener(v -> {
            recentKeywords.clear();
            populateRecentChips();
            showToast("Đã xóa lịch sử tìm kiếm.");
        });

        populateRecentChips();
        populatePopularChips();
        populateCategoryChips();
    }

    private void submitSearch(String query) {
        if (TextUtils.isEmpty(query)) {
            showToast("Vui lòng nhập từ khóa tìm kiếm.");
            return;
        }
        if (!recentKeywords.contains(query)) {
            recentKeywords.add(0, query);
        }
        navigateTo(SearchResultFragment.newInstance(query));
    }

    private void populateRecentChips() {
        binding.chipGroupRecent.removeAllViews();
        for (String kw : recentKeywords) {
            Chip chip = new Chip(getContext());
            chip.setText(kw);
            chip.setOnClickListener(v -> {
                binding.etSearchQuery.setText(kw);
                submitSearch(kw);
            });
            binding.chipGroupRecent.addView(chip);
        }
    }

    private void populatePopularChips() {
        binding.chipGroupPopular.removeAllViews();
        for (String kw : popularKeywords) {
            Chip chip = new Chip(getContext());
            chip.setText(kw);
            chip.setOnClickListener(v -> {
                binding.etSearchQuery.setText(kw);
                submitSearch(kw);
            });
            binding.chipGroupPopular.addView(chip);
        }
    }

    private void populateCategoryChips() {
        binding.chipGroupCategories.removeAllViews();
        for (Category cat : appStateManager.getCategories()) {
            Chip chip = new Chip(getContext());
            chip.setText(cat.getName());
            chip.setOnClickListener(v -> {
                binding.etSearchQuery.setText(cat.getName());
                submitSearch(cat.getName());
            });
            binding.chipGroupCategories.addView(chip);
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
