package com.restaurant.viettable.model;

import java.io.Serializable;

/** One place on the waitlist, as the server keeps it (owner 06/11). Times are server instants (UTC, ISO). */
public final class WaitlistEntry implements Serializable {
    public String id;
    public String branchId;
    public String branchName;
    public String windowStartUtc;
    public String windowEndUtc;
    public int partySize;
    public String note;
    public String status;            // Waiting, Offered, Accepted, Expired, Cancelled, Closed
    public int position;             // 1-based while Waiting, else 0
    public String offeredReservationId;
    public String offeredTableId;
    public String offeredReservedAtUtc;
    public String offerExpiresAtUtc;
    public String closedReason;

    public boolean isWaiting() { return "Waiting".equals(status); }
    public boolean isOffered() { return "Offered".equals(status); }
    public boolean isActive() { return isWaiting() || isOffered(); }
}
