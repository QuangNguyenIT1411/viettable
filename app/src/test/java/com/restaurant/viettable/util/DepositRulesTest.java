package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

import com.restaurant.viettable.model.DepositPolicy;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.network.ApiException;

import org.junit.Test;

import java.time.Instant;

/** Reservation deposits in the app (owner 27/10/2026, docs/DEPOSIT_PLAN.md): numbers from the server's policy. */
public class DepositRulesTest {
    private static final DepositPolicy ON = new DepositPolicy(true, 6, 50_000, 15, 2);

    @Test public void amount_comes_from_the_policy_and_starts_at_the_threshold() {
        assertEquals(0, DepositRules.requiredAmount(ON, 5));
        assertEquals(300_000, DepositRules.requiredAmount(ON, 6));
        assertEquals(500_000, DepositRules.requiredAmount(ON, 10));
        assertEquals(0, DepositRules.requiredAmount(DepositPolicy.OFF, 10));   // feature off or not loaded yet
        assertEquals(0, DepositRules.requiredAmount(null, 10));
    }

    @Test public void guest_step_notice_shows_the_real_amount_or_nothing() {
        assertNull(DepositRules.notice(ON, 4));
        assertNull(DepositRules.notice(DepositPolicy.OFF, 8));
        String text = DepositRules.notice(ON, 6);
        assertTrue(text, text.contains("50.000đ/khách") && text.contains("300.000đ") && text.contains("15 phút"));
    }

    @Test public void countdown_reads_minutes_and_seconds_and_stops_at_zero() {
        assertEquals("15:00", DepositRules.countdown(15 * 60_000));
        assertEquals("14:05", DepositRules.countdown(14 * 60_000 + 4_200));
        assertEquals("00:01", DepositRules.countdown(1));
        assertEquals("00:00", DepositRules.countdown(-5_000));
    }

    @Test public void cancel_notice_follows_the_two_hour_rule() {
        Instant reservedAt = Instant.parse("2026-10-29T12:00:00Z");
        assertTrue(DepositRules.cancelNotice("PAID", 300_000, reservedAt, reservedAt.minusSeconds(3 * 3600), 2).contains("sẽ hoàn 300.000đ"));
        assertTrue(DepositRules.cancelNotice("PAID", 300_000, reservedAt, reservedAt.minusSeconds(2 * 3600), 2).contains("sẽ hoàn"));
        assertTrue(DepositRules.cancelNotice("PAID", 300_000, reservedAt, reservedAt.minusSeconds(3600), 2).contains("không được hoàn"));
        assertTrue(DepositRules.cancelNotice("PENDING", 300_000, reservedAt, reservedAt.minusSeconds(60), 2).contains("không mất phí"));
        assertTrue(DepositRules.cancelNotice("NOT_REQUIRED", 0, reservedAt, reservedAt, 2).contains("không có tiền cọc"));
    }

    @Test public void every_server_state_has_a_line() {
        for (String s : new String[]{"PENDING", "PAID", "APPLIED", "REFUND_REQUIRED", "REFUNDED", "FORFEITED", "EXPIRED", "CANCELLED"})
            assertTrue(s, DepositRules.statusLine(s, 300_000) != null);
        assertNull(DepositRules.statusLine("PAID", 0));
        assertTrue(DepositRules.isPaid("APPLIED"));
        assertTrue(DepositRules.isClosedUnpaid("EXPIRED"));
    }

    @Test public void booking_waiting_for_its_deposit_cannot_be_changed_yet() {
        Instant now = Instant.parse("2026-10-27T10:00:00Z");
        Instant later = now.plusSeconds(86_400);
        assertEquals(false, ReservationSchedule.canChange(Reservation.Status.PENDING_DEPOSIT, later, now));
        assertEquals(true, ReservationSchedule.canCancel(Reservation.Status.PENDING_DEPOSIT, later, now));
        assertEquals(true, ReservationSchedule.canChange(Reservation.Status.PENDING, later, now));
    }

    @Test public void quote_locked_from_the_server_is_explained_like_the_old_code() {
        LoyaltyApplyRules.Outcome outcome = LoyaltyApplyRules.ofError(
                new ApiException(409, "quote_locked", "Đã yêu cầu thanh toán, không đổi được voucher/điểm.", null));
        assertEquals(LoyaltyApplyRules.Kind.QUOTE_LOCKED, outcome.kind);
    }
}
