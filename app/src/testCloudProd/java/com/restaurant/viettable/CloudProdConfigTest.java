package com.restaurant.viettable;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;

public class CloudProdConfigTest {
    @Test
    public void cloudProdUsesNewProductionOrigin() {
        assertEquals("https://viettable-beta.vercel.app", BuildConfig.API_BASE_URL);
        assertFalse(BuildConfig.API_BASE_URL.contains("hienkhanhvu981"));
    }

    @Test
    public void cloudProdKeepsColdStartTimeouts() {
        assertEquals(60000, BuildConfig.API_READ_TIMEOUT_MS);
        assertEquals(60000, BuildConfig.IMAGE_READ_TIMEOUT_MS);
    }
}
