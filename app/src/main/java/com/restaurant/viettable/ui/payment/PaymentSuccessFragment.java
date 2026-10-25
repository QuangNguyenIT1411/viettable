package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentPaymentSuccessBinding;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.profile.ReviewFragment;
import com.restaurant.viettable.util.SafeText;

public class PaymentSuccessFragment extends BaseFragment {

    private static final String ARG_INVOICE = "arg_invoice";
    private FragmentPaymentSuccessBinding binding;
    private Invoice invoice;

    public static PaymentSuccessFragment newInstance(Invoice invoice) {
        PaymentSuccessFragment f = new PaymentSuccessFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_INVOICE, invoice);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            invoice = (Invoice) getArguments().getSerializable(ARG_INVOICE);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentPaymentSuccessBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        // Project the server-owned closed state instead of closing the session locally.
        MobileRepository.getInstance().loadActiveSession(new MobileRepository.Callback<DiningSession>() {
            @Override public void onSuccess(DiningSession value) { }
            @Override public void onError(ApiException error) { }
        });

        if (invoice != null) {
            binding.tvSuccessAmount.setText(invoice.getFormattedFinalTotal());
            binding.tvSuccessInvoiceId.setText(com.restaurant.viettable.util.StatusLabels.invoiceTitle(invoice.getBackendInvoiceId(), invoice.getId()));
            binding.tvSuccessMethod.setText(invoice.getPaymentMethod());
            binding.tvSuccessBranch.setText(SafeText.join(" • ", invoice.getTableLabel(), invoice.getBranchName()));
        }

        binding.btnReviewMeal.setOnClickListener(v -> {
            if (invoice != null && invoice.getBackendInvoiceId() > 0) navigateTo(ReviewFragment.newInstanceForInvoice(invoice.getBackendInvoiceId()));
            else showToast("Không tìm thấy hóa đơn hợp lệ để đánh giá.");
        });

        // switchTab clears the back stack. Back must do the same: the screens underneath are the paid payment's
        // waiting/QR screens and must never open again (25/10: Back reopened TT33's QR after it was paid).
        binding.btnBackToHomeFromPayment.setOnClickListener(v -> switchTab(R.id.nav_home));
        requireActivity().getOnBackPressedDispatcher().addCallback(getViewLifecycleOwner(),
                new androidx.activity.OnBackPressedCallback(true) {
                    @Override public void handleOnBackPressed() { switchTab(R.id.nav_home); }
                });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
