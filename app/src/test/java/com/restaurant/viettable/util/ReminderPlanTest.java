package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import com.restaurant.viettable.model.Reservation;

import org.junit.Test;

import java.time.Instant;
import java.util.Arrays;
import java.util.List;

/** Reservation item 3 (26/10): which booking reminders the phone schedules, and their text. */
public class ReminderPlanTest {
    private static final Instant AT = Instant.parse("2026-10-26T12:00:00Z"); // 19:00 restaurant time

    private static List<Integer> minutes(List<ReminderPlan.Reminder> plan) {
        Integer[] result = new Integer[plan.size()];
        for (int i = 0; i < plan.size(); i++) result[i] = plan.get(i).offsetMinutes;
        return Arrays.asList(result);
    }

    @Test public void far_ahead_gets_both_reminders_at_the_right_times() {
        List<ReminderPlan.Reminder> plan = ReminderPlan.plan(Reservation.Status.CONFIRMED, AT, AT.minusSeconds(3 * 3600), ReminderPlan.DEFAULT_OFFSETS);
        assertEquals(Arrays.asList(60, 15), minutes(plan));
        assertEquals(Instant.parse("2026-10-26T11:00:00Z"), plan.get(0).fireAt);
        assertEquals(Instant.parse("2026-10-26T11:45:00Z"), plan.get(1).fireAt);
    }

    @Test public void less_than_60_minutes_ahead_drops_the_60_minute_reminder_and_less_than_15_gets_none() {
        assertEquals(Arrays.asList(15), minutes(ReminderPlan.plan(Reservation.Status.PENDING, AT, AT.minusSeconds(40 * 60), ReminderPlan.DEFAULT_OFFSETS)));
        assertEquals(Arrays.asList(15), minutes(ReminderPlan.plan(Reservation.Status.PENDING, AT, AT.minusSeconds(15 * 60), ReminderPlan.DEFAULT_OFFSETS)));
        assertTrue(ReminderPlan.plan(Reservation.Status.PENDING, AT, AT.minusSeconds(14 * 60), ReminderPlan.DEFAULT_OFFSETS).isEmpty());
    }

    @Test public void only_open_bookings_are_reminded() {
        Instant before = AT.minusSeconds(3 * 3600);
        assertTrue(ReminderPlan.remindable(Reservation.Status.PENDING));
        assertTrue(ReminderPlan.remindable(Reservation.Status.CONFIRMED));
        for (Reservation.Status closed : new Reservation.Status[] { Reservation.Status.CANCELLED, Reservation.Status.REJECTED,
                Reservation.Status.NO_SHOW, Reservation.Status.CHECKING_IN, Reservation.Status.CHECKED_IN, Reservation.Status.COMPLETED }) {
            assertFalse(closed.name(), ReminderPlan.remindable(closed));
            assertTrue(closed.name(), ReminderPlan.plan(closed, AT, before, ReminderPlan.DEFAULT_OFFSETS).isEmpty());
        }
        assertTrue(ReminderPlan.plan(Reservation.Status.CONFIRMED, null, before, ReminderPlan.DEFAULT_OFFSETS).isEmpty());
    }

    @Test public void offsets_come_from_the_build_with_a_safe_default() {
        assertEquals(Arrays.asList(60, 15), ReminderPlan.parseOffsets("60,15"));
        assertEquals(Arrays.asList(3, 1), ReminderPlan.parseOffsets("1, 3"));
        assertEquals(ReminderPlan.DEFAULT_OFFSETS, ReminderPlan.parseOffsets(""));
        assertEquals(ReminderPlan.DEFAULT_OFFSETS, ReminderPlan.parseOffsets("abc,0,-5"));
        assertEquals(Arrays.asList(3, 1), minutes(ReminderPlan.plan(Reservation.Status.CONFIRMED, AT, AT.minusSeconds(20 * 60), ReminderPlan.parseOffsets("3,1"))));
    }

    @Test public void text_names_time_branch_and_table() {
        assertEquals("Còn 15 phút nữa tới giờ hẹn 19:00 tại Chi nhánh Quận 1 – Bàn 8",
                ReminderPlan.message(15, "19:00", "Chi nhánh Quận 1", "Bàn 8"));
        assertEquals("Còn 60 phút nữa tới giờ hẹn 19:00 tại Chi nhánh Quận 1",
                ReminderPlan.message(60, "19:00", "Chi nhánh Quận 1", "Chưa có thông tin bàn"));
    }
}
