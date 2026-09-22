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

        binding.btnSubmitRegister.setOnClickListener(v -> {
            String name = binding.etFullName.getText() != null ? binding.etFullName.getText().toString().trim() : "";
            String email = binding.etEmail.getText() != null ? binding.etEmail.getText().toString().trim() : "";
            String phone = binding.etPhone.getText() != null ? binding.etPhone.getText().toString().trim() : "";
            String pass = binding.etPassword.getText() != null ? binding.etPassword.getText().toString().trim() : "";
            String confirmPass = binding.etConfirmPassword.getText() != null ? binding.etConfirmPassword.getText().toString().trim() : "";

            if (TextUtils.isEmpty(name) || TextUtils.isEmpty(email) || TextUtils.isEmpty(phone) || TextUtils.isEmpty(pass)) {
                binding.tvError.setVisibility(View.VISIBLE);
                binding.tvError.setText("Vui lòng điền đầy đủ thông tin đăng ký.");
                return;
            }

            if (!pass.equals(confirmPass)) {
                binding.tvError.setVisibility(View.VISIBLE);
                binding.tvError.setText("Mật khẩu xác nhận không khớp.");
                return;
            }

            if (!binding.cbTerms.isChecked()) {
                binding.tvError.setVisibility(View.VISIBLE);
                binding.tvError.setText("Bạn cần đồng ý với Điều khoản sử dụng.");
                return;
            }

            binding.tvError.setVisibility(View.GONE);
            binding.btnSubmitRegister.setEnabled(false);
            MobileRepository.getInstance().register(name, email, phone, pass, new MobileRepository.Callback<User>() {
                @Override public void onSuccess(User value) {
                    if (binding == null) return;
                    binding.btnSubmitRegister.setEnabled(true);
                    showToast("Đăng ký tài khoản thành công!");
                    navigateTo(new BranchListFragment());
                }
                @Override public void onError(ApiException error) {
                    if (binding == null) return;
                    binding.btnSubmitRegister.setEnabled(true);
                    binding.tvError.setVisibility(View.VISIBLE);
                    binding.tvError.setText(apiError(error));
                }
            });
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
