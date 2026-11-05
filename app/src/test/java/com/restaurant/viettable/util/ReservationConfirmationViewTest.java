package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertSame;
import static org.junit.Assert.assertTrue;

import com.restaurant.viettable.model.Reservation;

import org.junit.Test;

import java.util.Arrays;

public class ReservationConfirmationViewTest {

    private static Reservation booking(String id, String date, String time, Reservation.Status status) {
        Reservation value = new Reservation();
        value.setId(id);
        value.setDate(date);
        value.setTimeSlot(time);
        value.setStatus(status);
        return value;
    }

    @Test
    public void afterChangingTheTimeTheFreshBookingShowsTheNewTime() {
        Reservation opened = booking("41", "2026-11-20", "18:00", Reservation.Status.PENDING);
        Reservation changed = booking("41", "2026-11-20", "19:30", Reservation.Status.PENDING);
        Reservation other = booking("7", "2026-11-21", "12:00", Reservation.Status.PENDING);

        Reservation fresh = ReservationConfirmationView.findById(Arrays.asList(other, changed), opened.getId());

        assertSame(changed, fresh);
        assertEquals("19:30 • " + DisplayFormatters.date("2026-11-20"), ReservationConfirmationView.timeLine(fresh));
    }

    @Test
    public void afterCancellingTheScreenSaysSoAndNoLongerOffersTheBooking() {
        ReservationConfirmationView view = ReservationConfirmationView.of(Reservation.Status.CANCELLED, null);
        assertEquals("Đã huỷ đặt bàn", view.title);
        assertFalse(view.standing);
    }

    @Test
    public void aPendingBookingKeepsTheRequestSentTitle() {
        ReservationConfirmationView view = ReservationConfirmationView.of(Reservation.Status.PENDING, null);
        assertEquals("Đã gửi yêu cầu đặt bàn", view.title);
        assertTrue(view.standing);
    }

    @Test
    public void aRejectedOrMissedBookingNoLongerStands() {
        assertFalse(ReservationConfirmationView.of(Reservation.Status.NO_SHOW, null).standing);
        ReservationConfirmationView rejected = ReservationConfirmationView.of(Reservation.Status.REJECTED, "Hết bàn");
        assertFalse(rejected.standing);
        assertTrue(rejected.subtitle.contains("Hết bàn"));
        // Owner 05/11: no reason, no "Lý do từ chối"; a missed booking says why it lapsed.
        assertEquals("Bạn có thể đặt giờ khác.", ReservationConfirmationView.of(Reservation.Status.REJECTED, null).subtitle);
        assertEquals("Bạn chưa đến trong thời gian giữ bàn. Bạn có thể đặt bàn mới khi cần.",
                ReservationConfirmationView.of(Reservation.Status.NO_SHOW, null).subtitle);
    }

    @Test
    public void everyOpenStatusStillOffersTheDetailScreen() {
        for (Reservation.Status status : new Reservation.Status[] {Reservation.Status.PENDING_DEPOSIT, Reservation.Status.CONFIRMED,
                Reservation.Status.CHECKING_IN, Reservation.Status.CHECKED_IN, Reservation.Status.COMPLETED}) {
            assertTrue(status.name(), ReservationConfirmationView.of(status, null).standing);
        }
    }

    @Test
    public void aBookingMissingFromTheListIsNotFound() {
        assertNull(ReservationConfirmationView.findById(Arrays.asList(booking("7", "2026-11-21", "12:00", Reservation.Status.PENDING)), "41"));
        assertNull(ReservationConfirmationView.findById(null, "41"));
    }
}
