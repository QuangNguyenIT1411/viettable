package com.restaurant.viettable.ui.auth;

import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.BuildConfig;
import com.restaurant.viettable.databinding.FragmentLoginBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.branch.BranchListFragment;

public class LoginFragment extends BaseFragment {

    private FragmentLoginBinding binding;
    private final GoogleSignInCoordinator googleSignIn = new GoogleSignInCoordinator();
    private boolean authenticating;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentLoginBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnGoRegister.setOnClickListener(v -> navigateTo(new RegisterFragment()));
        binding.btnForgotPassword.setOnClickListener(v -> navigateTo(new ForgotPasswordFragment()));
        binding.btnGoogleSignIn.setOnClickListener(v -> startGoogleSignIn());

        binding.btnSubmitLogin.setOnClickListener(v -> {
            String email = binding.etEmail.getText() != null ? binding.etEmail.getText().toString().trim() : "";
            String pass = binding.etPassword.getText() != null ? binding.etPassword.getText().toString().trim() : "";

            if (TextUtils.isEmpty(email) || TextUtils.isEmpty(pass)) {
                binding.tvError.setVisibility(View.VISIBLE);
                binding.tvError.setText("Vui lòng nhập đầy đủ email/số điện thoại và mật khẩu.");
                return;
            }

            setAuthenticating(true, com.restaurant.viettable.R.string.auth_continue_with_google);
            MobileRepository.getInstance().login(email, pass, new MobileRepository.Callback<User>() {
                @Override public void onSuccess(User value) {
                    if (binding == null) return;
                    setAuthenticating(false, com.restaurant.viettable.R.string.auth_continue_with_google);
                    showToast("Đăng nhập thành công!");
                    navigateTo(new BranchListFragment());
                }
                @Override public void onError(ApiException error) {
                    if (binding == null) return;
                    setAuthenticating(false, com.restaurant.viettable.R.string.auth_continue_with_google);
                    binding.tvError.setVisibility(View.VISIBLE);
                    binding.tvError.setText(apiError(error));
                }
            });
        });
    }

    private void startGoogleSignIn() {
        if (authenticating || googleSignIn.isRequestInFlight()) return;
        if (!GoogleSignInConfig.isConfigured(BuildConfig.GOOGLE_WEB_CLIENT_ID)) {
            showError(getString(com.restaurant.viettable.R.string.auth_google_not_configured));
            return;
        }

        setAuthenticating(true, com.restaurant.viettable.R.string.auth_google_launching);
        googleSignIn.start(requireActivity(), BuildConfig.GOOGLE_WEB_CLIENT_ID, new GoogleSignInCoordinator.Callback() {
            @Override public void onIdToken(@NonNull String idToken) {
                if (binding == null) return;
                binding.btnGoogleSignIn.setText(com.restaurant.viettable.R.string.auth_google_authenticating);
                MobileRepository.getInstance().loginWithGoogle(idToken, new MobileRepository.Callback<User>() {
                    @Override public void onSuccess(User value) {
                        if (binding == null) return;
                        binding.btnGoogleSignIn.setText(com.restaurant.viettable.R.string.auth_google_success_state);
                        showToast(getString(com.restaurant.viettable.R.string.auth_google_success));
                        navigateTo(new BranchListFragment());
                    }

                    @Override public void onError(ApiException error) {
                        if (binding == null) return;
                        setAuthenticating(false, com.restaurant.viettable.R.string.auth_continue_with_google);
                        showError(apiError(error));
                    }
                });
            }

            @Override public void onCancelled() {
                if (binding == null) return;
                setAuthenticating(false, com.restaurant.viettable.R.string.auth_continue_with_google);
            }

            @Override public void onError(@NonNull String message) {
                if (binding == null) return;
                setAuthenticating(false, com.restaurant.viettable.R.string.auth_continue_with_google);
                showError(message);
            }
        });
    }

    private void setAuthenticating(boolean value, int googleButtonText) {
        authenticating = value;
        if (binding == null) return;
        binding.tvError.setVisibility(View.GONE);
        binding.btnSubmitLogin.setEnabled(!value);
        binding.btnGoogleSignIn.setEnabled(!value);
        binding.btnGoRegister.setEnabled(!value);
        binding.btnForgotPassword.setEnabled(!value);
        binding.btnGoogleSignIn.setText(googleButtonText);
    }

    private void showError(String message) {
        if (binding == null) return;
        binding.tvError.setVisibility(View.VISIBLE);
        binding.tvError.setText(message);
    }

    @Override
    public void onDestroyView() {
        googleSignIn.cancel();
        authenticating = false;
        super.onDestroyView();
        binding = null;
    }
}
