package com.restaurant.viettable.data;

public final class SplitBillAmounts {
    private SplitBillAmounts() {}

    /** VND remainder is assigned one đồng at a time to the first parts. */
    public static long[] equalParts(long total, int count) {
        if (total <= 0 || count < 2 || count > total) throw new IllegalArgumentException("Invalid split");
        long base = total / count;
        long remainder = total % count;
        long[] parts = new long[count];
        for (int i = 0; i < count; i++) parts[i] = base + (i < remainder ? 1 : 0);
        return parts;
    }

    public static long sum(long[] amounts) {
        long total = 0;
        for (long amount : amounts) total = Math.addExact(total, amount);
        return total;
    }
}
