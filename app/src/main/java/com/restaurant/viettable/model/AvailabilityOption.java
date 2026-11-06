package com.restaurant.viettable.model;

import java.io.Serializable;
import java.util.Locale;

/**
 * One "Hết bàn" alternative from the server (owner 06/11): another time at the same branch, or another branch at the
 * same time. Counts and distances are the server's; the app adds nothing.
 */
public final class AvailabilityOption implements Serializable {
    public final String branchId;      // null for a time at the chosen branch
    public final String branchName;
    public final String localDate;     // yyyy-MM-dd, restaurant time
    public final String localTime;     // HH:mm
    public final int availableTables;
    public final Double distanceKm;    // null when unknown

    public AvailabilityOption(String branchId, String branchName, String localDate, String localTime, int availableTables, Double distanceKm) {
        this.branchId = branchId;
        this.branchName = branchName;
        this.localDate = localDate;
        this.localTime = localTime;
        this.availableTables = availableTables;
        this.distanceKm = distanceKm;
    }

    public boolean isOtherBranch() { return branchId != null; }

    /** "19:30 · còn 3 bàn" or "Chi nhánh Gò Vấp · 2,4 km · còn 2 bàn". */
    public String label() {
        String tables = "còn " + availableTables + " bàn";
        if (!isOtherBranch()) return localTime + " · " + tables;
        StringBuilder text = new StringBuilder(branchName == null ? "Chi nhánh" : branchName);
        if (distanceKm != null) text.append(" · ").append(String.format(Locale.ROOT, "%.1f", distanceKm).replace('.', ',')).append(" km");
        return text.append(" · ").append(tables).toString();
    }
}
