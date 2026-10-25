package com.restaurant.viettable.util;

import com.restaurant.viettable.model.Reservation;

import java.time.Duration;
import java.time.Instant;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * Booking times and which reservation actions are possible (reservation review 25/10, A4–A6). Mirrors the server:
 * bookings 10:00–21:30 every 30 minutes, at least 15 minutes ahead; guests may check in 30 minutes either side.
 */
public final class ReservationSchedule {
    private ReservationSchedule() { }

    public static final LocalTime OPEN_FROM = LocalTime.of(10, 0);
    public static final LocalTime LAST_SLOT = LocalTime.of(21, 30);
    public static final LocalTime EVENING_FROM = LocalTime.of(15, 0);
    public static final int SLOT_MINUTES = 30;
    public static final int MIN_LEAD_MINUTES = 15;
    public static final int ARRIVAL_WINDOW_MINUTES = 30;
    public static final int DAYS_AHEAD = 7;

    private static final Pattern DAY_MONTH_YEAR = Pattern.compile("(\\d{1,2})/(\\d{1,2})(?:/(\\d{4}))?");

    /** "10:00", "10:30" … "21:30". */
    public static List<String> slots() {
        List<String> result = new ArrayList<>();
        for (LocalTime t = OPEN_FROM; !t.isAfter(LAST_SLOT); t = t.plusMinutes(SLOT_MINUTES)) result.add(t.toString());
        return Collections.unmodifiableList(result);
    }

    public static boolean isLunch(String slot) { return LocalTime.parse(slot).isBefore(EVENING_FROM); }

    /** A slot of a past day, or less than 15 minutes from now today, cannot be booked. */
    public static boolean selectable(LocalDate date, String slot, LocalDateTime now) {
        return !date.atTime(LocalTime.parse(slot)).isBefore(now.plusMinutes(MIN_LEAD_MINUTES));
    }

    /** The preferred slot when still bookable on that day, otherwise the first bookable one; null when none is left. */
    public static String firstSelectable(LocalDate date, LocalDateTime now, String preferred) {
        if (preferred != null && slots().contains(preferred) && selectable(date, preferred, now)) return preferred;
        for (String slot : slots()) if (selectable(date, slot, now)) return slot;
        return null;
    }

    /** Today and the next days a guest can pick; today only while it still has a bookable slot. */
    public static List<LocalDate> bookableDays(LocalDateTime now) {
        List<LocalDate> days = new ArrayList<>();
        LocalDate today = now.toLocalDate();
        if (firstSelectable(today, now, null) != null) days.add(today);
        for (int offset = 1; days.size() < DAYS_AHEAD; offset++) days.add(today.plusDays(offset));
        return days;
    }

    /** Reads "…dd/MM/yyyy" (the app's date text); a missing year means the next such day. */
    public static LocalDate parseDate(String text, LocalDate today) {
        Matcher matcher = DAY_MONTH_YEAR.matcher(text == null ? "" : text);
        if (!matcher.find()) return null;
        int day = Integer.parseInt(matcher.group(1));
        int month = Integer.parseInt(matcher.group(2));
        boolean hasYear = matcher.group(3) != null;
        LocalDate date = LocalDate.of(hasYear ? Integer.parseInt(matcher.group(3)) : today.getYear(), month, day);
        return !hasYear && date.isBefore(today) ? date.plusYears(1) : date;
    }

    private static boolean open(Reservation.Status status) {
        return status == Reservation.Status.PENDING || status == Reservation.Status.PENDING_DEPOSIT
                || status == Reservation.Status.CONFIRMED;
    }

    /** Change time/guests: still open and the reserved time has not come yet. */
    public static boolean canChange(Reservation.Status status, Instant reservedAt, Instant now) {
        return open(status) && reservedAt != null && now.isBefore(reservedAt);
    }

    /** Cancel: still open and not past the arrival window (the server marks it "Không đến" then). */
    public static boolean canCancel(Reservation.Status status, Instant reservedAt, Instant now) {
        return open(status) && reservedAt != null && !now.isAfter(reservedAt.plus(Duration.ofMinutes(ARRIVAL_WINDOW_MINUTES)));
    }

    /** Scan the table QR: confirmed (or a check-in in progress) and within 30 minutes of the reserved time. */
    public static boolean canCheckIn(Reservation.Status status, Instant reservedAt, Instant now) {
        return (status == Reservation.Status.CONFIRMED || status == Reservation.Status.CHECKING_IN) && reservedAt != null
                && Math.abs(Duration.between(reservedAt, now).toMinutes()) <= ARRIVAL_WINDOW_MINUTES;
    }

    /** Order dishes ahead: confirmed and before the reserved time. */
    public static boolean canPreOrder(Reservation.Status status, Instant reservedAt, Instant now) {
        return status == Reservation.Status.CONFIRMED && reservedAt != null && now.isBefore(reservedAt);
    }
}
