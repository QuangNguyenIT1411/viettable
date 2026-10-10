package com.restaurant.viettable.data;

import org.junit.Test;
import static org.junit.Assert.*;

public class CatalogCachePolicyTest {
    @Test public void recentCatalogIsFresh() {
        assertTrue(CatalogCachePolicy.fresh(1000, 1001));
        assertTrue(CatalogCachePolicy.fresh(1000, 1000 + CatalogCachePolicy.FRESH_MS));
    }
    @Test public void staleCatalogCanRenderWhileRevalidatingButExpires() {
        long now = 1000 + CatalogCachePolicy.FRESH_MS + 1;
        assertFalse(CatalogCachePolicy.fresh(1000, now));
        assertTrue(CatalogCachePolicy.usable(1000, now));
        assertFalse(CatalogCachePolicy.usable(1000, 1000 + CatalogCachePolicy.MAX_STALE_MS + 1));
    }
    @Test public void clockRollbackAndMissingTimestampAreMisses() {
        assertFalse(CatalogCachePolicy.usable(0, 1000));
        assertFalse(CatalogCachePolicy.fresh(2000, 1000));
    }
}
