package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

public class ReservationChangeRulesTest {

    @Test
    public void changingOnlyTheTimeKeepsTheSavedNote() {
        String box = ReservationChangeRules.prefilledNote("Gần cửa sổ, có trẻ nhỏ");
        assertEquals("Gần cửa sổ, có trẻ nhỏ", ReservationChangeRules.noteToSend(box));
    }

    @Test
    public void emptyingTheBoxOnPurposeSendsAnEmptyNote() {
        ReservationChangeRules.prefilledNote("Gần cửa sổ");
        assertEquals("", ReservationChangeRules.noteToSend(""));
        assertEquals("", ReservationChangeRules.noteToSend("   "));
    }

    @Test
    public void anEditedNoteIsSentTrimmed() {
        assertEquals("Thêm ghế em bé", ReservationChangeRules.noteToSend("  Thêm ghế em bé "));
    }

    @Test
    public void aReservationWithoutANoteOpensEmptyAndStaysEmpty() {
        assertEquals("", ReservationChangeRules.prefilledNote(null));
        assertEquals("", ReservationChangeRules.prefilledNote("null"));
        assertEquals("", ReservationChangeRules.noteToSend(null));
    }
}
