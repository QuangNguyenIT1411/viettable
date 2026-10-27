package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;

import java.util.TimeZone;

import org.junit.Test;

public class DisplayFormattersTest {
    @Test
    public void dateTime_treatsOffsetlessApiTimestampAsUtcAndConvertsToDeviceZone() {
        TimeZone original = TimeZone.getDefault();
        try {
            TimeZone.setDefault(TimeZone.getTimeZone("Asia/Ho_Chi_Minh"));
            assertEquals("15:58, 21/09/2026",
                    DisplayFormatters.dateTime("2026-09-21T08:58:46.3539593"));
            assertEquals("15:58, 21/09/2026",
                    DisplayFormatters.dateTime("2026-09-21T08:58:46Z"));
            assertEquals("08:58, 21/09/2026",
                    DisplayFormatters.dateTime("2026-09-21T08:58:46+07:00"));
        } finally {
            TimeZone.setDefault(original);
        }
    }

    @Test
    public void dateTime_doesNotExposeMalformedIsoValue() {
        assertEquals("Chưa rõ thời gian", DisplayFormatters.dateTime("2026-badTvalue"));
    }

    @Test
    public void dateTime_preservesAlreadyFormattedBusinessValue() {
        assertEquals("18:30, 23/09/2026", DisplayFormatters.dateTime("18:30, 23/09/2026"));
    }

    @Test
    public void date_formatsIsoDateAndTimestamp() {
        TimeZone original = TimeZone.getDefault();
        try {
            TimeZone.setDefault(TimeZone.getTimeZone("Asia/Ho_Chi_Minh"));
            assertEquals("16/09/2036", DisplayFormatters.date("2036-09-15T17:41:47.2014634Z"));
            assertEquals("00:41", DisplayFormatters.time("2036-09-15T17:41:47.2014634Z"));
        } finally {
            TimeZone.setDefault(original);
        }
        assertEquals("23/09/2026", DisplayFormatters.date("2026-09-23"));
    }

    @Test
    public void startedAt_showsDateOnlyWhenNotToday() {
        java.time.ZoneId vn = java.time.ZoneId.of("Asia/Ho_Chi_Minh");
        // Non-breaking spaces (\u00A0): "Bắt đầu 19:02" never splits across two lines.
        assertEquals("Bắt\u00A0đầu\u00A019:02",
                DisplayFormatters.startedAt("2026-10-26T12:02:00Z", vn, java.time.LocalDate.of(2026, 10, 26)));
        assertEquals("Bắt\u00A0đầu\u00A019:02,\u00A025/10/2026",
                DisplayFormatters.startedAt("2026-10-25T12:02:00Z", vn, java.time.LocalDate.of(2026, 10, 26)));
        // 23:30 UTC is already the next day in Viet Nam.
        assertEquals("Bắt\u00A0đầu\u00A006:30",
                DisplayFormatters.startedAt("2026-10-25T23:30:00Z", vn, java.time.LocalDate.of(2026, 10, 26)));
        assertEquals("Chưa rõ giờ bắt đầu", DisplayFormatters.startedAt(null, vn, java.time.LocalDate.of(2026, 10, 26)));
    }

    @Test
    public void tableTitle_addsBanOnce() {
        assertEquals("Bàn B4", DisplayFormatters.tableTitle("B4"));
        assertEquals("Bàn 5", DisplayFormatters.tableTitle("Bàn 5"));
        assertEquals("Bàn của bạn", DisplayFormatters.tableTitle(""));
    }
}
