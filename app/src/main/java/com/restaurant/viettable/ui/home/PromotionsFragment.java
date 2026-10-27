package com.restaurant.viettable.ui.home;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.restaurant.viettable.adapter.PromotionAdapter;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentPromotionsBinding;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.Promotion;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.common.ListState;
import com.restaurant.viettable.util.PdMotion;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/** "Ưu đãi" (docs/PROMO_PLAN.md): promotions running now at the selected branch; tap → detail. */
public class PromotionsFragment extends BaseFragment {

    private FragmentPromotionsBinding binding;
    private final List<Promotion> items = new ArrayList<>();
    private PromotionAdapter adapter;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentPromotionsBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        binding.btnBack.setOnClickListener(v -> navigateBack());
        Branch branch = appStateManager.getSelectedBranch();
        binding.tvPromotionsBranch.setText(("Ưu đãi" + (branch == null ? "" : " · " + branch.getName())).toUpperCase(Locale.forLanguageTag("vi")));
        adapter = new PromotionAdapter(items, p -> navigateTo(PromotionDetailFragment.newInstance(p)));
        binding.rvPromotions.setLayoutManager(new LinearLayoutManager(requireContext()));
        PdMotion.staggerCards(binding.rvPromotions);
        binding.rvPromotions.setAdapter(adapter);
        binding.btnRetryPromotions.setOnClickListener(v -> load());
        load();
    }

    private void load() {
        if (binding == null) return;
        render(true, null);
        Branch branch = appStateManager.getSelectedBranch();
        MobileRepository.getInstance().loadPromotions(branch == null ? null : branch.getId(), new MobileRepository.Callback<List<Promotion>>() {
            @Override public void onSuccess(List<Promotion> value) {
                if (binding == null) return;
                items.clear();
                items.addAll(value);
                adapter.notifyDataSetChanged();
                render(false, null);
            }
            @Override public void onError(ApiException error) { if (binding != null) render(false, apiError(error)); }
        });
    }

    private void render(boolean loading, @Nullable String error) {
        ListState.render(binding.rvPromotions, binding.progressPromotions, binding.tvPromotionsState, binding.btnRetryPromotions,
                !items.isEmpty(), loading, error, "Chi nhánh này hiện chưa có khuyến mãi nào. Quay lại sau nhé!");
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
