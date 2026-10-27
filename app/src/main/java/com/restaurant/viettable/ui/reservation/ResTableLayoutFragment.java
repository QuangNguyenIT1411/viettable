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
        int width = getResources().getDisplayMetrics().widthPixels - dp(36);
        int cardSize = width / columns - 2 * margin;
        for (Map.Entry<String, List<TableMapItem>> group : TableMapRules.grouped(tables).entrySet()) {
            TextView header = new TextView(requireContext());
            header.setText(group.getKey());
            androidx.core.widget.TextViewCompat.setTextAppearance(header, R.style.TextAppearance_VietTable_Display_M);
            header.setPadding(dp(6), dp(18), dp(6), dp(4));
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

        // Cell per DESIGN_PHO_DEM.md section 6 "Chọn bàn": free = surface + mint dot, selected = yellow,
        // booked / in use = surface_2 + lock, not enough seats = dashed border, paused = 40%.
        boolean tooSmall = TableMapItem.TOO_SMALL.equals(table.status);
        boolean inUse = TableMapItem.IN_USE.equals(table.status);
        android.widget.FrameLayout card = new android.widget.FrameLayout(requireContext());
        GridLayout.LayoutParams lp = new GridLayout.LayoutParams();
        lp.width = size;
        lp.height = size;
        lp.setMargins(margin, margin, margin, margin);
        card.setLayoutParams(lp);
        card.setBackgroundResource(isSelected ? R.drawable.pd_table_selected : free ? R.drawable.pd_table_free
                : tooSmall ? R.drawable.pd_table_short : R.drawable.pd_table_booked);
        card.setAlpha(free || booked || inUse || tooSmall ? 1f : 0.4f);

        LinearLayout content = new LinearLayout(requireContext());
        content.setOrientation(LinearLayout.VERTICAL);
        content.setGravity(Gravity.CENTER);
        content.setLayoutParams(new android.widget.FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        TextView name = new TextView(requireContext());
        name.setText("Bàn " + table.tableNumber);
        name.setGravity(Gravity.CENTER);
        androidx.core.widget.TextViewCompat.setTextAppearance(name, R.style.TextAppearance_VietTable_Body_Strong);
        name.setTextColor(getResources().getColor(isSelected ? R.color.pd_on_color : free ? R.color.pd_cream : R.color.pd_dim));
        content.addView(name);

        TextView detail = new TextView(requireContext());
        String state = table.label == null || table.label.isEmpty() ? "" : table.label;
        detail.setText(free ? table.seats + " người" : table.seats + " người • " + state);
        detail.setGravity(Gravity.CENTER);
        androidx.core.widget.TextViewCompat.setTextAppearance(detail, R.style.TextAppearance_VietTable_Caption);
        detail.setTextColor(getResources().getColor(isSelected ? R.color.pd_on_color : free ? R.color.pd_muted : R.color.pd_dim));
        content.addView(detail);
        card.addView(content);

        if (free && !isSelected) card.addView(corner(R.drawable.pd_circle_mint, 8, 0));
        else if (booked || inUse) card.addView(corner(R.drawable.ic_lock, 14, R.color.pd_dim));

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

    /** Small mark in the top-right corner of a table cell (mint dot or lock). */
    private View corner(int drawable, int sizeDp, int tint) {
        android.widget.ImageView mark = new android.widget.ImageView(requireContext());
        android.widget.FrameLayout.LayoutParams lp = new android.widget.FrameLayout.LayoutParams(dp(sizeDp), dp(sizeDp),
                Gravity.TOP | Gravity.END);
        lp.setMargins(0, dp(10), dp(10), 0);
        mark.setLayoutParams(lp);
        mark.setImageResource(drawable);
        if (tint != 0) mark.setColorFilter(getResources().getColor(tint));
        mark.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        return mark;
    }

    private int dp(int value) { return Math.round(value * getResources().getDisplayMetrics().density); }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
