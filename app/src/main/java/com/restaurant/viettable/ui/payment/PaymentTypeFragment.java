package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentPaymentTypeBinding;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.SplitBill;
import com.restaurant.viettable.ui.base.BaseFragment;

public class PaymentTypeFragment extends BaseFragment {

    private static final String ARG_INVOICE = "arg_invoice";
    private static final String ARG_SPLIT = "arg_split";
    private static final String ARG_PART = "arg_part";
    private FragmentPaymentTypeBinding binding;
    private Invoice invoice;
    private SplitBill splitBill;
    private SplitBill.Part splitPart;

    public static PaymentTypeFragment newInstance(Invoice invoice) {
        PaymentTypeFragment f = new PaymentTypeFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_INVOICE, invoice);
        f.setArguments(b);
        return f;
    }

    public static PaymentTypeFragment newInstanceForSplit(Invoice invoice, SplitBill splitBill, SplitBill.Part part) {
        PaymentTypeFragment fragment = newInstance(invoice);
        fragment.requireArguments().putSerializable(ARG_SPLIT, splitBill);
        fragment.requireArguments().putSerializable(ARG_PART, part);
        return fragment;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            invoice = (Invoice) getArguments().getSerializable(ARG_INVOICE);
            splitBill = (SplitBill) getArguments().getSerializable(ARG_SPLIT);
            splitPart = (SplitBill.Part) getArguments().getSerializable(ARG_PART);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentPaymentTypeBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        if (invoice != null) {
            binding.tvPaymentTypeGrandTotal.setText(splitPart == null ? invoice.getFormattedFinalTotal() : splitPart.getFormattedAmount());
            binding.tvPaymentTypeTable.setText(invoice.getTableLabel() + " • " + invoice.getBranchName());
        }

        binding.cardPayOnline.setOnClickListener(v -> {
            navigateTo(splitPart == null ? OnlinePaymentMethodFragment.newInstance(invoice)
                    : OnlinePaymentMethodFragment.newInstanceForSplit(invoice, splitBill, splitPart));
        });

        binding.cardPayOnsite.setOnClickListener(v -> {
            navigateTo(splitPart == null ? OnsitePaymentMethodFragment.newInstance(invoice)
                    : OnsitePaymentMethodFragment.newInstanceForSplit(invoice, splitBill, splitPart));
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
