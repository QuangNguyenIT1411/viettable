package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

import com.restaurant.viettable.model.Reservation;

import org.junit.Test;

import java.time.Instant;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.util.List;

/** Reservation review 25/10, items A4–A6: one slot table, no past slots, actions only when the server allows them. */
public class ReservationScheduleTest {
    private static final LocalDate DAY = LocalDate.of(2026, 10, 25);

    @Test public void slots_cover_the_booking_hours_every_30_minutes() {
        List<String> slots = ReservationSchedule.slots();
        assertEquals("10:00", slots.get(0));
        assertEquals("21:30", slots.get(slots.size() - 1));
        assertEquals(24, slots.size());
        assertTrue(ReservationSchedule.isLunch("14:30"));
        assertFalse(ReservationSchedule.isLunch("15:00"));
    }

    @Test public void today_hides_slots_less_than_15_minutes_away() {
        LocalDateTime now = DAY.atTime(18, 50);
        assertFalse(ReservationSchedule.selectable(DAY, "19:00", now));
        assertTrue(ReservationSchedule.selectable(DAY, "19:30", now));
        assertTrue(ReservationSchedule.selectable(DAY.plusDays(1), "10:00", now));
        assertEquals("19:30", ReservationSchedule.firstSelectable(DAY, now, "19:00"));
        assertEquals("20:00", ReservationSchedule.firstSelectable(DAY, now, "20:00"));   // the guest's time is kept
        assertNull(ReservationSchedule.firstSelectable(DAY, DAY.atTime(21, 20), null));
    }

    @Test public void today_is_offered_only_while_it_has_a_bookable_slot() {
        List<LocalDate> morning = ReservationSchedule.bookableDays(DAY.atTime(9, 0));
        assertEquals(DAY, morning.get(0));
        assertEquals(7, morning.size());
        List<LocalDate> late = ReservationSchedule.bookableDays(DAY.atTime(21, 20));
        assertEquals(DAY.plusDays(1), late.get(0));
        assertEquals(7, late.size());
    }

    @Test public void date_text_is_read_from_the_app_formats() {
        assertEquals(DAY, ReservationSchedule.parseDate("Chủ nhật, 25/10/2026", DAY));
        assertEquals(LocalDate.of(2027, 1, 2), ReservationSchedule.parseDate("02/01", DAY));
        assertNull(ReservationSchedule.parseDate("không rõ", DAY));
    }

    /** The booking draft keeps ISO "yyyy-MM-dd"; the weekday text is only for display (bug "Chưa rõ ngày" 27/10). */
    @Test public void draft_date_is_iso_and_formatted_only_for_display() {
        String stored = LocalDate.of(2026, 10, 29).toString();
        assertEquals("2026-10-29", stored);
        assertEquals(LocalDate.of(2026, 10, 29), ReservationSchedule.parseDate(stored, DAY));
        assertEquals("29/10/2026", DisplayFormatters.date(stored));
        assertEquals("Thứ Năm, 29/10/2026", DisplayFormatters.weekdayDate(stored));
        // The old stored text: "Thứ" starts with T and was mistaken for an ISO date-time.
        assertEquals("Chưa rõ ngày", DisplayFormatters.date("Thứ Năm, 29/10/2026"));
        // A booking loaded from the server keeps dd/MM/yyyy and still parses.
        assertEquals(LocalDate.of(2026, 10, 29), ReservationSchedule.parseDate("29/10/2026", DAY));
    }

    @Test public void actions_follow_status_and_time() {
        Instant at = Instant.parse("2026-10-25T12:00:00Z");
        Instant before = at.minusSeconds(3600);
        Instant late = at.plusSeconds(20 * 60);
        Instant tooLate = at.plusSeconds(31 * 60);
        Reservation.Status confirmed = Reservation.Status.CONFIRMED;

        assertTrue(ReservationSchedule.canChange(confirmed, at, before));
        assertFalse(ReservationSchedule.canChange(confirmed, at, late));
        assertTrue(ReservationSchedule.canCancel(confirmed, at, late));
        assertFalse(ReservationSchedule.canCancel(confirmed, at, tooLate));
        assertFalse(ReservationSchedule.canCheckIn(confirmed, at, before));
        assertTrue(ReservationSchedule.canCheckIn(confirmed, at, late));
        assertTrue(ReservationSchedule.canPreOrder(confirmed, at, before));

        for (Reservation.Status closed : new Reservation.Status[] { Reservation.Status.CANCELLED, Reservation.Status.NO_SHOW,
                Reservation.Status.CHECKED_IN, Reservation.Status.COMPLETED, Reservation.Status.REJECTED }) {
            assertFalse(closed.name(), ReservationSchedule.canChange(closed, at, before));
            assertFalse(closed.name(), ReservationSchedule.canCancel(closed, at, before));
            assertFalse(closed.name(), ReservationSchedule.canCheckIn(closed, at, late));
            assertFalse(closed.name(), ReservationSchedule.canPreOrder(closed, at, before));
        }
        assertFalse(ReservationSchedule.canCancel(Reservation.Status.CHECKING_IN, at, late));
        assertTrue(ReservationSchedule.canCheckIn(Reservation.Status.CHECKING_IN, at, late));
        assertFalse(ReservationSchedule.canChange(confirmed, null, before));
    }

    // Reservation item 2 (26/10): a booking waits for the restaurant; the guest may still check in, not order ahead.
    @Test public void pending_booking_can_check_in_change_and_cancel_but_not_order_ahead() {
        Instant at = Instant.parse("2026-10-25T12:00:00Z");
        Instant before = at.minusSeconds(3600);
        Instant late = at.plusSeconds(20 * 60);
        Reservation.Status pending = Reservation.Status.PENDING;

        assertTrue(ReservationSchedule.awaitingGuest(pending));
        assertTrue(ReservationSchedule.awaitingGuest(Reservation.Status.CONFIRMED));
        assertFalse(ReservationSchedule.awaitingGuest(Reservation.Status.REJECTED));
        assertTrue(ReservationSchedule.canCheckIn(pending, at, late));
        assertFalse(ReservationSchedule.canCheckIn(pending, at, before));
        assertTrue(ReservationSchedule.canChange(pending, at, before));
        assertTrue(ReservationSchedule.canCancel(pending, at, before));
        assertFalse(ReservationSchedule.canPreOrder(pending, at, before));
    }

    // Owner 26/10: the home card showed #18 (14:30, newest) instead of #15 (14:00).
    @Test public void upcoming_card_shows_the_soonest_open_booking_not_the_newest() {
        Instant now = Instant.parse("2026-10-26T06:00:00Z"); // 13:00 restaurant time
        Reservation r15 = booking("15", Reservation.Status.CONFIRMED, "2026-10-26T07:00:00Z");   // 14:00
        Reservation r18 = booking("18", Reservation.Status.PENDING, "2026-10-26T07:30:00Z");     // 14:30
        Reservation cancelled = booking("17", Reservation.Status.CANCELLED, "2026-10-26T06:30:00Z");
        Reservation past = booking("12", Reservation.Status.CONFIRMED, "2026-10-26T05:00:00Z"); // 12:00, window over
        List<Reservation> newestFirst = java.util.Arrays.asList(r18, cancelled, r15, past);
        assertEquals("15", ReservationSchedule.nextUpcoming(newestFirst, now).getId());
        // Still shown while the guest may arrive (within 30 minutes after the time), then the next one.
        assertEquals("15", ReservationSchedule.nextUpcoming(newestFirst, Instant.parse("2026-10-26T07:25:00Z")).getId());
        assertEquals("18", ReservationSchedule.nextUpcoming(newestFirst, Instant.parse("2026-10-26T07:31:00Z")).getId());
        assertNull(ReservationSchedule.nextUpcoming(java.util.Arrays.asList(cancelled, past), now));
    }

    private static Reservation booking(String id, Reservation.Status status, String reservedAt) {
        Reservation r = new Reservation();
        r.setId(id);
        r.setStatus(status);
        r.setReservedAt(reservedAt);
        return r;
    }

    @Test public void status_line_explains_waiting_and_shows_the_rejection_reason() {
        assertTrue(ReservationSchedule.statusInfo(Reservation.Status.PENDING, null).startsWith("Nhà hàng sẽ xác nhận sớm."));
        assertEquals("Lý do từ chối: Hết bàn. Bạn có thể đặt giờ khác.",
                ReservationSchedule.statusInfo(Reservation.Status.REJECTED, " Hết bàn "));
        assertEquals("Lý do từ chối: Bếp bảo trì. Bạn có thể đặt giờ khác.",
                ReservationSchedule.statusInfo(Reservation.Status.REJECTED, "Bếp bảo trì."));
        assertTrue(ReservationSchedule.statusInfo(Reservation.Status.REJECTED, "  ").contains("không thể nhận"));
        assertNull(ReservationSchedule.statusInfo(Reservation.Status.CONFIRMED, "Tự xác nhận khi check-in"));
        Reservation reservation = new Reservation();
        reservation.setStatus(Reservation.Status.PENDING);
        assertEquals("Chờ nhà hàng xác nhận", reservation.getStatusDisplayText());
        reservation.setStatus(Reservation.Status.REJECTED);
        assertEquals("Bị từ chối", reservation.getStatusDisplayText());
    }
}
