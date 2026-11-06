package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
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
        assertEquals("Lời mời đã hết hạn", WaitlistText.status(e));
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

    private static WaitlistEntry entry(String id, String status, String start, String end) {
        WaitlistEntry e = new WaitlistEntry();
        e.id = id; e.status = status; e.branchId = "1"; e.partySize = 6; e.windowStartUtc = start; e.windowEndUtc = end;
        return e;
    }

    @Test
    public void rejoinOnlyAfterALapsedOfferWhileTheWindowIsAheadAndNotAlreadyBack() {
        Instant now = Instant.parse("2026-11-20T10:00:00Z");
        WaitlistEntry lapsed = entry("5", "Expired", "2026-11-20T11:00:00Z", "2026-11-20T13:00:00Z");
        assertTrue(WaitlistText.canRejoin(lapsed, Arrays.asList(lapsed), now));
        WaitlistEntry back = entry("6", "Waiting", "2026-11-20T11:00:00Z", "2026-11-20T13:00:00Z");
        assertFalse(WaitlistText.canRejoin(lapsed, Arrays.asList(lapsed, back), now));
        assertFalse(WaitlistText.canRejoin(lapsed, Arrays.asList(lapsed), Instant.parse("2026-11-20T12:50:00Z"))); // window nearly over
        assertFalse(WaitlistText.canRejoin(entry("7", "Cancelled", "2026-11-20T11:00:00Z", "2026-11-20T13:00:00Z"), null, now));
    }

    @Test
    public void lapsedOffersAreToldOnceAndOnlyRecentOnes() {
        Instant now = Instant.parse("2026-11-20T10:00:00Z");
        WaitlistEntry recent = entry("5", "Expired", "2026-11-20T11:00:00Z", "2026-11-20T13:00:00Z");
        recent.offerExpiresAtUtc = "2026-11-20T09:50:00Z";
        WaitlistEntry old = entry("4", "Expired", "2026-11-18T11:00:00Z", "2026-11-18T13:00:00Z");
        old.offerExpiresAtUtc = "2026-11-18T11:20:00Z";
        WaitlistEntry waiting = entry("6", "Waiting", "2026-11-20T11:00:00Z", "2026-11-20T13:00:00Z");
        List<WaitlistEntry> all = Arrays.asList(recent, old, waiting);
        assertEquals(1, WaitlistText.lapsedToTell(all, new java.util.HashSet<>(), now).size());
        assertEquals("5", WaitlistText.lapsedToTell(all, new java.util.HashSet<>(), now).get(0).id);
        assertTrue(WaitlistText.lapsedToTell(all, new java.util.HashSet<>(Arrays.asList("5")), now).isEmpty());
    }

    @Test
    public void lapsedTextSaysNextGuestOnlyWhenTheServerSaysSo() {
        WaitlistEntry e = entry("5", "Expired", "2026-11-20T11:00:00Z", "2026-11-20T13:00:00Z");
        e.closedReason = "Hết thời gian xác nhận, bàn đã mời khách kế tiếp";
        assertEquals("Bạn chưa xác nhận kịp nên bàn đã được mời cho khách kế tiếp.", WaitlistText.lapsedDetail(e));
        e.closedReason = "Hết thời gian xác nhận, bàn đã được nhả";
        assertEquals("Bạn chưa xác nhận kịp nên bàn đã được nhả.", WaitlistText.lapsedDetail(e));
        e.closedReason = "Hết thời gian xác nhận"; // entries closed before 06/11
        assertEquals("Bạn chưa xác nhận kịp nên lời mời đã hết hạn.", WaitlistText.lapsedDetail(e));
    }

    @Test
    public void offerNotificationsAreClearedOnceTheOfferIsOver() {
        WaitlistEntry offered = entry("1", "Offered", "2026-11-20T11:00:00Z", "2026-11-20T13:00:00Z");
        WaitlistEntry accepted = entry("2", "Accepted", "2026-11-20T11:00:00Z", "2026-11-20T13:00:00Z");
        WaitlistEntry skipped = entry("3", "Cancelled", "2026-11-20T11:00:00Z", "2026-11-20T13:00:00Z");
        WaitlistEntry lapsed = entry("4", "Expired", "2026-11-20T11:00:00Z", "2026-11-20T13:00:00Z");
        WaitlistEntry neverShown = entry("5", "Cancelled", "2026-11-20T11:00:00Z", "2026-11-20T13:00:00Z");
        java.util.Set<String> notified = new java.util.HashSet<>(Arrays.asList("1:80", "2:81", "3:82", "4:83"));
        assertEquals(Arrays.asList("2", "3", "4"),
                WaitlistText.offersToClear(Arrays.asList(offered, accepted, skipped, lapsed, neverShown), notified));
    }
}
