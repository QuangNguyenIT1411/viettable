package com.restaurant.viettable.util;

import static org.junit.Assert.assertArrayEquals;
import static org.junit.Assert.assertEquals;

import org.junit.Test;

/** Owner 07/11: menu photos are shrunk to at most 720 px on the long side before they are kept. */
public class ImageLoaderSizeTest {
    @Test
    public void sampleSizeHalvesWhileStillAtLeastTheTarget() {
        assertEquals(1, ImageLoader.sampleSize(700, 500, 720));
        assertEquals(1, ImageLoader.sampleSize(1200, 800, 720));
        assertEquals(2, ImageLoader.sampleSize(1600, 1200, 720));
        assertEquals(4, ImageLoader.sampleSize(3000, 2000, 720));
        assertEquals(1, ImageLoader.sampleSize(0, 0, 720));
    }

    @Test
    public void scaledSizeKeepsTheShape() {
        assertArrayEquals(new int[]{720, 480}, ImageLoader.scaledSize(1200, 800, 720));
        assertArrayEquals(new int[]{405, 720}, ImageLoader.scaledSize(900, 1600, 720));
        assertArrayEquals(new int[]{640, 480}, ImageLoader.scaledSize(640, 480, 720));
    }
}
