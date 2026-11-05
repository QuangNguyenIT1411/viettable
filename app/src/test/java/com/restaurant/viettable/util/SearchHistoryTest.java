package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

public class SearchHistoryTest {
    @Test
    public void startsEmptyAndNeverInventsSearches() {
        assertTrue(SearchHistory.decode(null).isEmpty());
        assertTrue(SearchHistory.decode("").isEmpty());
    }

    @Test
    public void newestFirstWithoutRepeats() {
        List<String> list = SearchHistory.add(new ArrayList<>(), "Phở");
        list = SearchHistory.add(list, "Bún chả");
        list = SearchHistory.add(list, " phở ");
        assertEquals(Arrays.asList("phở", "Bún chả"), list);
    }

    @Test
    public void keepsAtMostEight() {
        List<String> list = new ArrayList<>();
        for (int i = 0; i < 12; i++) list = SearchHistory.add(list, "món " + i);
        assertEquals(SearchHistory.MAX, list.size());
        assertEquals("món 11", list.get(0));
    }

    @Test
    public void findsDishesWithoutToneMarks() {
        assertTrue(SearchHistory.matches("Bún Bò Huế", "bun"));
        assertTrue(SearchHistory.matches("Đậu hũ chiên", "dau hu"));
        assertTrue(SearchHistory.matches("Phở bò", "PHỞ"));
        assertTrue(!SearchHistory.matches("Bánh mì", "bun"));
    }

    @Test
    public void survivesStorage() {
        List<String> list = Arrays.asList("Phở bò", "Bún chả");
        assertEquals(list, SearchHistory.decode(SearchHistory.encode(list)));
    }
}
