package com.restaurant.viettable.ui.reservation;

import android.graphics.Color;
import android.graphics.Typeface;
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
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentResTableLayoutBinding;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.model.ReservationDraft;
import com.restaurant.viettable.model.TableMapItem;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.DisplayFormatters;
import com.restaurant.viettable.util.TableMapRules;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;

/**
 * "Sơ đồ & Chọn bàn" (reservation item 5): every table of the branch for the chosen time, grouped by seats, with
 * its state from the server. Nothing is chosen for the guest; Continue stays off until a free table is picked.
 * Also used by "Đổi đặt bàn": the booking's own table is kept selected while it is still free.
 */
public class ResTableLayoutFragment extends BaseFragment {

    private static final String ARG_DRAFT = "arg_draft";
    private static final String ARG_CHANGE = "arg_change_reservation";
    private static final String ARG_NOTE = "arg_change_note";
    private static final String STATE_SELECTED = "selectedTableId";

    private FragmentResTableLayoutBinding binding;
    private ReservationDraft draft;
    private Reservation changing;   // null for a new booking
    private String changeNote;
    private final List<TableMapItem> tables = new ArrayList<>();
    private String selectedTableId;
    private boolean loaded;
    private boolean saving;

    public static ResTableLayoutFragment newInstance(ReservationDraft draft) {
        ResTableLayoutFragment f = new ResTableLayoutFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_DRAFT, draft);
        f.setArguments(b);
        return f;
    }

    /** Change an existing booking to this day, time and party size; the guest confirms the table here. */
    public static ResTableLayoutFragment forChange(Reservation reservation, String date, String time, int guests, String note) {
        ReservationDraft draft = new ReservationDraft();
        draft.setDate(date);
        draft.setTimeSlot(time);
        draft.setGuestCount(guests);
        ResTableLayoutFragment f = new ResTableLayoutFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_DRAFT, draft);
        b.putSerializable(ARG_CHANGE, reservation);
        b.putString(ARG_NOTE, note);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            draft = (ReservationDraft) getArguments().getSerializable(ARG_DRAFT);
            changing = (Reservation) getArguments().getSerializable(ARG_CHANGE);
            changeNote = getArguments().getString(ARG_NOTE, "");
        }
        if (savedInstanceState != null) selectedTableId = savedInstanceState.getString(STATE_SELECTED);
        else if (changing != null && changing.getTableId() != null && !changing.getTableId().isEmpty()) selectedTableId = changing.getTableId();
    }

    @Override
    public void onSaveInstanceState(@NonNull Bundle outState) {
        super.onSaveInstanceState(outState);
        outState.putString(STATE_SELECTED, selectedTableId);
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentResTableLayoutBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        if (draft == null) draft = new ReservationDraft();
        if (changing != null && draft.getBranch() == null) {
            Branch branch = appStateManager.findBranch(changing.getBranchId());
            draft.setBranch(branch);
        }

        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnNoTableSuggestion.setOnClickListener(v -> navigateTo(new ResNoTableFragment()));
        if (changing != null) {
            binding.tvTableMapTitle.setText("Đổi đặt bàn: chọn bàn");
            binding.wizardProgress.getRoot().setVisibility(View.GONE);
            binding.btnContinue.setText("Lưu thay đổi");
        } else {
            binding.wizardProgress.tvWizardStepName.setText(getString(R.string.res_wizard_step4));
            binding.wizardProgress.tvWizardStepFraction.setText("4/5");
            binding.wizardProgress.progressWizard.setProgress(80);
        }
        binding.tvTableMapContext.setText(draft.getTimeSlot() + " • " + DisplayFormatters.date(draft.getDate())
                + " • " + draft.getGuestCount() + " khách");

        binding.btnContinue.setOnClickListener(v -> onContinue());
        loadMap(null);
    }

    /** (Re)loads the map; notice is shown first (e.g. the chosen table was just taken). */
    private void loadMap(@Nullable String notice) {
        if (binding == null) return;
        loaded = false;
        binding.progressTableMap.setVisibility(tables.isEmpty() ? View.VISIBLE : View.GONE);
        updateBanner(notice);
        MobileRepository.getInstance().loadTableMap(draft, changing == null ? null : changing.getId(),
                new MobileRepository.Callback<List<TableMapItem>>() {
                    @Override public void onSuccess(List<TableMapItem> value) {
                        if (binding == null) return;
                        tables.clear();
                        tables.addAll(value);
                        selectedTableId = TableMapRules.keepSelection(selectedTableId, tables);
                        loaded = true;
                        binding.progressTableMap.setVisibility(View.GONE);
                        updateBanner(notice);
                        render();
                    }
                    @Override public void onError(ApiException error) {
                        if (binding == null) return;
                        binding.progressTableMap.setVisibility(View.GONE);
                        binding.tvSelectedTableBanner.setText(apiError(error));
                        showToast(apiError(error));
                    }
                });
    }

    private void updateBanner(@Nullable String notice) {
        TableMapItem selected = selected();
        String text;
        if (selected != null) text = "Đang chọn: Bàn " + selected.tableNumber + " (" + selected.seats + " người)";
        else if (!loaded) text = "Đang tải sơ đồ bàn…";
        else if (TableMapRules.availableCount(tables) == 0) text = "Khung giờ này chưa còn bàn phù hợp. Bấm \"Hết bàn?\" để xem gợi ý.";
        else text = "Chạm vào một bàn trống để chọn";
        binding.tvSelectedTableBanner.setText(notice == null ? text : notice + "\n" + text);
        binding.btnContinue.setEnabled(selected != null && !saving);
    }

    @Nullable
    private TableMapItem selected() {
        if (selectedTableId == null) return null;
        for (TableMapItem t : tables) if (t.tableId.equals(selectedTableId)) return t;
        return null;
    }

    private void onContinue() {
        TableMapItem selected = selected();
        if (selected == null || !selected.selectable()) { showToast("Vui lòng chọn một bàn còn trống."); return; }
        if (changing == null) {
            draft.setTableId(selected.tableId);
            navigateTo(ResNoteFragment.newInstance(draft));
            return;
        }
        saving = true;
        updateBanner(null);
        MobileRepository.getInstance().updateReservation(changing, draft.getDate(), draft.getTimeSlot(), draft.getGuestCount(),
                changeNote, selected.tableId, new MobileRepository.Callback<Reservation>() {
                    @Override public void onSuccess(Reservation value) {
                        saving = false;
                        if (binding == null) return;
                        showToast("Đã cập nhật thay đổi đặt bàn thành công!");
                        // Back to the reservation detail: leave this map and the change form.
                        getParentFragmentManager().popBackStack();
                        getParentFragmentManager().popBackStack();
                    }
                    @Override public void onError(ApiException error) {
                        saving = false;
                        if (binding == null) return;
                        if ("table_taken".equals(error.getCode())) {
                            showLongToast(apiError(error));
                            loadMap(apiError(error));
                        } else {
                            showToast(apiError(error));
                            updateBanner(null);
                        }
                    }
                });
    }

    private void render() {
        LinearLayout container = binding.containerTableGroups;
        container.removeAllViews();
        int columns = 3;
        int margin = dp(6);
        int width = getResources().getDisplayMetrics().widthPixels - dp(32);
        int cardSize = width / columns - 2 * margin;
        for (Map.Entry<String, List<TableMapItem>> group : TableMapRules.grouped(tables).entrySet()) {
            TextView header = new TextView(requireContext());
            header.setText(group.getKey());
            header.setTextSize(15);
            header.setTypeface(null, Typeface.BOLD);
            header.setTextColor(getResources().getColor(R.color.text_primary));
            header.setPadding(0, dp(12), 0, dp(4));
            container.addView(header);

            GridLayout grid = new GridLayout(requireContext());
            grid.setColumnCount(columns);
            for (TableMapItem table : group.getValue()) grid.addView(card(table, cardSize, margin));
            container.addView(grid);
        }
    }

    private View card(TableMapItem table, int size, int margin) {
        boolean isSelected = table.tableId.equals(selectedTableId);
        boolean booked = TableMapItem.BOOKED.equals(table.status);
        boolean free = table.selectable();

        MaterialCardView card = new MaterialCardView(requireContext());
        GridLayout.LayoutParams lp = new GridLayout.LayoutParams();
        lp.width = size;
        lp.height = size;
        lp.setMargins(margin, margin, margin, margin);
        card.setLayoutParams(lp);
        card.setRadius(dp(10));
        card.setStrokeWidth(isSelected ? dp(2) : 1);
        int primary = getResources().getColor(R.color.primary);
        if (isSelected) {
            card.setCardBackgroundColor(primary);
            card.setStrokeColor(primary);
        } else if (free) {
            card.setCardBackgroundColor(Color.WHITE);
            card.setStrokeColor(getResources().getColor(R.color.divider));
        } else if (booked) {
            card.setCardBackgroundColor(Color.parseColor("#D1D5DB"));
            card.setStrokeColor(Color.parseColor("#C2C7CE"));
        } else {
            card.setCardBackgroundColor(Color.parseColor("#E3DFD6"));
            card.setStrokeColor(Color.parseColor("#DDD9D0"));
        }
        card.setAlpha(free ? 1f : booked ? 0.75f : 0.45f); // anything that cannot be chosen is faded

        LinearLayout content = new LinearLayout(requireContext());
        content.setOrientation(LinearLayout.VERTICAL);
        content.setGravity(Gravity.CENTER);

        TextView name = new TextView(requireContext());
        name.setText("Bàn " + table.tableNumber);
        name.setTextSize(16);
        name.setTypeface(null, Typeface.BOLD);
        name.setTextColor(isSelected ? Color.WHITE : free ? getResources().getColor(R.color.text_primary) : Color.parseColor("#5A625D"));
        content.addView(name);

        TextView detail = new TextView(requireContext());
        String state = table.label == null || table.label.isEmpty() ? "" : table.label;
        detail.setText(free ? table.seats + " người" : table.seats + " người • " + state);
        detail.setGravity(Gravity.CENTER);
        detail.setTextSize(11);
        detail.setTextColor(isSelected ? Color.parseColor("#F5F2EC") : getResources().getColor(R.color.text_secondary));
        content.addView(detail);
        card.addView(content);

        card.setContentDescription("Bàn " + table.tableNumber + ", " + table.seats + " người, " + (isSelected ? "đang chọn" : state));
        // Unavailable tables cannot be chosen; a tap only says why.
        card.setOnClickListener(v -> {
            String reason = TableMapRules.reason(table, draft.getGuestCount());
            if (reason != null) { showToast(reason); return; }
            selectedTableId = table.tableId;
            updateBanner(null);
            render();
        });
        return card;
    }

    private int dp(int value) { return Math.round(value * getResources().getDisplayMetrics().density); }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
