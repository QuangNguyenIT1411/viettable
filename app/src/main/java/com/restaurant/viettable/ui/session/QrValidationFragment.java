package com.restaurant.viettable.ui.session;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentQrValidationBinding;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import org.json.JSONObject;
import com.restaurant.viettable.util.SafeText;

public class QrValidationFragment extends BaseFragment {

    private static final String ARG_TABLE_CODE = "arg_table_code";
    private FragmentQrValidationBinding binding;
    private String tableCode;

    public static QrValidationFragment newInstance(String tableCode) {
        QrValidationFragment fragment = new QrValidationFragment();
        Bundle args = new Bundle();
        args.putString(ARG_TABLE_CODE, tableCode);
        fragment.setArguments(args);
        return fragment;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            tableCode = getArguments().getString(ARG_TABLE_CODE);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentQrValidationBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        if (tableCode == null || tableCode.trim().isEmpty()) {
            showToast("Không nhận được mã bàn hợp lệ.");
            navigateBack();
            return;
        }
        binding.tvScannedCodeDisplay.setText("Mã bàn đã quét: " + tableCode);

        MobileRepository.getInstance().validateQr(tableCode, new MobileRepository.Callback<JSONObject>() {
            @Override public void onSuccess(JSONObject value) {
                if (!isAdded()) return;
                String outcome = SafeText.json(value, "outcome");
                if ("READY".equals(outcome)) navigateTo(CheckinConfirmFragment.newInstance(tableCode));
                else if ("MISMATCH_CONFIRMATION_REQUIRED".equals(outcome))
                    navigateTo(ReservationMismatchFragment.newInstance(tableCode, String.valueOf(value.optInt("reservedTableId"))));
                else if ("WALK_IN_AVAILABLE".equals(outcome)) navigateTo(WalkinConfirmFragment.newInstance(tableCode));
                else { showToast(SafeText.json(value, "message", "Chưa thể check-in lúc này.")); navigateBack(); }
            }
            @Override public void onError(ApiException error) { if (binding != null) { showToast(apiError(error)); navigateBack(); } }
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
