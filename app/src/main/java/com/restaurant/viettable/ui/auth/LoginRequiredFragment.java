package com.restaurant.viettable.ui.auth;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentLoginRequiredBinding;
import com.restaurant.viettable.ui.base.BaseFragment;

/** "Bạn chưa đăng nhập" (owner 07/11): shown instead of an account-only screen when the guest has no session. */
public class LoginRequiredFragment extends BaseFragment {
    private static final String ARG_PURPOSE = "purpose";
    private FragmentLoginRequiredBinding binding;

    public static LoginRequiredFragment newInstance(String purpose) {
        LoginRequiredFragment f = new LoginRequiredFragment();
        Bundle b = new Bundle();
        b.putString(ARG_PURPOSE, purpose);
        f.setArguments(b);
        return f;
    }

    /** "Đăng nhập để xem thông tin cá nhân." — the sentence under the title. */
    public static String message(@Nullable String purpose) {
        String what = purpose == null ? "" : purpose.trim();
        return what.isEmpty() ? "Đăng nhập để dùng chức năng này." : "Đăng nhập để xem " + what + ".";
    }

    @Nullable @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentLoginRequiredBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.tvLoginRequiredMessage.setText(message(getArguments() == null ? null : getArguments().getString(ARG_PURPOSE)));
        binding.btnLoginHere.setOnClickListener(v -> navigateTo(new LoginFragment()));
        binding.btnRegisterHere.setOnClickListener(v -> navigateTo(new RegisterFragment()));
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
