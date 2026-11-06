package com.restaurant.viettable.assistant;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.util.Arrays;
import java.util.List;

public class BookingAssistantTest {
    private static final LocalDateTime NOW = LocalDateTime.of(2026, 11, 20, 15, 0); // Friday afternoon
    private static final List<BookingIntentParser.BranchRef> BRANCHES = Arrays.asList(
            new BookingIntentParser.BranchRef("1", "Chi nhánh Quận 1"),
            new BookingIntentParser.BranchRef("2", "Chi nhánh Bình Thạnh"));

    @Test
    public void asksOneMissingThingAtATimeThenChecks() {
        BookingAssistant a = new BookingAssistant(BRANCHES);
        BookingAssistant.Turn t = a.reply("tối mai 4 người", NOW);
        assertEquals("Bạn muốn đặt ở chi nhánh nào ạ?", t.message);
        assertEquals(Arrays.asList("Chi nhánh Quận 1", "Chi nhánh Bình Thạnh"), t.quickReplies);
        t = a.reply("Quận 1", NOW);
        assertEquals("Mấy giờ ạ?", t.message);                 // "tối" was said, the hour was not
        t = a.reply("7 giờ", NOW);
        assertEquals(BookingAssistant.Action.CHECK_AVAILABILITY, t.action);
        assertEquals(LocalTime.of(19, 0), a.intent().time);
        assertEquals(LocalDate.of(2026, 11, 21), a.intent().date);
        assertEquals(Integer.valueOf(4), a.intent().partySize);
    }

    @Test
    public void bookingNeedsAnExplicitYes() {
        BookingAssistant a = new BookingAssistant(BRANCHES);
        a.reply("tối mai 7h 4 người quận 1", NOW);
        BookingAssistant.Turn t = a.availableNow();
        assertTrue(t.message.startsWith("Còn bàn! Chi nhánh Quận 1 · Thứ Bảy 21/11 · 19:00 · 4 người"));
        assertEquals(BookingAssistant.Action.CREATE, a.reply("Đặt bàn", NOW).action);
    }

    @Test
    public void noChangesInsteadOfBooking() {
        BookingAssistant a = new BookingAssistant(BRANCHES);
        a.reply("tối mai 7h 4 người quận 1", NOW);
        a.availableNow();
        BookingAssistant.Turn t = a.reply("không, đổi giờ", NOW);
        assertEquals(BookingAssistant.Action.NONE, t.action);
        // The guest corrects the hour: checked again, never booked without a new yes.
        assertEquals(BookingAssistant.Action.CHECK_AVAILABILITY, a.reply("8 giờ tối", NOW).action);
        assertEquals(LocalTime.of(20, 0), a.intent().time);
    }

    private static BookingAssistant withHours() {
        BookingAssistant a = new BookingAssistant(BRANCHES);
        a.setHours(LocalTime.of(10, 0), LocalTime.of(21, 30), 30); // as the server answers GET /reservations/hours
        return a;
    }

    @Test
    public void pastDaysAndHoursOutsideOpeningAreRefused() {
        BookingAssistant a = withHours();
        assertTrue(a.reply("15/11 19h 2 người quận 1", NOW).message.contains("2027") == false);
        BookingAssistant b = withHours();
        BookingAssistant.Turn t = b.reply("mai 23h 2 người quận 1", NOW);
        assertTrue(t.message.contains("10:00 đến 21:30"));
        BookingAssistant c = withHours();
        t = c.reply("hôm nay 3 giờ chiều 2 người quận 1", NOW);
        assertTrue(t.message.contains("quá sát"));
    }

    @Test
    public void unknownSentenceIsSaidSoAndTheQuestionRepeated() {
        BookingAssistant a = new BookingAssistant(BRANCHES);
        BookingAssistant.Turn t = a.reply("xin chào", NOW);
        assertTrue(t.message.startsWith("Mình chưa hiểu câu này."));
    }

    @Test
    public void aSuggestionKeepsTheRestAndChecksAgain() {
        BookingAssistant a = new BookingAssistant(BRANCHES);
        a.reply("tối mai 7h 4 người quận 1", NOW);
        BookingAssistant.Turn t = a.choose("2", null, null);
        assertEquals(BookingAssistant.Action.CHECK_AVAILABILITY, t.action);
        assertEquals("2", a.intent().branchId);
        assertEquals(LocalTime.of(19, 0), a.intent().time);
    }

    @Test
    public void morningBeforeOpeningIsSaidAndTheNearestTimeSuggestedNotSwitchedToEvening() {
        BookingAssistant a = withHours();
        BookingAssistant.Turn t = a.reply("mai 8 giờ sáng 2 người quận 1", NOW);
        assertEquals(BookingAssistant.Action.NONE, t.action);
        assertTrue(t.message, t.message.contains("từ 10:00 đến 21:30"));
        assertTrue(t.message, t.message.contains("lúc 08:00"));
        assertTrue(t.message, t.message.contains("Giờ gần nhất là 10:00"));
        assertEquals(Arrays.asList("10:00"), t.quickReplies);
        assertEquals(null, a.intent().time);                     // never 20:00
        t = a.reply("10:00", NOW);
        assertEquals(BookingAssistant.Action.CHECK_AVAILABILITY, t.action);
        assertEquals(LocalTime.of(10, 0), a.intent().time);
    }

    @Test
    public void lateNightSuggestsTheLastSlot() {
        BookingAssistant a = withHours();
        BookingAssistant.Turn t = a.reply("mai 11 giờ đêm 2 người quận 1", NOW);
        assertTrue(t.message, t.message.contains("lúc 23:00"));
        assertEquals(Arrays.asList("21:30"), t.quickReplies);
        assertEquals(BookingAssistant.Action.CHECK_AVAILABILITY, a.reply("21:30", NOW).action);
        assertEquals(LocalTime.of(21, 30), a.intent().time);
    }

    @Test
    public void anHourSaidLaterKeepsThePartOfDaySaidBefore() {
        BookingAssistant a = withHours();
        BookingAssistant.Turn t = a.reply("sáng mai 4 người quận 1", NOW);
        assertEquals("Mấy giờ ạ?", t.message);
        assertEquals(Arrays.asList("10:00", "10:30", "11:00", "11:30"), t.quickReplies); // only morning, only open hours
        t = a.reply("8 giờ", NOW);                                // still morning: 08:00, not 20:00
        assertTrue(t.message, t.message.contains("lúc 08:00"));
        BookingAssistant b = withHours();
        b.reply("chiều mai 4 người quận 1", NOW);
        assertEquals(BookingAssistant.Action.CHECK_AVAILABILITY, b.reply("3 giờ", NOW).action);
        assertEquals(LocalTime.of(15, 0), b.intent().time);
    }

    @Test
    public void withoutHoursFromTheServerTheServerDecides() {
        BookingAssistant a = new BookingAssistant(BRANCHES);       // hours not loaded yet
        assertEquals(BookingAssistant.Action.CHECK_AVAILABILITY, a.reply("mai 23h 2 người quận 1", NOW).action);
    }

    @Test
    public void nearestSlot() {
        LocalTime open = LocalTime.of(10, 0), last = LocalTime.of(21, 30);
        assertEquals(open, BookingAssistant.nearestSlot(LocalTime.of(6, 0), open, last, 30));
        assertEquals(last, BookingAssistant.nearestSlot(LocalTime.of(0, 0).minusMinutes(1), open, last, 30));
        assertEquals(LocalTime.of(19, 0), BookingAssistant.nearestSlot(LocalTime.of(19, 10), open, last, 30));
    }
}
