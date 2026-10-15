package com.restaurant.viettable.util;

import org.json.JSONArray;
import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

public class SafeTextTest {
    @Test public void jsonNullMissingAndTheWordNullAreEmpty() throws Exception {
        JSONObject row = new JSONObject("{\"comment\":null,\"note\":\"null\",\"name\":\"Phở bò\",\"blank\":\"  \",\"count\":5}");
        assertEquals("", SafeText.json(row, "comment"));
        assertEquals("", SafeText.json(row, "note"));
        assertEquals("", SafeText.json(row, "missing"));
        assertEquals("", SafeText.json(row, "blank"));
        assertEquals("Phở bò", SafeText.json(row, "name"));
        assertEquals("5", SafeText.json(row, "count"));
        assertEquals("", SafeText.json(null, "name"));
    }

    @Test public void fallbackIsUsedOnlyWhenThereIsNothingToShow() throws Exception {
        JSONObject row = new JSONObject("{\"comment\":null,\"tier\":\"GOLD\"}");
        assertNull(SafeText.json(row, "comment", null));
        assertEquals("Thành viên", SafeText.json(row, "missing", "Thành viên"));
        assertEquals("GOLD", SafeText.json(row, "tier", "Thành viên"));
    }

    @Test public void jsonArrayItems() throws Exception {
        JSONArray items = new JSONArray("[\"a.png\",null,\"null\"]");
        assertEquals("a.png", SafeText.json(items, 0));
        assertEquals("", SafeText.json(items, 1));
        assertEquals("", SafeText.json(items, 2));
        assertEquals("", SafeText.json(items, 7));
    }

    @Test public void cleanOrAndBlank() {
        assertEquals("", SafeText.clean(null));
        assertEquals("", SafeText.clean(" null "));
        assertEquals("", SafeText.clean("undefined"));
        assertEquals("", SafeText.clean("NaN"));
        assertEquals("Bàn 3", SafeText.clean("  Bàn 3 "));
        assertEquals("Chưa có", SafeText.or("NULL", "Chưa có"));
        assertTrue(SafeText.isBlank("null"));
        assertFalse(SafeText.isBlank("Không cay"));
    }

    @Test public void joinSkipsEmptyParts() {
        assertEquals("Bàn 3 • Quận 1", SafeText.join(" • ", "Bàn 3", null, "null", " ", "Quận 1"));
        assertEquals("", SafeText.join(" • ", (CharSequence) null));
    }
}
