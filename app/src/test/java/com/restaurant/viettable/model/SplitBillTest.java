package com.restaurant.viettable.model;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import java.util.Arrays;
import org.junit.Test;

public class SplitBillTest {
    @Test
    public void allPartsPaid_requiresEveryCanonicalPartToBePaid() {
        SplitBill.Part paid = new SplitBill.Part(1, "Phần 1", 50_000, "PAID", 10, "SUCCEEDED");
        SplitBill.Part unpaid = new SplitBill.Part(2, "Phần 2", 50_000, "UNPAID", null, null);

        assertFalse(new SplitBill(1, 1, 1, 100_000, "OPEN", Arrays.asList(paid, unpaid)).areAllPartsPaid());
        assertTrue(new SplitBill(1, 1, 1, 50_000, "PAID", Arrays.asList(paid)).areAllPartsPaid());
    }

    @Test
    public void failedPayment_isRetryableButPendingAndPaidAreNot() {
        assertTrue(new SplitBill.Part(1, "Phần 1", 50_000, "PENDING", 10, "FAILED").canRetryPayment());
        assertFalse(new SplitBill.Part(1, "Phần 1", 50_000, "PENDING", 10, "PENDING").canRetryPayment());
        assertFalse(new SplitBill.Part(1, "Phần 1", 50_000, "PAID", 10, "SUCCEEDED").canRetryPayment());
    }
}
