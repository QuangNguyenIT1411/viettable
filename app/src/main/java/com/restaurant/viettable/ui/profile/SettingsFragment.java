package com.restaurant.viettable.ui.profile;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;

import com.restaurant.viettable.databinding.FragmentSettingsBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.network.ApiException;
import org.json.JSONObject;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.SafeText;

public class SettingsFragment extends BaseFragment {

    private FragmentSettingsBinding binding;
    private boolean loading = true;
    private String languageCode = "vi";
    // Fingerprint login is per device (Hồ sơ); the account flag is only echoed back unchanged.
    private boolean serverBiometricFlag;

    public static SettingsFragment newInstance() {
        return new SettingsFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentSettingsBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        binding.switchPushNotification.setOnCheckedChangeListener((btn, isChecked) -> {
            if (loading) return;
            showToast(isChecked ? "Đã bật thông báo đẩy" : "Đã tắt thông báo đẩy");
            saveSettings();
        });

        binding.switchSms.setOnCheckedChangeListener((btn, isChecked) -> {
            if (loading) return;
            showToast(isChecked ? "Đã bật thông báo SMS" : "Đã tắt thông báo SMS");
            saveSettings();
        });

        binding.rowLanguage.setOnClickListener(v -> {
            new com.google.android.material.dialog.MaterialAlertDialogBuilder(requireContext())
                    .setTitle("Chọn ngôn ngữ")
                    .setSingleChoiceItems(new String[]{"Tiếng Việt (Mặc định)", "English"}, 0, (dialog, which) -> {
                        languageCode = which == 0 ? "vi" : "en";
                        saveSettings();
                        showToast("Đã chọn ngôn ngữ");
                        dialog.dismiss();
                    })
                    .setNegativeButton("Đóng", null)
                    .show();
        });

        // The deposit sentence follows the server's flag (owner 27/10): never "no deposit" while one is asked.
        binding.rowTerms.setOnClickListener(v -> MobileRepository.getInstance().loadDepositPolicy(
                new MobileRepository.Callback<com.restaurant.viettable.model.DepositPolicy>() {
                    @Override public void onSuccess(com.restaurant.viettable.model.DepositPolicy policy) { showTerms(policy); }
                    @Override public void onError(ApiException error) { showTerms(MobileRepository.getInstance().cachedDepositPolicy()); }
                }));

        binding.rowPrivacy.setOnClickListener(v -> {
            com.restaurant.viettable.ui.common.DialogHelper.showInfoDialog(
                    requireContext(),
                    "Chính sách bảo mật",
                    "Dữ liệu khách hàng, số điện thoại và lịch sử giao dịch được mã hóa an toàn theo tiêu chuẩn bảo mật hệ thống nhà hàng.",
                    "Đóng",
                    null
            );
        });

        MobileRepository.getInstance().loadDepositPolicy(new MobileRepository.Callback<com.restaurant.viettable.model.DepositPolicy>() {
            @Override public void onSuccess(com.restaurant.viettable.model.DepositPolicy policy) { }
            @Override public void onError(ApiException error) { }
        });
        MobileRepository.getInstance().loadSettings(new MobileRepository.Callback<JSONObject>() {
            @Override public void onSuccess(JSONObject value) {
                if (binding == null) return;
                binding.switchPushNotification.setChecked(value.optBoolean("pushNotificationsEnabled"));
                binding.switchSms.setChecked(value.optBoolean("smsReservationRemindersEnabled"));
                serverBiometricFlag = value.optBoolean("biometricLoginEnabled");
                languageCode = SafeText.json(value, "languageCode", "vi"); loading = false;
            }
            @Override public void onError(ApiException error) { loading = false; if (binding != null) showToast(apiError(error)); }
        });
    }

    private void saveSettings() {
        if (binding == null || loading) return;
        MobileRepository.getInstance().updateSettings(binding.switchPushNotification.isChecked(), binding.switchSms.isChecked(),
                serverBiometricFlag, languageCode, new MobileRepository.Callback<JSONObject>() {
                    @Override public void onSuccess(JSONObject value) {}
                    @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
                });
    }

    private void showTerms(com.restaurant.viettable.model.DepositPolicy policy) {
        if (binding == null) return;
        com.restaurant.viettable.ui.common.DialogHelper.showInfoDialog(requireContext(), "Điều khoản dịch vụ",
                com.restaurant.viettable.util.DepositRules.termsText(policy), "Đóng", null);
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
