package com.restaurant.viettable.util;

import com.restaurant.viettable.model.WaitlistEntry;

import java.time.Duration;
import java.time.Instant;
import java.time.ZoneId;
import java.time.format.DateTimeFormatter;
import java.util.Locale;

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
            case "Expired": return "Hết thời gian giữ bàn";
            case "Cancelled": return "Đã huỷ chờ";
            case "Closed": return "Đã hết khoảng giờ chờ";
            default: return entry.status;
        }
    }

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
