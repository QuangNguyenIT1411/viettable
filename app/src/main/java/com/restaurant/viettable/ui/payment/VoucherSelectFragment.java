package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.restaurant.viettable.adapter.VoucherSelectAdapter;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentVoucherSelectBinding;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.Voucher;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.common.ListState;

import java.util.ArrayList;
import java.util.List;

/**
 * "Chọn Voucher" on the bill (owner 28/10/2026): the server's list for THIS bill — vouchers the guest may use first,
 * the others greyed with the reason (đã dùng, hết hạn, chưa đủ đơn tối thiểu…). The chosen one is applied on the
 * server, then the bill shows it; it is never applied by itself.
 */
public class VoucherSelectFragment extends BaseFragment {

    private static final String ARG_POINTS = "arg_points";
    private FragmentVoucherSelectBinding binding;
    private VoucherSelectAdapter adapter;
    private final List<Voucher> vouchers = new ArrayList<>();
    private Voucher chosenVoucher;
    private boolean loading;
    private boolean applying;
    private String loadError;

    public static VoucherSelectFragment newInstance(long points) {
        VoucherSelectFragment f = new VoucherSelectFragment();
        Bundle b = new Bundle();
        b.putLong(ARG_POINTS, points);
        f.setArguments(b);
        return f;
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentVoucherSelectBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        adapter = new VoucherSelectAdapter(vouchers, voucher -> chosenVoucher = voucher);
        binding.rvVouchers.setLayoutManager(new LinearLayoutManager(requireContext()));
        binding.rvVouchers.setAdapter(adapter);
        binding.btnRetryVouchers.setOnClickListener(v -> load());

        binding.btnApplyCode.setOnClickListener(v -> {
            String code = binding.etVoucherCode.getText() != null ? binding.etVoucherCode.getText().toString().trim() : "";
            if (TextUtils.isEmpty(code)) {
                binding.tilVoucherCode.setError("Vui lòng nhập mã");
                return;
            }
            binding.tilVoucherCode.setError(null);
            Voucher found = null;
            for (Voucher voucher : vouchers) {
                if (voucher.getCode() != null && voucher.getCode().equalsIgnoreCase(code)) { found = voucher; break; }
            }
            if (found == null) { binding.tilVoucherCode.setError("Mã không tồn tại hoặc chưa áp dụng"); return; }
            if (!found.isUsable()) { binding.tilVoucherCode.setError(found.getReason()); return; }
            chosenVoucher = found;
            adapter.select(found);
            showToast("Đã chọn mã " + found.getCode());
        });

        binding.btnConfirmVoucher.setOnClickListener(v -> confirm());
        load();
    }

    private void load() {
        loading = true; loadError = null; render();
        MobileRepository.getInstance().loadVouchersForBill(new MobileRepository.Callback<List<Voucher>>() {
            @Override public void onSuccess(List<Voucher> value) {
                if (binding == null) return;
                loading = false;
                vouchers.clear(); vouchers.addAll(value);
                adapter.notifyDataSetChanged();
                for (Voucher voucher : vouchers) if (voucher.isApplied() && voucher.isUsable()) { chosenVoucher = voucher; adapter.select(voucher); }
                render();
            }
            @Override public void onError(ApiException error) {
                if (binding == null) return;
                loading = false; loadError = apiError(error); render();
            }
        });
    }

    private void render() {
        ListState.render(binding.rvVouchers, binding.progressVouchers, binding.tvVoucherState, binding.btnRetryVouchers,
                !vouchers.isEmpty(), loading, loadError, "Hiện chưa có voucher nào. Khuyến mãi tự động (nếu có) vẫn được áp vào hoá đơn.");
        boolean anyUsable = false;
        for (Voucher voucher : vouchers) if (voucher.isUsable()) { anyUsable = true; break; }
        binding.btnConfirmVoucher.setEnabled(!applying && anyUsable);
    }

    private void confirm() {
        if (applying) return;
        Voucher voucher = chosenVoucher != null ? chosenVoucher : adapter.getSelectedVoucher();
        if (voucher == null) { showToast("Vui lòng chọn một voucher."); return; }
        applying = true; render();
        MobileRepository.getInstance().applyVoucher(voucher, new MobileRepository.Callback<Invoice>() {
            @Override public void onSuccess(Invoice value) {
                applying = false;
                if (binding == null) return;
                showToast(value.getVoucherDiscount() > 0 ? "Đã áp voucher " + voucher.getCode()
                        : "Đã chọn voucher " + voucher.getCode() + " (khuyến mãi đang lợi hơn nên chưa áp)");
                navigateBack();
            }
            @Override public void onError(ApiException error) {
                applying = false;
                if (binding == null) return;
                render();
                showToast(apiError(error));
                load(); // the reason (e.g. the last use was just taken) shows on the card
            }
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
