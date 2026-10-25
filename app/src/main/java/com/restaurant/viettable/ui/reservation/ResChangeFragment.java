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

import com.restaurant.viettable.util.ReservationSchedule;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

public class ResChangeFragment extends BaseFragment {

    private static final String ARG_RES = "arg_res";
    private FragmentResChangeBinding binding;
    private Reservation reservation;

    private final List<String> dates = new ArrayList<>();
    private static final int MAX_GUESTS = 30;
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

            // Same days and slots as a new booking; the reservation's own day and time stay selected (it used to
            // offer only 18:30–20:00, forcing a lunch booking into the evening).
            DateTimeFormatter formatter = DateTimeFormatter.ofPattern("EEEE, dd/MM/yyyy", new Locale("vi", "VN"));
            LocalDateTime now = LocalDateTime.now();
            LocalDate current = ReservationSchedule.parseDate(selectedDate, now.toLocalDate());
            List<LocalDate> days = new ArrayList<>(ReservationSchedule.bookableDays(now));
            if (current != null && !days.contains(current) && !current.isBefore(now.toLocalDate())) days.add(0, current);
            dates.clear();
            for (LocalDate day : days) dates.add(day.format(formatter));

            binding.chipGroupChangeDate.removeAllViews();
            for (int i = 0; i < days.size(); i++) {
                LocalDate day = days.get(i);
                String label = dates.get(i);
                Chip chip = new Chip(getContext());
                chip.setText(label);
                chip.setCheckable(true);
                if (day.equals(current)) chip.setChecked(true);
                chip.setOnCheckedChangeListener((btn, isChecked) -> {
                    if (!isChecked) return;
                    selectedDate = label;
                    renderTimes(day);
                });
                binding.chipGroupChangeDate.addView(chip);
            }
            if (current == null || !days.contains(current)) { current = days.get(0); selectedDate = dates.get(0); }
            renderTimes(current);

            binding.btnChangeMinusGuest.setOnClickListener(v -> {
                if (guests > 1) {
                    guests--;
                    binding.tvChangeGuestCount.setText(String.valueOf(guests));
                }
            });

            binding.btnChangePlusGuest.setOnClickListener(v -> {
                if (guests >= MAX_GUESTS) { showToast("Tối đa " + MAX_GUESTS + " khách cho một đặt bàn."); return; }
                guests++;
                binding.tvChangeGuestCount.setText(String.valueOf(guests));
            });

            binding.btnSubmitChange.setOnClickListener(v -> {
                if (selectedTime == null) { showToast("Ngày này đã hết giờ nhận đặt bàn. Vui lòng chọn ngày khác."); return; }
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

    /** Slots of that day; the current time stays selected when still bookable, else the first bookable one. */
    private void renderTimes(LocalDate day) {
        if (binding == null) return;
        LocalDateTime now = LocalDateTime.now();
        selectedTime = ReservationSchedule.firstSelectable(day, now, selectedTime);
        binding.chipGroupChangeTime.removeAllViews();
        for (String t : ReservationSchedule.slots()) {
            boolean bookable = ReservationSchedule.selectable(day, t, now);
            Chip chip = new Chip(getContext());
            chip.setText(t);
            chip.setCheckable(bookable);
            chip.setEnabled(bookable);
            if (t.equals(selectedTime)) chip.setChecked(true);
            chip.setOnCheckedChangeListener((btn, isChecked) -> { if (isChecked) selectedTime = t; });
            binding.chipGroupChangeTime.addView(chip);
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
