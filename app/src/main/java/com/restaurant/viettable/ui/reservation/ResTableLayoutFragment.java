package com.restaurant.viettable.ui.reservation;

import android.graphics.Color;
import android.os.Bundle;
import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.GridLayout;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.material.card.MaterialCardView;
import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentResTableLayoutBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.ReservationDraft;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.ArrayList;
import java.util.List;

public class ResTableLayoutFragment extends BaseFragment {

    private static final String ARG_DRAFT = "arg_draft";
    private FragmentResTableLayoutBinding binding;
    private ReservationDraft draft;

    private static class TableNode {
        String id;
        int displayNumber;
        int capacity;
        boolean occupied;
        TableNode(String id, int displayNumber, int capacity, boolean occupied) {
            this.id = id;
            this.displayNumber = displayNumber;
            this.capacity = capacity;
            this.occupied = occupied;
        }
    }

    private final List<TableNode> floor1Tables = new ArrayList<>();

    private String selectedTableId;

    public static ResTableLayoutFragment newInstance(ReservationDraft draft) {
        ResTableLayoutFragment f = new ResTableLayoutFragment();
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
        binding = FragmentResTableLayoutBinding.inflate(inflater, container, false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (draft == null) draft = new ReservationDraft();

        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnNoTableSuggestion.setOnClickListener(v -> navigateTo(new ResNoTableFragment()));

        binding.wizardProgress.tvWizardStepName.setText(getString(R.string.res_wizard_step4));
        binding.wizardProgress.tvWizardStepFraction.setText("4/5");
        binding.wizardProgress.progressWizard.setProgress(80);

        binding.tabLayoutFloors.addTab(binding.tabLayoutFloors.newTab().setText("Bàn phù hợp đang còn trống"));

        renderTableGrid();
        binding.btnContinue.setEnabled(false);
        MobileRepository.getInstance().loadAvailability(draft, new MobileRepository.Callback<List<MobileRepository.AvailableTable>>() {
            @Override public void onSuccess(List<MobileRepository.AvailableTable> value) {
                if (binding == null) return;
                floor1Tables.clear();
                for (MobileRepository.AvailableTable table : value)
                    floor1Tables.add(new TableNode(table.tableId, table.tableNumber, table.capacity, false));
                // The guest picks the table (it used to silently pick the first one).
                selectedTableId = null;
                binding.btnContinue.setEnabled(false);
                if (!floor1Tables.isEmpty()) {
                    binding.tvSelectedTableBanner.setText("Chạm vào một bàn để chọn");
                } else {
                    binding.tvSelectedTableBanner.setText("Khung giờ này chưa còn bàn phù hợp");
                }
                renderTableGrid();
            }
            @Override public void onError(ApiException error) {
                if (binding != null) { binding.tvSelectedTableBanner.setText(apiError(error)); showToast(apiError(error)); }
            }
        });

        binding.btnContinue.setOnClickListener(v -> {
            if (selectedTableId == null) { showToast("Vui lòng chọn một bàn còn trống."); return; }
            draft.setTableId(selectedTableId);
            navigateTo(ResNoteFragment.newInstance(draft));
        });
    }

    private void renderTableGrid() {
        binding.gridTables.removeAllViews();
        int screenWidth = getResources().getDisplayMetrics().widthPixels - 96; // padding
        int cardSize = screenWidth / 3;

        for (TableNode node : floor1Tables) {
            boolean isSelected = node.id.equals(selectedTableId);

            MaterialCardView card = new MaterialCardView(requireContext());
            GridLayout.LayoutParams glp = new GridLayout.LayoutParams();
            glp.width = cardSize;
            glp.height = cardSize;
            glp.setMargins(12, 12, 12, 12);
            card.setLayoutParams(glp);
            card.setRadius(20);
            card.setStrokeWidth(isSelected ? 4 : 1);
            card.setStrokeColor(isSelected ? getResources().getColor(R.color.primary) : getResources().getColor(R.color.divider));

            if (node.occupied) {
                card.setCardBackgroundColor(Color.parseColor("#F0EFEA"));
                card.setStrokeColor(Color.parseColor("#DDD9D0"));
                card.setAlpha(0.85f);
            } else if (isSelected) {
                card.setCardBackgroundColor(getResources().getColor(R.color.primary));
                card.setStrokeColor(getResources().getColor(R.color.primary));
                card.setAlpha(1.0f);
            } else {
                card.setCardBackgroundColor(Color.WHITE);
                card.setStrokeColor(getResources().getColor(R.color.divider));
                card.setAlpha(1.0f);
            }

            LinearLayout content = new LinearLayout(requireContext());
            content.setOrientation(LinearLayout.VERTICAL);
            content.setGravity(Gravity.CENTER);

            TextView tvId = new TextView(requireContext());
            tvId.setText("Bàn " + node.displayNumber);
            tvId.setTextSize(16);
            tvId.setTypeface(null, android.graphics.Typeface.BOLD);
            tvId.setTextColor(isSelected ? Color.WHITE : (node.occupied ? Color.parseColor("#5A625D") : getResources().getColor(R.color.text_primary)));
            content.addView(tvId);

            TextView tvCap = new TextView(requireContext());
            tvCap.setText(node.occupied ? "Đã đặt" : node.capacity + " người");
            tvCap.setTextSize(11);
            tvCap.setTextColor(isSelected ? Color.parseColor("#F5F2EC") : (node.occupied ? Color.parseColor("#7E8580") : getResources().getColor(R.color.text_secondary)));
            content.addView(tvCap);

            card.addView(content);

            if (!node.occupied) {
                card.setOnClickListener(v -> {
                    selectedTableId = node.id;
                    binding.tvSelectedTableBanner.setText("Đang chọn: Bàn " + node.displayNumber + " (" + node.capacity + " người)");
                    binding.btnContinue.setEnabled(true);
                    renderTableGrid();
                });
            }

            binding.gridTables.addView(card);
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
