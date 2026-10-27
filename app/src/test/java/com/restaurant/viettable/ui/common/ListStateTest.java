package com.restaurant.viettable.ui.common;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

/** "Thử lại" must replace the list, never sit on top of rows still shown (owner request 27/10). */
public class ListStateTest {
    @Test public void an_error_replaces_the_rows() {
        assertEquals(ListState.Mode.ERROR, ListState.of(true, false, "Không có kết nối mạng."));
        assertEquals(ListState.Mode.ERROR, ListState.of(false, false, "Không có kết nối mạng."));
    }

    @Test public void a_reload_keeps_the_rows_and_a_first_load_shows_the_spinner() {
        assertEquals(ListState.Mode.CONTENT, ListState.of(true, true, null));
        assertEquals(ListState.Mode.LOADING, ListState.of(false, true, null));
        assertEquals(ListState.Mode.LOADING, ListState.of(false, true, "old error"));
    }

    @Test public void loaded_lists_show_rows_or_the_empty_message() {
        assertEquals(ListState.Mode.CONTENT, ListState.of(true, false, null));
        assertEquals(ListState.Mode.EMPTY, ListState.of(false, false, null));
    }
}
