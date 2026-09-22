package com.restaurant.viettable.ui.auth;

import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentForgotPasswordBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

public class ForgotPasswordFragment extends BaseFragment {

    private FragmentForgotPasswordBinding binding;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentForgotPasswordBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.etEmailPhone.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == android.view.inputmethod.EditorInfo.IME_ACTION_DONE) {
                binding.btnSubmitForgot.performClick();
                return true;
            }
            return false;
        });
        binding.btnSubmitForgot.setOnClickListener(v -> {
            String input = binding.etEmailPhone.getText() != null ? binding.etEmailPhone.getText().toString().trim() : "";
            if (TextUtils.isEmpty(input)) {
                binding.tilEmailPhone.setError("Vui lòng nhập email hoặc số điện thoại.");
                binding.etEmailPhone.requestFocus();
                return;
            }
            binding.tilEmailPhone.setError(null);
            binding.btnSubmitForgot.setEnabled(false);
            binding.btnSubmitForgot.setText("Đang gửi…");
            MobileRepository.getInstance().forgotPassword(input, new MobileRepository.Callback<String>() {
                @Override public void onSuccess(String token) { if (binding != null) { showToast("Đã xác minh yêu cầu đặt lại mật khẩu."); navigateTo(ResetPasswordFragment.newInstance(token)); } }
                @Override public void onError(ApiException error) { if (binding != null) { binding.btnSubmitForgot.setEnabled(true); binding.btnSubmitForgot.setText("Gửi mã xác nhận"); binding.tilEmailPhone.setError(apiError(error)); } }
            });
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
