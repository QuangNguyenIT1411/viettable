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
import com.restaurant.viettable.util.PasswordResetRules;

/**
 * "Tạo mật khẩu mới" (owner 08/11): the 6-digit code e-mailed for the account typed on "Quên mật khẩu", the new
 * password twice, then back to "Đăng nhập". "Gửi lại" asks for a new code.
 */
public class ResetPasswordFragment extends BaseFragment {

    private FragmentResetPasswordBinding binding;
    private static final String ARG_IDENTITY = "reset_identity";
    private static final String ARG_MESSAGE = "reset_message";
    private String identity;
    private String sentMessage;

    public static ResetPasswordFragment newInstance(String identity, String sentMessage) {
        ResetPasswordFragment fragment = new ResetPasswordFragment();
        Bundle args = new Bundle();
        args.putString(ARG_IDENTITY, identity);
        args.putString(ARG_MESSAGE, sentMessage);
        fragment.setArguments(args);
        return fragment;
    }

    @Override public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            identity = getArguments().getString(ARG_IDENTITY);
            sentMessage = getArguments().getString(ARG_MESSAGE);
        }
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
        binding.tvResetSent.setText(TextUtils.isEmpty(sentMessage) ? PasswordResetRules.DEFAULT_SENT : sentMessage);
        binding.etConfirmNewPassword.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == android.view.inputmethod.EditorInfo.IME_ACTION_DONE) {
                binding.btnSubmitReset.performClick();
                return true;
            }
            return false;
        });
        binding.btnResendCode.setOnClickListener(v -> resend());
        binding.btnSubmitReset.setOnClickListener(v -> submit());
    }

    private void submit() {
        String code = binding.etResetCode.getText() != null ? binding.etResetCode.getText().toString() : "";
        String p1 = binding.etNewPassword.getText() != null ? binding.etNewPassword.getText().toString().trim() : "";
        String p2 = binding.etConfirmNewPassword.getText() != null ? binding.etConfirmNewPassword.getText().toString().trim() : "";

        binding.tilResetCode.setError(null);
        binding.tilNewPassword.setError(null);
        binding.tilConfirmNewPassword.setError(null);
        String badCode = PasswordResetRules.whyBadCode(code);
        if (badCode != null) {
            binding.tilResetCode.setError(badCode);
            binding.etResetCode.requestFocus();
            return;
        }
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
        if (TextUtils.isEmpty(identity)) { showToast("Vui lòng nhập lại email hoặc số điện thoại."); navigateBack(); return; }
        binding.btnSubmitReset.setEnabled(false);
        binding.btnSubmitReset.setText("Đang cập nhật…");
        MobileRepository.getInstance().resetPasswordWithCode(identity, code, p1, new MobileRepository.Callback<Void>() {
            @Override public void onSuccess(Void value) {
                if (binding == null) return;
                showLongToast("Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.");
                resetTo(new LoginFragment());
            }
            @Override public void onError(ApiException error) {
                if (binding == null) return;
                binding.btnSubmitReset.setEnabled(true);
                binding.btnSubmitReset.setText("Xác nhận đổi mật khẩu");
                // Code problems belong under the code box; anything else under the password.
                String code = error.getCode();
                if (code != null && (code.contains("code") || code.contains("reset_token"))) binding.tilResetCode.setError(apiError(error));
                else binding.tilNewPassword.setError(apiError(error));
            }
        });
    }

    private void resend() {
        if (TextUtils.isEmpty(identity)) { navigateBack(); return; }
        binding.btnResendCode.setEnabled(false);
        MobileRepository.getInstance().forgotPassword(identity, new MobileRepository.Callback<String>() {
            @Override public void onSuccess(String message) {
                if (binding == null) return;
                binding.btnResendCode.setEnabled(true);
                binding.tvResetSent.setText(message);
                showToast("Đã gửi lại mã xác nhận.");
            }
            @Override public void onError(ApiException error) {
                if (binding == null) return;
                binding.btnResendCode.setEnabled(true);
                showToast(apiError(error));
            }
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
