package com.restaurant.viettable.ui.auth;

import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentResetPasswordBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

public class ResetPasswordFragment extends BaseFragment {

    private FragmentResetPasswordBinding binding;
    private static final String ARG_TOKEN = "reset_token";
    private String resetToken;

    public static ResetPasswordFragment newInstance(String token) {
        ResetPasswordFragment fragment = new ResetPasswordFragment();
        Bundle args = new Bundle(); args.putString(ARG_TOKEN, token); fragment.setArguments(args); return fragment;
    }

    @Override public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState); if (getArguments() != null) resetToken = getArguments().getString(ARG_TOKEN);
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentResetPasswordBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.etConfirmNewPassword.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == android.view.inputmethod.EditorInfo.IME_ACTION_DONE) {
                binding.btnSubmitReset.performClick();
                return true;
            }
            return false;
        });
        binding.btnSubmitReset.setOnClickListener(v -> {
            String p1 = binding.etNewPassword.getText() != null ? binding.etNewPassword.getText().toString().trim() : "";
            String p2 = binding.etConfirmNewPassword.getText() != null ? binding.etConfirmNewPassword.getText().toString().trim() : "";

            binding.tilNewPassword.setError(null);
            binding.tilConfirmNewPassword.setError(null);
            if (TextUtils.isEmpty(p1) || p1.length() < 6) {
                binding.tilNewPassword.setError("Mật khẩu phải có ít nhất 6 ký tự.");
                binding.etNewPassword.requestFocus();
                return;
            }
            if (!p1.equals(p2)) {
                binding.tilConfirmNewPassword.setError("Mật khẩu xác nhận không khớp.");
                binding.etConfirmNewPassword.requestFocus();
                return;
            }
            if (TextUtils.isEmpty(resetToken)) { showToast("Mã đặt lại mật khẩu không hợp lệ."); return; }
            binding.btnSubmitReset.setEnabled(false);
            binding.btnSubmitReset.setText("Đang cập nhật…");
            MobileRepository.getInstance().resetPassword(resetToken, p1, new MobileRepository.Callback<Void>() {
                @Override public void onSuccess(Void value) { showToast("Đặt lại mật khẩu thành công! Vui lòng đăng nhập lại."); navigateTo(new LoginFragment()); }
                @Override public void onError(ApiException error) { if (binding != null) { binding.btnSubmitReset.setEnabled(true); binding.btnSubmitReset.setText("Xác nhận đổi mật khẩu"); binding.tilNewPassword.setError(apiError(error)); } }
            });
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
