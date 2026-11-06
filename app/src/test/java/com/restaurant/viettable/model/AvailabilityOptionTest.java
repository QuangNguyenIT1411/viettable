package com.restaurant.viettable.model;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotSame;

import org.junit.Test;

public class AvailabilityOptionTest {
    @Test
    public void timeAndBranchLabelsUseTheServerNumbers() {
        assertEquals("19:30 · còn 3 bàn", new AvailabilityOption(null, null, "2026-11-20", "19:30", 3, null).label());
        assertEquals("Chi nhánh Gò Vấp · 2,4 km · còn 2 bàn",
                new AvailabilityOption("3", "Chi nhánh Gò Vấp", "2026-11-20", "19:00", 2, 2.4).label());
        assertEquals("Chi nhánh Bình Thạnh · còn 1 bàn",
                new AvailabilityOption("2", "Chi nhánh Bình Thạnh", "2026-11-20", "19:00", 1, null).label());
    }

    @Test
    public void draftCopyIsIndependent() {
        ReservationDraft draft = new ReservationDraft();
        draft.setDate("2026-11-20");
        draft.setTimeSlot("19:00");
        draft.setGuestCount(6);
        ReservationDraft copy = draft.copy();
        copy.setTimeSlot("19:30");
        assertNotSame(draft, copy);
        assertEquals("19:00", draft.getTimeSlot());
        assertEquals("19:30", copy.getTimeSlot());
        assertEquals(6, copy.getGuestCount());
    }
}
