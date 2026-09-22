package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.material.chip.Chip;
import com.restaurant.viettable.databinding.FragmentResChangeBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.time.LocalDate;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Locale;

public class ResChangeFragment extends BaseFragment {

    private static final String ARG_RES = "arg_res";
    private FragmentResChangeBinding binding;
    private Reservation reservation;

    private final List<String> dates = new ArrayList<>();
    private final List<String> times = Arrays.asList("18:30", "19:00", "19:30", "20:00");
    private String selectedDate;
    private String selectedTime;
    private int guests = 4;

    public static ResChangeFragment newInstance(Reservation res) {
        ResChangeFragment f = new ResChangeFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_RES, res);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            reservation = (Reservation) getArguments().getSerializable(ARG_RES);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentResChangeBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        if (reservation == null) {
            showToast("Không tìm thấy thông tin đặt bàn.");
            navigateBack();
            return;
        }

        if (reservation != null) {
            binding.tvResChangeId.setText("Mã đặt bàn: #" + reservation.getId());
            selectedDate = reservation.getDate();
            selectedTime = reservation.getTimeSlot();
            guests = reservation.getGuestCount();
            binding.tvChangeGuestCount.setText(String.valueOf(guests));

            DateTimeFormatter formatter = DateTimeFormatter.ofPattern("EEEE, dd/MM/yyyy", new Locale("vi", "VN"));
            for (int offset = 1; offset <= 7; offset++) dates.add(LocalDate.now().plusDays(offset).format(formatter));

            // Date chips
            binding.chipGroupChangeDate.removeAllViews();
            for (String d : dates) {
                Chip chip = new Chip(getContext());
                chip.setText(d);
                chip.setCheckable(true);
                if (d.contains(selectedDate) || selectedDate.contains(d)) chip.setChecked(true);
                chip.setOnCheckedChangeListener((btn, isChecked) -> {
                    if (isChecked) selectedDate = d;
                });
                binding.chipGroupChangeDate.addView(chip);
            }

            // Time chips
            binding.chipGroupChangeTime.removeAllViews();
            for (String t : times) {
                Chip chip = new Chip(getContext());
                chip.setText(t);
                chip.setCheckable(true);
                if (t.equals(selectedTime)) chip.setChecked(true);
                chip.setOnCheckedChangeListener((btn, isChecked) -> {
                    if (isChecked) selectedTime = t;
                });
                binding.chipGroupChangeTime.addView(chip);
            }

            binding.btnChangeMinusGuest.setOnClickListener(v -> {
                if (guests > 1) {
                    guests--;
                    binding.tvChangeGuestCount.setText(String.valueOf(guests));
                }
            });

            binding.btnChangePlusGuest.setOnClickListener(v -> {
                guests++;
                binding.tvChangeGuestCount.setText(String.valueOf(guests));
            });

            binding.btnSubmitChange.setOnClickListener(v -> {
                String note = binding.etChangeNote.getText() != null ? binding.etChangeNote.getText().toString().trim() : "";
                binding.btnSubmitChange.setEnabled(false);
                MobileRepository.getInstance().updateReservation(reservation, selectedDate, selectedTime, guests, note,
                        new MobileRepository.Callback<Reservation>() {
                            @Override public void onSuccess(Reservation value) {
                                if (binding == null) return;
                                binding.btnSubmitChange.setEnabled(true);
                                showToast("Đã cập nhật thay đổi đặt bàn thành công!");
                                navigateBack();
                            }
                            @Override public void onError(ApiException error) {
                                if (binding == null) return;
                                binding.btnSubmitChange.setEnabled(true);
                                showToast(apiError(error));
                            }
                        });
            });
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
