package com.restaurant.viettable.assistant;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;

import org.junit.Test;
import org.junit.runner.RunWith;
import org.junit.runners.Parameterized;

import java.time.LocalDate;
import java.time.LocalTime;
import java.util.Arrays;
import java.util.Collection;
import java.util.List;

/**
 * 06/11 booking assistant: sentences a guest may type, with and without tone marks, short forms and small slips.
 * Today is Friday 20/11/2026. Branches: 1 = Quận 1, 2 = Bình Thạnh, 3 = Gò Vấp. "-" = not understood (asked again).
 */
@RunWith(Parameterized.class)
public class BookingIntentParserTest {
    private static final LocalDate TODAY = LocalDate.of(2026, 11, 20); // Friday
    private static final List<BookingIntentParser.BranchRef> BRANCHES = Arrays.asList(
            new BookingIntentParser.BranchRef("1", "Chi nhánh Quận 1"),
            new BookingIntentParser.BranchRef("2", "Chi nhánh Bình Thạnh"),
            new BookingIntentParser.BranchRef("3", "Chi nhánh Gò Vấp"));

    @Parameterized.Parameters(name = "{0}")
    public static Collection<Object[]> cases() {
        return Arrays.asList(new Object[][]{
                // sentence, date, time, people, branch
                {"Đặt bàn tối nay 7 giờ cho 4 người ở Quận 1", "2026-11-20", "19:00", "4", "1"},
                {"dat ban toi nay 7 gio 4 nguoi quan 1", "2026-11-20", "19:00", "4", "1"},
                {"mai 19h 6 người Bình Thạnh", "2026-11-21", "19:00", "6", "2"},
                {"ngày mai 7h30 tối 2 người", "2026-11-21", "19:30", "2", "-"},
                {"mốt 18:30 nhóm 8 gò vấp", "-", "18:30", "8", "3"},
                {"ngày mốt 18:30 nhóm 8 gò vấp", "2026-11-22", "18:30", "8", "3"},
                {"ngay mot 6h chieu 3 khach", "2026-11-22", "18:00", "3", "-"},
                {"thứ 7 lúc 12 giờ trưa 5 người q1", "2026-11-21", "12:00", "5", "1"},
                {"thu 7 12h trua 5 nguoi q.1", "2026-11-21", "12:00", "5", "1"},
                {"t7 1h trưa 4 ng", "2026-11-21", "13:00", "4", "-"},
                {"chủ nhật 11h 10 người", "2026-11-22", "11:00", "10", "-"},
                {"cn 11 giờ 10 khách", "2026-11-22", "11:00", "10", "-"},
                {"thứ 6 tuần sau 8 giờ tối 4 người", "2026-11-27", "20:00", "4", "-"},
                {"thứ sáu 20h 2 người", "2026-11-20", "20:00", "2", "-"},
                {"thứ hai 19h 4 nguoi", "2026-11-23", "19:00", "4", "-"},
                {"thu ba toi 7g30 2 nguoi", "2026-11-24", "19:30", "2", "-"},
                {"15/11 19h 4 người", "2027-11-15", "19:00", "4", "-"},
                {"25/11 lúc 18:00 cho 6 người", "2026-11-25", "18:00", "6", "-"},
                {"25-11-2026 18h 6 khách bình thạnh", "2026-11-25", "18:00", "6", "2"},
                {"ngày 28 lúc 7 giờ tối 3 người", "2026-11-28", "19:00", "3", "-"},
                {"ngày 5 trưa 11h30 2 người", "2026-12-05", "11:30", "2", "-"},
                {"7 rưỡi tối mai 4 người", "2026-11-21", "19:30", "4", "-"},
                {"mai 7 gio ruoi toi 4 nguoi", "2026-11-21", "19:30", "4", "-"},
                {"hôm nay 19h hai người", "2026-11-20", "19:00", "2", "-"},
                {"hom nay 19h bon nguoi", "2026-11-20", "19:00", "4", "-"},
                {"mai 18h mười hai người", "2026-11-21", "18:00", "12", "-"},
                {"mai 18h muoi lam nguoi", "2026-11-21", "18:00", "15", "-"},
                {"mai 19h 4 người lớn 2 trẻ em", "2026-11-21", "19:00", "6", "-"},
                {"mai 19h hai người lớn một trẻ em", "2026-11-21", "19:00", "3", "-"},
                {"tối mai 7h vợ chồng tôi", "2026-11-21", "19:00", "2", "-"},
                {"mai bàn 4 người 7h tối", "2026-11-21", "19:00", "4", "-"},
                {"binh tanh mai 19h 4 nguoi", "2026-11-21", "19:00", "4", "2"},       // small slip
                {"go vap toi nay 8h 2 nguoi", "2026-11-20", "20:00", "2", "3"},
                {"Q1 tối nay 7h 2 người", "2026-11-20", "19:00", "2", "1"},
                {"quan 1 toi mai 8h 2 ng", "2026-11-21", "20:00", "2", "1"},
                {"tối nay", "2026-11-20", "-", "-", "-"},                                 // asks the hour
                {"6 người", "-", "-", "6", "-"},
                {"Bình Thạnh", "-", "-", "-", "2"},
                {"lúc 19:15", "-", "19:15", "-", "-"},
                {"19h", "-", "19:00", "-", "-"},
                {"10 giờ sáng mai 2 người", "2026-11-21", "10:00", "2", "-"},
                {"mai 4 người ghi chú: 2 ghế trẻ em, gần cửa sổ", "2026-11-21", "-", "4", "-"}, // the note keeps its numbers
                {"Tối mai 7h 6 người, sinh nhật", "2026-11-21", "19:00", "6", "-"},
                // 06/11: the part of day said is followed exactly, even outside opening hours (the assistant then says so)
                {"8 giờ sáng mai 2 người", "2026-11-21", "08:00", "2", "-"},
                {"8h sang mai 2 nguoi", "2026-11-21", "08:00", "2", "-"},
                {"sáng mai 9 giờ 3 người", "2026-11-21", "09:00", "3", "-"},
                {"mai 2 giờ chiều 4 người", "2026-11-21", "14:00", "4", "-"},
                {"chieu mai 3h 4 nguoi", "2026-11-21", "15:00", "4", "-"},
                {"trưa mai 12h30 2 người", "2026-11-21", "12:30", "2", "-"},
                {"mai 11 giờ đêm 2 người", "2026-11-21", "23:00", "2", "-"},
                {"mai 12 giờ đêm 2 người", "2026-11-21", "00:00", "2", "-"},
                {"6h sáng mai 2 người", "2026-11-21", "06:00", "2", "-"},
                {"cho tôi đặt bàn 7h tối mai 2 người", "2026-11-21", "19:00", "2", "-"},
                {"cho tôi đặt bàn mai 9h sáng 2 người", "2026-11-21", "09:00", "2", "-"},
                {"cho toi dat ban mai 7h 2 nguoi", "2026-11-21", "19:00", "2", "-"},          // "toi" here is "tôi"
                {"dat cho toi 11h sang mai 2 nguoi", "2026-11-21", "11:00", "2", "-"},
                {"vo chong toi mai 10h sang", "2026-11-21", "10:00", "2", "-"},
                {"xin chào", "-", "-", "-", "-"},
                {"dat ban", "-", "-", "-", "-"},
        });
    }

    private final String sentence;
    private final String date;
    private final String time;
    private final String people;
    private final String branch;

    public BookingIntentParserTest(String sentence, String date, String time, String people, String branch) {
        this.sentence = sentence;
        this.date = date;
        this.time = time;
        this.people = people;
        this.branch = branch;
    }

    @Test
    public void readsTheSentence() {
        BookingIntent intent = BookingIntentParser.parse(sentence, TODAY, BRANCHES);
        assertEquals("date", "-".equals(date) ? null : LocalDate.parse(date), intent.date);
        assertEquals("time", "-".equals(time) ? null : LocalTime.parse(time), intent.time);
        assertEquals("people", "-".equals(people) ? null : Integer.valueOf(people), intent.partySize);
        assertEquals("branch", "-".equals(branch) ? null : branch, intent.branchId);
    }

    public static class Notes {
        @Test
        public void noteAfterTheWordGhiChu() {
            assertEquals("2 ghế trẻ em, gần cửa sổ",
                    BookingIntentParser.parse("mai 4 người ghi chú: 2 ghế trẻ em, gần cửa sổ", TODAY, BRANCHES).note);
        }

        @Test
        public void occasionsBecomeANote() {
            assertEquals("Sinh nhật", BookingIntentParser.parse("Tối mai 7h 6 người, sinh nhật", TODAY, BRANCHES).note);
            assertNull(BookingIntentParser.parse("mai 7h 2 người", TODAY, BRANCHES).note);
        }

        @Test
        public void partOfDayWithoutHourIsKept() {
            assertEquals("toi", BookingIntentParser.parse("tối nay", TODAY, BRANCHES).period);
        }
    }
}
