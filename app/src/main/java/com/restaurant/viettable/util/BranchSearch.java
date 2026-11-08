package com.restaurant.viettable.util;

import androidx.annotation.Nullable;

/**
 * Branch search (owner 09/11): by name or address, ignoring tone marks and case like the dish search
 * ("Quan 1" finds "Chi nhánh Quận 1", "dien bien" finds "Điện Biên Phủ"). An empty query shows every branch.
 */
public final class BranchSearch {
    private BranchSearch() { }

    public static boolean matches(@Nullable String name, @Nullable String address, @Nullable String query) {
        if (query == null || query.trim().isEmpty()) return true;
        return SearchHistory.matches(name, query) || SearchHistory.matches(address, query);
    }
}
