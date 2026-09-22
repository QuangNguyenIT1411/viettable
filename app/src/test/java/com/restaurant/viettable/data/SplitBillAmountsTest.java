package com.restaurant.viettable.data;

import org.junit.Test;
import static org.junit.Assert.assertArrayEquals;
import static org.junit.Assert.assertEquals;

public class SplitBillAmountsTest {
    @Test public void equalPartsPreserveEveryDong() {
        long[] parts = SplitBillAmounts.equalParts(100_000, 3);
        assertArrayEquals(new long[] { 33_334, 33_333, 33_333 }, parts);
        assertEquals(100_000, SplitBillAmounts.sum(parts));
    }

    @Test(expected = IllegalArgumentException.class)
    public void rejectsNonPositiveParts() { SplitBillAmounts.equalParts(1, 2); }
}
