package com.restaurant.viettable.assistant;

import java.time.LocalDate;
import java.time.LocalTime;

/** What the guest said so far. Null = not said (the assistant asks). */
public final class BookingIntent {
    public LocalDate date;
    public LocalTime time;
    public String period;      // toi, chieu, trua, sang when said without an hour
    public Integer partySize;
    public String branchId;
    public String note;

    /** Fills what the other sentence added; a new value replaces an old one (the guest corrects themselves). */
    public void merge(BookingIntent other) {
        if (other == null) return;
        if (other.date != null) date = other.date;
        if (other.time != null) time = other.time;
        if (other.period != null) period = other.period;
        if (other.partySize != null) partySize = other.partySize;
        if (other.branchId != null) branchId = other.branchId;
        if (other.note != null) note = other.note;
    }

    public boolean complete() { return branchId != null && date != null && time != null && partySize != null; }
}
