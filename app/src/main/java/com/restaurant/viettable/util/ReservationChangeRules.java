package com.restaurant.viettable.util;

import androidx.annotation.Nullable;

/**
 * The note on "Thay đổi đặt bàn". The server replaces the stored note with whatever the change request carries (a
 * missing one becomes empty), so the form starts from the saved note and always sends what the box holds: untouched
 * means the old note, and only a guest who empties the box on purpose sends "".
 */
public final class ReservationChangeRules {
    private ReservationChangeRules() { }

    /** What the note box shows when the form opens. */
    public static String prefilledNote(@Nullable String savedNote) {
        return SafeText.clean(savedNote);
    }

    /** What the change request carries for the note. */
    public static String noteToSend(@Nullable CharSequence noteBox) {
        return noteBox == null ? "" : noteBox.toString().trim();
    }
}
