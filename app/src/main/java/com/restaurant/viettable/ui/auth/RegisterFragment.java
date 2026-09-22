package com.restaurant.viettable.ui.auth;

import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentRegisterBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.branch.BranchListFragment;

public class RegisterFragment extends BaseFragment {

    private FragmentRegisterBinding binding;
    private boolean registering;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentRegisterBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnGoLogin.setOnClickListener(v -> navigateBack());
        binding.etConfirmPassword.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == android.view.inputmethod.EditorInfo.IME_ACTION_DONE) {
                binding.btnSubmitRegister.performClick();
                return true;
            }
            return false;
        });

        binding.btnSubmitRegister.setOnClickListener(v -> {
            if (registering) return;
            String name = binding.etFullName.getText() != null ? binding.etFullName.getText().toString().trim() : "";
            String email = binding.etEmail.getText() != null ? binding.etEmail.getText().toString().trim() : "";
            String phone = binding.etPhone.getText() != null ? binding.etPhone.getText().toString().trim() : "";
            String pass = binding.etPassword.getText() != null ? binding.etPassword.getText().toString().trim() : "";
            String confirmPass = binding.etConfirmPassword.getText() != null ? binding.etConfirmPassword.getText().toString().trim() : "";

            clearFieldErrors();
            if (TextUtils.isEmpty(name)) {
                binding.tilFullName.setError("Vui lòng nhập họ và tên.");
                binding.etFullName.requestFocus();
                return;
            }
            if (TextUtils.isEmpty(email)) {
                binding.tilEmail.setError("Vui lòng nhập email.");
                binding.etEmail.requestFocus();
                return;
            }
            if (!android.util.Patterns.EMAIL_ADDRESS.matcher(email).matches()) {
                binding.tilEmail.setError("Email không đúng định dạng.");
                binding.etEmail.requestFocus();
                return;
            }
            if (TextUtils.isEmpty(phone)) {
                binding.tilPhone.setError("Vui lòng nhập số điện thoại.");
                binding.etPhone.requestFocus();
                return;
            }
            if (TextUtils.isEmpty(pass)) {
                binding.tilPassword.setError("Vui lòng nhập mật khẩu.");
                binding.etPassword.requestFocus();
                return;
            }
            if (pass.length() < 6) {
                binding.tilPassword.setError("Mật khẩu phải có ít nhất 6 ký tự.");
                binding.etPassword.requestFocus();
                return;
            }

            if (TextUtils.isEmpty(confirmPass)) {
                binding.tilConfirmPassword.setError("Vui lòng xác nhận mật khẩu.");
                binding.etConfirmPassword.requestFocus();
                return;
            }
            if (!pass.equals(confirmPass)) {
                binding.tilConfirmPassword.setError("Mật khẩu xác nhận không khớp.");
                binding.etConfirmPassword.requestFocus();
                return;
            }

            if (!binding.cbTerms.isChecked()) {
                binding.tvError.setVisibility(View.VISIBLE);
                binding.tvError.setText("Bạn cần đồng ý với Điều khoản sử dụng.");
                return;
            }

            setRegistering(true);
            MobileRepository.getInstance().register(name, email, phone, pass, new MobileRepository.Callback<User>() {
                @Override public void onSuccess(User value) {
                    if (binding == null) return;
                    setRegistering(false);
                    showToast("Đăng ký tài khoản thành công!");
                    navigateTo(new BranchListFragment());
                }
                @Override public void onError(ApiException error) {
                    if (binding == null) return;
                    setRegistering(false);
                    binding.tvError.setVisibility(View.VISIBLE);
                    binding.tvError.setText(apiError(error));
                }
            });
        });
    }

    private void clearFieldErrors() {
        binding.tilFullName.setError(null);
        binding.tilEmail.setError(null);
        binding.tilPhone.setError(null);
        binding.tilPassword.setError(null);
        binding.tilConfirmPassword.setError(null);
        binding.tvError.setVisibility(View.GONE);
    }

    private void setRegistering(boolean value) {
        registering = value;
        if (binding == null) return;
        binding.btnSubmitRegister.setEnabled(!value);
        binding.btnGoLogin.setEnabled(!value);
        binding.btnSubmitRegister.setText(value
                ? com.restaurant.viettable.R.string.auth_registering
                : com.restaurant.viettable.R.string.auth_register);
    }

    @Override
    public void onDestroyView() {
        registering = false;
        super.onDestroyView();
        binding = null;
    }
}
