package com.restaurant.viettable.data;

/** Public catalog only: never use these lifetimes for authenticated business state. */
public final class CatalogCachePolicy {
    public static final long FRESH_MS = 5 * 60 * 1000L;
    public static final long MAX_STALE_MS = 24 * 60 * 60 * 1000L;
    private CatalogCachePolicy() {}
    public static boolean usable(long saved, long now) {
        return saved > 0 && now >= saved && now - saved <= MAX_STALE_MS;
    }
    public static boolean fresh(long saved, long now) {
        return usable(saved, now) && now - saved <= FRESH_MS;
    }
}
