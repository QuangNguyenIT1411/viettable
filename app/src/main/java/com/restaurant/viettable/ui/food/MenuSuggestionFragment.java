package com.restaurant.viettable.ui.food;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.material.button.MaterialButton;
import com.restaurant.viettable.R;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentMenuSuggestionBinding;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.model.MenuSuggestion;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.ArrayList;
import java.util.Locale;

/**
 * "Gợi ý món theo nhóm và ngân sách" (owner 06/11). The server picks dishes the admin tagged and prices them like the
 * bill; the guest may swap a dish for one the server offered, then adds everything to the cart in one tap.
 */
public class MenuSuggestionFragment extends BaseFragment {
    private FragmentMenuSuggestionBinding binding;
    private MenuSuggestion suggestion;
    private String branchId;
    private boolean busy;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentMenuSuggestionBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        binding.btnBack.setOnClickListener(v -> navigateBack());
        Branch branch = appStateManager.getSelectedBranch();
        branchId = branch == null ? null : branch.getId();
        if (branch != null) binding.tvSuggestionBranch.setText(("THỰC ĐƠN · " + branch.getName()).toUpperCase(Locale.forLanguageTag("vi")));
        binding.btnSuggest.setOnClickListener(v -> ask());
        binding.btnAddAll.setOnClickListener(v -> addAll());
        if (suggestion != null) render();
    }

    private void ask() {
        if (busy) return;
        // The keyboard must not stay over the results (found on the Samsung, 06/11).
        View focused = requireActivity().getCurrentFocus();
        if (focused != null) {
            focused.clearFocus();
            android.view.inputmethod.InputMethodManager ime = (android.view.inputmethod.InputMethodManager)
                    requireContext().getSystemService(android.content.Context.INPUT_METHOD_SERVICE);
            if (ime != null) ime.hideSoftInputFromWindow(focused.getWindowToken(), 0);
        }
        if (branchId == null) { status("Chưa chọn chi nhánh. Quay lại Thực đơn để chọn chi nhánh."); return; }
        int people = number(binding.etPeople.getText());
        int vegetarians = number(binding.etVegetarians.getText());
        long budget = MenuSuggestion.parseBudget(binding.etBudget.getText() == null ? "" : binding.etBudget.getText().toString());
        if (people < 1 || people > 30) { status("Nhập số người từ 1 đến 30."); return; }
        if (vegetarians > people) { status("Số người ăn chay không được lớn hơn số người."); return; }
        if (budget <= 0) { status("Nhập ngân sách, ví dụ 800k hoặc 1,5tr."); return; }
        setBusy(true, "Đang chọn món...");
        MobileRepository.getInstance().suggestMenu(branchId, people, budget, vegetarians, binding.switchNoSpicy.isChecked(),
                new MobileRepository.Callback<MenuSuggestion>() {
                    @Override public void onSuccess(MenuSuggestion value) {
                        if (binding == null) return;
                        setBusy(false, null);
                        suggestion = value;
                        render();
                    }
                    @Override public void onError(ApiException error) {
                        if (binding == null) return;
                        setBusy(false, apiError(error));
                    }
                });
    }

    private void swap(int index, MenuSuggestion.Alternative alt) {
        if (busy || suggestion == null) return;
        MenuSuggestion swapped = suggestion.swap(index, alt);
        setBusy(true, "Đang tính lại tổng tiền...");
        MobileRepository.getInstance().estimateMenu(branchId, swapped, new MobileRepository.Callback<MenuSuggestion.Totals>() {
            @Override public void onSuccess(MenuSuggestion.Totals value) {
                if (binding == null) return;
                setBusy(false, null);
                suggestion = swapped.withTotals(value);
                render();
            }
            @Override public void onError(ApiException error) {
                if (binding == null) return;
                setBusy(false, apiError(error)); // keep the old plan and its total
            }
        });
    }

    private void render() {
        binding.layoutNotes.removeAllViews();
        binding.layoutLines.removeAllViews();
        for (String note : suggestion.notes) binding.layoutNotes.addView(note(note));
        for (int i = 0; i < suggestion.lines.size(); i++) binding.layoutLines.addView(line(i, suggestion.lines.get(i)));
        boolean any = !suggestion.lines.isEmpty();
        binding.cardTotals.setVisibility(any ? View.VISIBLE : View.GONE);
        binding.btnAddAll.setVisibility(any && suggestion.feasible ? View.VISIBLE : View.GONE);
        if (!any) return;
        MenuSuggestion.Totals t = suggestion.totals;
        StringBuilder b = new StringBuilder("Tạm tính: ").append(MenuSuggestion.money(t.subtotal));
        if (t.fee > 0) b.append("\nPhí phục vụ ").append(percent(t.feePercent)).append(": ").append(MenuSuggestion.money(t.fee));
        if (t.vat > 0) b.append("\nVAT ").append(percent(t.vatPercent)).append(": ").append(MenuSuggestion.money(t.vat));
        binding.tvTotalsBreakdown.setText(b.toString());
        binding.tvTotal.setText((suggestion.feasible ? "Tổng dự kiến " : "Tối thiểu khoảng ") + MenuSuggestion.money(t.total));
        binding.tvTotalNote.setText(MenuSuggestion.TOTAL_NOTE);
        binding.tvBudgetLine.setText(suggestion.budgetLine() + " " + MenuSuggestion.money(suggestion.budget));
        binding.tvBudgetLine.setTextColor(getResources().getColor(suggestion.overBudget() ? R.color.pd_red_text : R.color.pd_mint_text, null));
        binding.btnAddAll.setText("Thêm tất cả vào giỏ (" + suggestion.dishCount() + " món)");
    }

    private View line(int index, MenuSuggestion.Line line) {
        LinearLayout card = new LinearLayout(requireContext());
        card.setOrientation(LinearLayout.VERTICAL);
        card.setBackgroundResource(R.drawable.pd_card_surface);
        card.setPadding(dp(16), dp(14), dp(16), dp(14));
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        params.topMargin = dp(10);
        card.setLayoutParams(params);

        TextView kind = text(MenuSuggestion.courseLabel(line.course).toUpperCase(Locale.forLanguageTag("vi"))
                + (line.forVegetarians ? " · CHAY" : ""), R.style.TextAppearance_VietTable_Label);
        kind.setTextColor(getResources().getColor(line.forVegetarians ? R.color.pd_mint_text : R.color.pd_yellow_text, null));
        card.addView(kind);
        TextView name = text(line.quantity + " × " + line.name, R.style.TextAppearance_VietTable_Body_Strong);
        card.addView(name);
        TextView detail = text(line.reason + " · " + MenuSuggestion.money(line.lineTotal()), R.style.TextAppearance_VietTable_BodySmall);
        detail.setTextColor(getResources().getColor(R.color.pd_muted, null));
        card.addView(detail);
        if (!line.alternatives.isEmpty()) {
            TextView label = text("Đổi sang:", R.style.TextAppearance_VietTable_BodySmall);
            label.setPadding(0, dp(8), 0, 0);
            card.addView(label);
            for (MenuSuggestion.Alternative alt : line.alternatives) {
                MaterialButton button = new MaterialButton(requireContext(), null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
                button.setText(alt.name + " · " + MenuSuggestion.money(alt.price));
                button.setAllCaps(false);
                button.setMinHeight(dp(48));
                button.setOnClickListener(v -> swap(index, alt));
                card.addView(button, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
            }
        }
        return card;
    }

    private void addAll() {
        if (suggestion == null || busy) return;
        int added = 0;
        ArrayList<String> missing = new ArrayList<>();
        for (MenuSuggestion.Line line : suggestion.lines) {
            FoodItem food = appStateManager.findFoodItem(line.dishId);
            if (food == null || !food.isAvailable()) { missing.add(line.name); continue; }
            appStateManager.addToCart(food, line.quantity, "Tiêu chuẩn", new ArrayList<>(), 0, "");
            added += line.quantity;
        }
        if (added == 0) { status("Chưa thêm được món nào: thực đơn trên máy chưa tải xong. Mở Thực đơn rồi thử lại."); return; }
        if (missing.isEmpty()) showCartAdded(added + " món");
        else showLongToast("Đã thêm " + added + " món. Chưa thêm được: " + String.join(", ", missing) + ".");
        navigateBack();
    }

    private void setBusy(boolean value, @Nullable String message) {
        busy = value;
        binding.btnSuggest.setEnabled(!value);
        binding.btnAddAll.setEnabled(!value);
        status(message);
    }

    private void status(@Nullable String message) {
        binding.tvSuggestionStatus.setVisibility(message == null ? View.GONE : View.VISIBLE);
        binding.tvSuggestionStatus.setText(message);
    }

    private View note(String value) {
        TextView view = text(value, R.style.TextAppearance_VietTable_BodySmall);
        view.setBackgroundResource(R.drawable.pd_card_surface);
        view.setPadding(dp(14), dp(10), dp(14), dp(10));
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        params.topMargin = dp(10);
        view.setLayoutParams(params);
        return view;
    }

    private TextView text(String value, int style) {
        TextView view = new TextView(requireContext());
        view.setTextAppearance(style);
        view.setText(value);
        return view;
    }

    private static int number(@Nullable CharSequence text) {
        if (text == null) return 0;
        try { return Integer.parseInt(text.toString().trim()); } catch (NumberFormatException e) { return 0; }
    }

    private static String percent(double value) {
        return (value == Math.rint(value) ? String.valueOf((long) value) : String.valueOf(value).replace('.', ',')) + "%";
    }

    private int dp(int value) { return (int) (value * getResources().getDisplayMetrics().density); }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
