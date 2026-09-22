package com.restaurant.viettable.util;

import java.time.Instant;
import java.time.LocalDateTime;
import java.time.LocalDate;
import java.time.OffsetDateTime;
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
            return OffsetDateTime.parse(normalized)
                    .atZoneSameInstant(ZoneId.systemDefault()).format(DATE_TIME);
        } catch (DateTimeParseException ignored) {
            try {
                return Instant.parse(normalized).atZone(ZoneId.systemDefault()).format(DATE_TIME);
            } catch (DateTimeParseException ignoredAgain) {
                try {
                    return LocalDateTime.parse(normalized).format(DATE_TIME);
                } catch (DateTimeParseException ignoredLocal) {
                    return normalized.contains("T") ? "Chưa rõ thời gian" : normalized;
                }
            }
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
}
