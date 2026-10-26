package com.restaurant.viettable.util;

import com.restaurant.viettable.model.Reservation;

import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/**
 * Booking reminders on the phone (reservation item 3, 26/10): which reminders a reservation gets and their text.
 * Default 60 and 15 minutes before the reserved time; a mark already past is skipped (booked 40 minutes ahead:
 * only the 15-minute one; less than 15 minutes ahead: none). Only open bookings (pending or confirmed) are reminded.
 */
public final class ReminderPlan {
    private ReminderPlan() { }

    public static final List<Integer> DEFAULT_OFFSETS = Collections.unmodifiableList(java.util.Arrays.asList(60, 15));

    public static final class Reminder {
        public final int offsetMinutes;
        public final Instant fireAt;
        Reminder(int offsetMinutes, Instant fireAt) { this.offsetMinutes = offsetMinutes; this.fireAt = fireAt; }
    }

    /** "60,15" → [60, 15]; blank or invalid → the default. Test builds use "3,1". */
    public static List<Integer> parseOffsets(String csv) {
        List<Integer> result = new ArrayList<>();
        if (csv != null) for (String part : csv.split(",")) {
            try {
                int minutes = Integer.parseInt(part.trim());
                if (minutes > 0 && minutes <= 24 * 60 && !result.contains(minutes)) result.add(minutes);
            } catch (NumberFormatException ignored) { }
        }
        if (result.isEmpty()) return DEFAULT_OFFSETS;
        Collections.sort(result, Collections.reverseOrder());
        return Collections.unmodifiableList(result);
    }

    public static boolean remindable(Reservation.Status status) {
        return status == Reservation.Status.PENDING || status == Reservation.Status.CONFIRMED;
    }

    /** The reminders still to come for this reservation (none when it is closed or the time is unknown). */
    public static List<Reminder> plan(Reservation.Status status, Instant reservedAt, Instant now, List<Integer> offsets) {
        List<Reminder> result = new ArrayList<>();
        if (!remindable(status) || reservedAt == null || now == null) return result;
        for (int offset : offsets) {
            Instant fireAt = reservedAt.minus(Duration.ofMinutes(offset));
            if (!fireAt.isBefore(now)) result.add(new Reminder(offset, fireAt));
        }
        return result;
    }

    /** "Còn 15 phút nữa tới giờ hẹn 19:00 tại Chi nhánh Quận 1 – Bàn 8". */
    public static String message(int offsetMinutes, String time, String branchName, String tableLabel) {
        StringBuilder text = new StringBuilder("Còn ").append(offsetMinutes).append(" phút nữa tới giờ hẹn");
        if (!SafeText.isBlank(time)) text.append(' ').append(time.trim());
        if (!SafeText.isBlank(branchName)) text.append(" tại ").append(branchName.trim());
        if (!SafeText.isBlank(tableLabel) && !tableLabel.trim().equals("0") && !tableLabel.startsWith("Chưa"))
            text.append(" – ").append(tableLabel.trim());
        return text.toString();
    }
}
