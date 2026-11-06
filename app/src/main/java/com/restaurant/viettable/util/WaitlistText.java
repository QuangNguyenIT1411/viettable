package com.restaurant.viettable.util;

import com.restaurant.viettable.model.WaitlistEntry;

import java.time.Duration;
import java.time.Instant;
import java.time.ZoneId;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.Set;

/** What the waitlist screens say (owner 06/11). Pure; unit tested. */
public final class WaitlistText {
    private WaitlistText() { }

    private static final DateTimeFormatter TIME = DateTimeFormatter.ofPattern("HH:mm", new Locale("vi", "VN"));
    private static final DateTimeFormatter DAY = DateTimeFormatter.ofPattern("dd/MM", new Locale("vi", "VN"));

    public static String status(WaitlistEntry entry) {
        if (entry == null || entry.status == null) return "";
        switch (entry.status) {
            case "Waiting": return entry.position > 0 ? "Đang chờ · vị trí thứ " + entry.position : "Đang chờ";
            case "Offered": return "Có bàn cho bạn";
            case "Accepted": return "Đã nhận bàn";
            case "Expired": return "Lời mời đã hết hạn";
            case "Cancelled": return "Đã huỷ chờ";
            case "Closed": return "Đã hết khoảng giờ chờ";
            default: return entry.status;
        }
    }

    /** Why a lapsed offer is over, for the card and the dialog when the app opens. */
    public static final String LAPSED_DETAIL = "Bạn chưa xác nhận kịp nên lời mời đã hết hạn.";

    /**
     * Why a lapsed offer is over (owner 06/11): "mời cho khách kế tiếp" only when the server says someone else was
     * waiting for that time ({@code closedReason}); otherwise the table was simply released.
     */
    public static String lapsedDetail(WaitlistEntry entry) {
        String reason = entry == null || entry.closedReason == null ? "" : entry.closedReason.toLowerCase(Locale.ROOT);
        if (reason.contains("khách kế tiếp")) return "Bạn chưa xác nhận kịp nên bàn đã được mời cho khách kế tiếp.";
        if (reason.contains("nhả")) return "Bạn chưa xác nhận kịp nên bàn đã được nhả.";
        return LAPSED_DETAIL;
    }

    /** Offer notifications this phone showed whose offer is over (taken, skipped, lapsed, closed): remove them. */
    public static List<String> offersToClear(List<WaitlistEntry> all, Set<String> notifiedKeys) {
        List<String> out = new ArrayList<>();
        if (all == null || notifiedKeys == null) return out;
        for (WaitlistEntry e : all) {
            if (e == null || e.isOffered()) continue;
            for (String key : notifiedKeys) if (key.startsWith(e.id + ":")) { out.add(e.id); break; }
        }
        return out;
    }

    /**
     * "Xếp lại hàng chờ" (owner 06/11): only for an offer that lapsed, while the window is still more than 15 minutes
     * ahead and the guest is not already back in the queue for the same branch, window and party.
     */
    public static boolean canRejoin(WaitlistEntry entry, List<WaitlistEntry> all, Instant now) {
        if (entry == null || !"Expired".equals(entry.status)) return false;
        Instant end = DisplayFormatters.instant(entry.windowEndUtc);
        if (end == null || !end.isAfter(now.plusSeconds(15 * 60))) return false;
        if (all != null) for (WaitlistEntry other : all) {
            if (other == entry || other == null || !other.isActive()) continue;
            if (eq(other.branchId, entry.branchId) && eq(other.windowStartUtc, entry.windowStartUtc)
                    && eq(other.windowEndUtc, entry.windowEndUtc) && other.partySize == entry.partySize) return false;
        }
        return true;
    }

    /** Lapsed offers of the last day the guest has not been told about yet (the app shows them once on open). */
    public static List<WaitlistEntry> lapsedToTell(List<WaitlistEntry> all, Set<String> told, Instant now) {
        List<WaitlistEntry> out = new ArrayList<>();
        if (all == null) return out;
        for (WaitlistEntry e : all) {
            if (e == null || !"Expired".equals(e.status) || (told != null && told.contains(e.id))) continue;
            Instant lapsed = DisplayFormatters.instant(e.offerExpiresAtUtc);
            if (lapsed != null && lapsed.isBefore(now.minusSeconds(24 * 3600))) continue;
            out.add(e);
        }
        return out;
    }

    private static boolean eq(String a, String b) { return a == null ? b == null : a.equals(b); }

    /** "18:00–20:00, 20/11" in the phone's time zone. */
    public static String window(String startUtc, String endUtc, ZoneId zone) {
        Instant start = DisplayFormatters.instant(startUtc);
        Instant end = DisplayFormatters.instant(endUtc);
        if (start == null || end == null) return "";
        return start.atZone(zone).format(TIME) + "–" + end.atZone(zone).format(TIME) + ", " + start.atZone(zone).format(DAY);
    }

    public static String clock(String utc, ZoneId zone) {
        Instant at = DisplayFormatters.instant(utc);
        return at == null ? "" : at.atZone(zone).format(TIME);
    }

    /** "còn 9:05" to answer an offer; "hết giờ" once past; empty without a deadline. */
    public static String countdown(String expiresUtc, Instant now) {
        Instant end = DisplayFormatters.instant(expiresUtc);
        if (end == null) return "";
        long seconds = Duration.between(now, end).getSeconds();
        if (seconds <= 0) return "hết giờ";
        return String.format(Locale.ROOT, "còn %d:%02d", seconds / 60, seconds % 60);
    }
}
