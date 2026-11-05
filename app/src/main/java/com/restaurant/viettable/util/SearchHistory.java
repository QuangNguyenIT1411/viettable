package com.restaurant.viettable.util;

import java.util.ArrayList;
import java.util.List;

/** The guest's own recent searches: newest first, no repeats, at most {@link #MAX}. Starts empty, never pre-filled. */
public final class SearchHistory {
    public static final int MAX = 8;
    private static final String SEPARATOR = "\n";

    private SearchHistory() {}

    /** "bun" finds "Bún", "pho bo" finds "Phở bò": guests often type without tone marks. */
    public static boolean matches(String text, String query) {
        return text != null && fold(text).contains(fold(query));
    }

    static String fold(String value) {
        String lower = value == null ? "" : value.toLowerCase(java.util.Locale.ROOT).trim();
        return java.text.Normalizer.normalize(lower, java.text.Normalizer.Form.NFD).replaceAll("\\p{M}", "").replace('đ', 'd');
    }

    public static List<String> add(List<String> current, String query) {
        List<String> result = new ArrayList<>();
        String clean = query == null ? "" : query.trim();
        if (!clean.isEmpty()) result.add(clean);
        for (String item : current) {
            if (result.size() >= MAX) break;
            if (!item.equalsIgnoreCase(clean)) result.add(item);
        }
        return result;
    }

    public static String encode(List<String> items) {
        return String.join(SEPARATOR, items);
    }

    public static List<String> decode(String stored) {
        List<String> result = new ArrayList<>();
        if (stored == null) return result;
        for (String item : stored.split(SEPARATOR)) {
            if (!item.trim().isEmpty() && result.size() < MAX) result.add(item.trim());
        }
        return result;
    }
}
