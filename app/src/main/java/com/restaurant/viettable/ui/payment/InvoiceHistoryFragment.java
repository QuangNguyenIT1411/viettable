package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.view.KeyEvent;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.view.inputmethod.EditorInfo;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;

import com.google.android.material.chip.Chip;
import com.restaurant.viettable.R;
import com.restaurant.viettable.adapter.ReceiptHistoryAdapter;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentInvoiceHistoryBinding;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.Receipt;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.time.LocalDate;
import java.time.ZoneId;
import java.util.ArrayList;
import java.util.List;

/**
 * "Hoá đơn của tôi" (owner 05/11): the guest's invoices from the server, a page at a time, grouped by month; search by
 * invoice number and filters for month, branch and payment method are applied by the server, not on the phone.
 */
public class InvoiceHistoryFragment extends BaseFragment implements com.restaurant.viettable.ui.base.RequiresLogin {
    @Override public String loginPurpose() { return "hoá đơn của bạn"; }

    private static final int PAGE = 20;

    private FragmentInvoiceHistoryBinding binding;
    private ReceiptHistoryAdapter adapter;
    private final List<Receipt> receipts = new ArrayList<>();
    private String nextCursor;
    private boolean loading;
    private int loadToken;
    private String month = "";
    private int branchId;
    private String method = "";
    private String query = "";

    public static InvoiceHistoryFragment newInstance() { return new InvoiceHistoryFragment(); }

    @Nullable @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentInvoiceHistoryBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        binding.btnBack.setOnClickListener(v -> navigateBack());
        adapter = new ReceiptHistoryAdapter(receipt -> navigateTo(InvoiceHistoryDetailFragment.newInstance(receipt.invoiceId)));
        LinearLayoutManager layout = new LinearLayoutManager(requireContext());
        binding.rvInvoices.setLayoutManager(layout);
        binding.rvInvoices.setAdapter(adapter);
        binding.rvInvoices.addOnScrollListener(new RecyclerView.OnScrollListener() {
            @Override public void onScrolled(@NonNull RecyclerView rv, int dx, int dy) {
                if (dy > 0 && !loading && nextCursor != null && layout.findLastVisibleItemPosition() >= adapter.getItemCount() - 4) loadMore();
            }
        });
        binding.btnRetryInvoices.setOnClickListener(v -> reload());
        binding.etInvoiceSearch.setOnEditorActionListener((v, actionId, event) -> {
            boolean search = actionId == EditorInfo.IME_ACTION_SEARCH
                    || (event != null && event.getKeyCode() == KeyEvent.KEYCODE_ENTER && event.getAction() == KeyEvent.ACTION_UP);
            if (search) { query = v.getText() == null ? "" : v.getText().toString().trim(); hideKeyboard(); reload(); }
            return search;
        });
        binding.tilInvoiceSearch.setEndIconOnClickListener(v -> {
            binding.etInvoiceSearch.setText("");
            if (!query.isEmpty()) { query = ""; reload(); }
        });
        buildMonthChips();
        buildFilterChips();
        reload();
    }

    /** "Tất cả" and the last 12 months (Vietnam time); the server filters. */
    private void buildMonthChips() {
        binding.chipGroupInvoiceMonth.removeAllViews();
        addChoice(binding.chipGroupInvoiceMonth, "Mọi tháng", true, v -> { month = ""; reload(); });
        LocalDate now = LocalDate.now(ZoneId.of("Asia/Ho_Chi_Minh")).withDayOfMonth(1);
        for (int i = 0; i < 12; i++) {
            LocalDate m = now.minusMonths(i);
            String key = m.getYear() + "-" + (m.getMonthValue() < 10 ? "0" : "") + m.getMonthValue();
            addChoice(binding.chipGroupInvoiceMonth, "Tháng " + m.getMonthValue() + "/" + m.getYear(), false, v -> { month = key; reload(); });
        }
    }

    /** Branch (from the server's branch list) and method chips; tapping a selected one clears it. */
    private void buildFilterChips() {
        binding.chipGroupInvoiceFilter.removeAllViews();
        addToggle("Tiền mặt", () -> method.equals("CASH"), () -> method = method.equals("CASH") ? "" : "CASH");
        addToggle("Chuyển khoản", () -> method.equals("TRANSFER"), () -> method = method.equals("TRANSFER") ? "" : "TRANSFER");
        for (Branch branch : appStateManager.getBranches()) {
            int id;
            try { id = Integer.parseInt(branch.getId()); } catch (NumberFormatException e) { continue; }
            addToggle(branch.getName(), () -> branchId == id, () -> branchId = branchId == id ? 0 : id);
        }
    }

    private interface Check { boolean on(); }

    private void addToggle(String label, Check isOn, Runnable flip) {
        Chip chip = new Chip(requireContext(), null, com.google.android.material.R.attr.chipStyle);
        chip.setText(label);
        chip.setCheckable(true);
        chip.setChecked(isOn.on());
        chip.setOnClickListener(v -> {
            flip.run();
            refreshFilterChecks();
            reload();
        });
        binding.chipGroupInvoiceFilter.addView(chip);
    }

    private void refreshFilterChecks() {
        if (binding == null) return;
        int i = 0;
        String[] methods = {"CASH", "TRANSFER"};
        for (; i < methods.length && i < binding.chipGroupInvoiceFilter.getChildCount(); i++)
            ((Chip) binding.chipGroupInvoiceFilter.getChildAt(i)).setChecked(method.equals(methods[i]));
        for (Branch branch : appStateManager.getBranches()) {
            if (i >= binding.chipGroupInvoiceFilter.getChildCount()) break;
            int id;
            try { id = Integer.parseInt(branch.getId()); } catch (NumberFormatException e) { continue; }
            ((Chip) binding.chipGroupInvoiceFilter.getChildAt(i++)).setChecked(branchId == id);
        }
    }

    private void addChoice(com.google.android.material.chip.ChipGroup group, String label, boolean checked, View.OnClickListener click) {
        Chip chip = new Chip(requireContext(), null, com.google.android.material.R.attr.chipStyle);
        chip.setText(label);
        chip.setCheckable(true);
        chip.setChecked(checked);
        chip.setOnClickListener(click);
        group.addView(chip);
    }

    private void reload() {
        receipts.clear();
        nextCursor = null;
        adapter.submit(receipts, false);
        showState(true, "Đang tải hoá đơn…", false);
        fetch(null);
    }

    private void loadMore() {
        if (nextCursor == null) return;
        adapter.submit(receipts, true);
        fetch(nextCursor);
    }

    private void fetch(@Nullable String cursor) {
        loading = true;
        int token = ++loadToken;
        MobileRepository.getInstance().loadInvoicePage(month, branchId, method, query, cursor, PAGE, new MobileRepository.Callback<MobileRepository.InvoicePage>() {
            @Override public void onSuccess(MobileRepository.InvoicePage page) {
                if (binding == null || token != loadToken) return;
                loading = false;
                receipts.addAll(page.items);
                nextCursor = page.nextCursor;
                adapter.submit(receipts, false);
                if (receipts.isEmpty()) showState(false, emptyText(), false);
                else binding.layoutInvoiceState.setVisibility(View.GONE);
            }
            @Override public void onError(ApiException error) {
                if (binding == null || token != loadToken) return;
                loading = false;
                adapter.submit(receipts, false);
                if (receipts.isEmpty()) showState(false, apiError(error), true);
                else showToast(apiError(error));
            }
        });
    }

    private String emptyText() {
        boolean filtered = !month.isEmpty() || branchId > 0 || !method.isEmpty() || !query.isEmpty();
        return filtered ? "Không có hoá đơn khớp bộ lọc." : "Bạn chưa có hoá đơn nào. Hoá đơn sẽ có ở đây sau khi bạn thanh toán bữa ăn.";
    }

    private void showState(boolean progress, String text, boolean retry) {
        binding.layoutInvoiceState.setVisibility(View.VISIBLE);
        binding.progressInvoices.setVisibility(progress ? View.VISIBLE : View.GONE);
        binding.tvInvoiceState.setText(text);
        binding.btnRetryInvoices.setVisibility(retry ? View.VISIBLE : View.GONE);
    }

    private void hideKeyboard() {
        android.view.inputmethod.InputMethodManager imm = (android.view.inputmethod.InputMethodManager)
                requireContext().getSystemService(android.content.Context.INPUT_METHOD_SERVICE);
        if (imm != null) imm.hideSoftInputFromWindow(binding.etInvoiceSearch.getWindowToken(), 0);
    }

    @Override public void onDestroyView() {
        super.onDestroyView();
        loadToken++;
        binding = null;
    }
}
