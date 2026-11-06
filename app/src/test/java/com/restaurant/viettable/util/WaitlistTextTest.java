package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

import com.restaurant.viettable.model.WaitlistEntry;

import org.junit.Test;

import java.time.Instant;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.ZoneId;
import java.util.Arrays;
import java.util.List;

public class WaitlistTextTest {
    private static final ZoneId VN = ZoneId.of("Asia/Ho_Chi_Minh");

    @Test
    public void statusSaysThePlaceOrTheOffer() {
        WaitlistEntry e = new WaitlistEntry();
        e.status = "Waiting"; e.position = 2;
        assertEquals("Đang chờ · vị trí thứ 2", WaitlistText.status(e));
        e.status = "Offered";
        assertEquals("Có bàn cho bạn", WaitlistText.status(e));
        e.status = "Expired";
        assertEquals("Hết thời gian giữ bàn", WaitlistText.status(e));
    }

    @Test
    public void windowAndClockAreLocalTime() {
        assertEquals("18:00–20:00, 20/11", WaitlistText.window("2026-11-20T11:00:00Z", "2026-11-20T13:00:00Z", VN));
        assertEquals("19:30", WaitlistText.clock("2026-11-20T12:30:00Z", VN));
    }

    @Test
    public void countdownToTheDeadline() {
        Instant now = Instant.parse("2026-11-20T12:00:00Z");
        assertEquals("còn 9:05", WaitlistText.countdown("2026-11-20T12:09:05Z", now));
        assertEquals("hết giờ", WaitlistText.countdown("2026-11-20T11:59:59Z", now));
        assertEquals("", WaitlistText.countdown(null, now));
    }

    @Test
    public void slotsStartAtLeastFifteenMinutesAhead() {
        List<String> slots = WaitlistWindow.slots(LocalDate.of(2026, 11, 20), LocalDateTime.of(2026, 11, 20, 18, 20));
        assertEquals("19:00", slots.get(0));
        assertEquals("21:30", slots.get(slots.size() - 1));
    }

    @Test
    public void defaultWindowIsTheBookingTimePlusAnHour() {
        List<String> slots = Arrays.asList("18:00", "18:30", "19:00", "19:30", "20:00");
        assertEquals("19:00", WaitlistWindow.defaultFrom(slots, "19:00"));
        assertEquals("20:00", WaitlistWindow.defaultTo(slots, "19:00"));
        assertEquals("20:00", WaitlistWindow.defaultTo(slots, "19:30"));
        assertEquals("18:00", WaitlistWindow.defaultFrom(slots, "12:00"));
    }

    @Test
    public void windowProblems() {
        assertNull(WaitlistWindow.problem("18:00", "20:00"));
        assertTrue(WaitlistWindow.problem("20:00", "18:00").contains("sau"));
        assertTrue(WaitlistWindow.problem("10:00", "17:00").contains("6"));
        assertTrue(WaitlistWindow.problem(null, "17:00").contains("Chọn"));
    }
}
