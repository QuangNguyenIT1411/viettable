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

public class EditProfileFragment extends BaseFragment implements com.restaurant.viettable.ui.base.RequiresLogin {
    @Override public String loginPurpose() { return "thông tin cá nhân"; }


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
            birthday = user.getDateOfBirth();
        }
        // Birthday from the server's profile (no made-up date); empty until the guest picks one.
        showBirthday();
        binding.etEditBirthday.setOnClickListener(v -> pickBirthday());
        // No server field for food preferences / allergies and no avatar upload yet: hidden instead of pretending.
        binding.tilEditDietary.setVisibility(View.GONE);
        binding.btnChangeAvatar.setVisibility(View.GONE);

        binding.btnSaveProfile.setOnClickListener(v -> {
            String name = binding.etEditName.getText() != null ? binding.etEditName.getText().toString().trim() : "";
            String phone = binding.etEditPhone.getText() != null ? binding.etEditPhone.getText().toString().trim() : "";
            String email = binding.etEditEmail.getText() != null ? binding.etEditEmail.getText().toString().trim() : "";

            if (TextUtils.isEmpty(name)) {
                binding.tilEditName.setError("Vui lòng nhập họ tên");
                return;
            }

            binding.btnSaveProfile.setEnabled(false);
            MobileRepository.getInstance().updateProfile(name, phone, email, birthday, new MobileRepository.Callback<User>() {
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

    /** yyyy-MM-dd as the server keeps it; null = none. */
    private String birthday;

    private void showBirthday() {
        binding.etEditBirthday.setText(birthday == null ? "" : com.restaurant.viettable.util.DisplayFormatters.date(birthday));
    }

    private void pickBirthday() {
        long selection = com.google.android.material.datepicker.MaterialDatePicker.todayInUtcMilliseconds();
        try {
            if (birthday != null) selection = java.time.LocalDate.parse(birthday).atStartOfDay(java.time.ZoneOffset.UTC).toInstant().toEpochMilli();
        } catch (Exception ignored) { }
        com.google.android.material.datepicker.MaterialDatePicker<Long> picker = com.google.android.material.datepicker.MaterialDatePicker.Builder
                .datePicker().setTitleText("Ngày sinh").setSelection(selection)
                .setCalendarConstraints(new com.google.android.material.datepicker.CalendarConstraints.Builder()
                        .setValidator(com.google.android.material.datepicker.DateValidatorPointBackward.now()).build())
                .build();
        picker.addOnPositiveButtonClickListener(value -> {
            birthday = java.time.Instant.ofEpochMilli(value).atZone(java.time.ZoneOffset.UTC).toLocalDate().toString();
            if (binding != null) showBirthday();
        });
        picker.show(getChildFragmentManager(), "birthday");
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
