package com.restaurant.viettable.model;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/** The server's answer for "Hết bàn?": room at the asked time, nearest other times, other branches. */
public final class AvailabilityAlternatives {
    public final boolean available;
    public final List<AvailabilityOption> times;
    public final List<AvailabilityOption> branches;

    public AvailabilityAlternatives(boolean available, List<AvailabilityOption> times, List<AvailabilityOption> branches) {
        this.available = available;
        this.times = times == null ? Collections.emptyList() : new ArrayList<>(times);
        this.branches = branches == null ? Collections.emptyList() : new ArrayList<>(branches);
    }

    public boolean isEmpty() { return times.isEmpty() && branches.isEmpty(); }
}
