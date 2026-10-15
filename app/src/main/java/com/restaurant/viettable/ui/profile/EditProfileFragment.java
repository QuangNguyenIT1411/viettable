package com.restaurant.viettable.ui.profile;

import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentEditProfileBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.SafeText;

public class EditProfileFragment extends BaseFragment {

    private FragmentEditProfileBinding binding;

    public static EditProfileFragment newInstance() {
        return new EditProfileFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentEditProfileBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        User user = appStateManager.getCurrentUser();
        if (user != null) {
            binding.etEditName.setText(SafeText.clean(user.getFullName()));
            binding.etEditPhone.setText(SafeText.clean(user.getPhone()));
            binding.etEditEmail.setText(SafeText.clean(user.getEmail()));
        }

        binding.btnChangeAvatar.setOnClickListener(v -> {
            showToast("Chọn ảnh đại diện mới từ thiết bị");
        });

        binding.btnSaveProfile.setOnClickListener(v -> {
            String name = binding.etEditName.getText() != null ? binding.etEditName.getText().toString().trim() : "";
            String phone = binding.etEditPhone.getText() != null ? binding.etEditPhone.getText().toString().trim() : "";
            String email = binding.etEditEmail.getText() != null ? binding.etEditEmail.getText().toString().trim() : "";

            if (TextUtils.isEmpty(name)) {
                binding.tilEditName.setError("Vui lòng nhập họ tên");
                return;
            }

            binding.btnSaveProfile.setEnabled(false);
            MobileRepository.getInstance().updateProfile(name, phone, email, new MobileRepository.Callback<User>() {
                @Override public void onSuccess(User value) {
                    if (binding == null) return;
                    binding.btnSaveProfile.setEnabled(true);
                    showToast("Đã lưu thay đổi thông tin thành công!");
                    navigateBack();
                }
                @Override public void onError(ApiException error) {
                    if (binding == null) return;
                    binding.btnSaveProfile.setEnabled(true);
                    showToast(apiError(error));
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
