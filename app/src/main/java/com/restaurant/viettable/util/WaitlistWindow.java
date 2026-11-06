package com.restaurant.viettable.util;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.util.ArrayList;
import java.util.List;

/** The time choices of "Vào danh sách chờ" (owner 06/11). Pure; unit tested. */
public final class WaitlistWindow {
    private WaitlistWindow() { }

    public static final int MAX_HOURS = 6;

    /** Booking slots of the day still at least 15 minutes ahead. */
    public static List<String> slots(LocalDate day, LocalDateTime now) {
        List<String> result = new ArrayList<>();
        if (day == null) return result;
        for (String slot : ReservationSchedule.slots()) {
            if (day.atTime(LocalTime.parse(slot)).isAfter(now.plusMinutes(14))) result.add(slot);
        }
        return result;
    }

    /** The guest's booking time when it is still a choice, else the first one. */
    public static String defaultFrom(List<String> slots, String preferred) {
        if (slots.isEmpty()) return null;
        return preferred != null && slots.contains(preferred) ? preferred : slots.get(0);
    }

    /** One hour after the start when possible (else the last slot). */
    public static String defaultTo(List<String> slots, String from) {
        if (slots.isEmpty() || from == null) return null;
        LocalTime wanted = LocalTime.parse(from).plusHours(1);
        String best = slots.get(slots.size() - 1);
        for (String slot : slots) if (!LocalTime.parse(slot).isBefore(wanted)) { best = slot; break; }
        return best;
    }

    /** Null when the window is fine; otherwise what to fix. */
    public static String problem(String from, String to) {
        if (from == null || to == null) return "Chọn giờ bắt đầu và kết thúc.";
        LocalTime a = LocalTime.parse(from);
        LocalTime b = LocalTime.parse(to);
        if (b.isBefore(a)) return "Giờ kết thúc phải sau giờ bắt đầu.";
        if (java.time.Duration.between(a, b).toMinutes() > MAX_HOURS * 60L) return "Khoảng giờ chờ tối đa " + MAX_HOURS + " tiếng.";
        return null;
    }
}
