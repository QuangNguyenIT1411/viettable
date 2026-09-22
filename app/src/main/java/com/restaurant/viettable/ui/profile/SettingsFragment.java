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

public class SettingsFragment extends BaseFragment {

    private FragmentSettingsBinding binding;
    private boolean loading = true;
    private String languageCode = "vi";

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

        binding.switchBiometric.setOnCheckedChangeListener((btn, isChecked) -> {
            if (loading) return;
            showToast(isChecked ? "Đã kích hoạt xác thực sinh trắc học" : "Đã tắt xác thực sinh trắc học");
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

        binding.rowTerms.setOnClickListener(v -> {
            com.restaurant.viettable.ui.common.DialogHelper.showInfoDialog(
                    requireContext(),
                    "Điều khoản dịch vụ",
                    "VietTable cam kết giữ bàn trong vòng 15 phút sau giờ đặt trước. Tiền cọc sẽ được hoàn lại nếu khách hủy bàn trước 2 tiếng theo quy định của chuỗi nhà hàng.",
                    "Đóng",
                    null
            );
        });

        binding.rowPrivacy.setOnClickListener(v -> {
            com.restaurant.viettable.ui.common.DialogHelper.showInfoDialog(
                    requireContext(),
                    "Chính sách bảo mật",
                    "Dữ liệu khách hàng, số điện thoại và lịch sử giao dịch được mã hóa an toàn theo tiêu chuẩn bảo mật hệ thống nhà hàng.",
                    "Đóng",
                    null
            );
        });

        MobileRepository.getInstance().loadSettings(new MobileRepository.Callback<JSONObject>() {
            @Override public void onSuccess(JSONObject value) {
                if (binding == null) return;
                binding.switchPushNotification.setChecked(value.optBoolean("pushNotificationsEnabled"));
                binding.switchSms.setChecked(value.optBoolean("smsReservationRemindersEnabled"));
                binding.switchBiometric.setChecked(value.optBoolean("biometricLoginEnabled"));
                languageCode = value.optString("languageCode", "vi"); loading = false;
            }
            @Override public void onError(ApiException error) { loading = false; if (binding != null) showToast(apiError(error)); }
        });
    }

    private void saveSettings() {
        if (binding == null || loading) return;
        MobileRepository.getInstance().updateSettings(binding.switchPushNotification.isChecked(), binding.switchSms.isChecked(),
                binding.switchBiometric.isChecked(), languageCode, new MobileRepository.Callback<JSONObject>() {
                    @Override public void onSuccess(JSONObject value) {}
                    @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
                });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
