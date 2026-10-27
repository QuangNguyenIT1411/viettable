package com.restaurant.viettable.util;

import java.time.Instant;
import java.time.LocalDateTime;
import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.time.ZonedDateTime;
import java.time.ZoneId;
import java.time.format.DateTimeFormatter;
import java.time.format.DateTimeParseException;
import java.util.Locale;

/** Small, locale-aware formatters for values shown directly to customers. */
public final class DisplayFormatters {
    private static final DateTimeFormatter DATE_TIME =
            DateTimeFormatter.ofPattern("HH:mm, dd/MM/yyyy", new Locale("vi", "VN"));
    private static final DateTimeFormatter DATE = DateTimeFormatter.ofPattern("dd/MM/yyyy");

    private DisplayFormatters() {}

    public static String dateTime(String value) {
        if (value == null || value.trim().isEmpty()) return "Chưa rõ thời gian";
        String normalized = value.trim();
        try {
            ZonedDateTime local = parseUtcInstant(normalized).atZone(ZoneId.systemDefault());
            return local.format(DATE_TIME);
        } catch (DateTimeParseException ignored) {
            return normalized.contains("T") ? "Chưa rõ thời gian" : normalized;
        }
    }

    public static String date(String value) {
        if (value == null || value.trim().isEmpty()) return "Chưa rõ ngày";
        String normalized = value.trim();
        try {
            return LocalDate.parse(normalized).format(DATE);
        } catch (DateTimeParseException ignored) {
            String formatted = dateTime(normalized);
            int separator = formatted.indexOf(", ");
            if (separator >= 0) return formatted.substring(separator + 2);
            return normalized.contains("T") ? "Chưa rõ ngày" : normalized;
        }
    }

    public static String time(String value) {
        if (value == null || value.trim().isEmpty()) return "Chưa rõ giờ";
        String normalized = value.trim();
        try {
            return parseUtcInstant(normalized).atZone(ZoneId.systemDefault())
                    .format(DateTimeFormatter.ofPattern("HH:mm", new Locale("vi", "VN")));
        } catch (DateTimeParseException ignored) {
            return normalized.contains("T") ? "Chưa rõ giờ" : normalized;
        }
    }

    /**
     * "Bắt đầu 19:02" when the session started today, "Bắt đầu 19:02, 25/10/2026" otherwise. The space inside
     * "Bắt đầu 19:02" is a non-breaking one so the time never wraps alone onto the next line.
     */
    public static String startedAt(String value, ZoneId zone, LocalDate today) {
        Instant started = instant(value);
        if (started == null) return "Chưa rõ giờ bắt đầu";
        ZonedDateTime local = started.atZone(zone);
        String time = local.format(DateTimeFormatter.ofPattern("HH:mm", new Locale("vi", "VN")));
        String label = "Bắt\u00A0đầu\u00A0" + time;
        return local.toLocalDate().equals(today) ? label : label + ",\u00A0" + local.format(DATE);
    }

    /** The table as a title: "B4" -> "Bàn B4"; labels that already say "Bàn ..." stay as they are. */
    public static String tableTitle(String label) {
        String clean = SafeText.clean(label);
        if (clean.isEmpty()) return "Bàn của bạn";
        return clean.toLowerCase(new Locale("vi", "VN")).startsWith("bàn") ? clean : "Bàn " + clean;
    }

    /** The server timestamp as an instant (UTC when no offset is given); null when missing or unreadable. */
    public static Instant instant(String value) {
        if (value == null || value.trim().isEmpty()) return null;
        try { return parseUtcInstant(value.trim()); } catch (DateTimeParseException ignored) { return null; }
    }

    private static Instant parseUtcInstant(String value) {
        try {
            return OffsetDateTime.parse(value).toInstant();
        } catch (DateTimeParseException ignored) {
            try {
                return Instant.parse(value);
            } catch (DateTimeParseException ignoredAgain) {
                // The mobile API contract defines every ISO timestamp as UTC. SQL Server
                // datetime2 can lose DateTime.Kind, so some valid UTC values arrive without Z.
                return LocalDateTime.parse(value).toInstant(ZoneOffset.UTC);
            }
        }
    }
}
