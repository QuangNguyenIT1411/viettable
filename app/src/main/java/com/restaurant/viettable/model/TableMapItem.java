package com.restaurant.viettable.model;

import java.io.Serializable;

/** One table on the booking map, as the server reports it for the chosen time and party size. */
public class TableMapItem implements Serializable {
    public static final String AVAILABLE = "AVAILABLE";
    public static final String BOOKED = "BOOKED";
    public static final String TOO_SMALL = "TOO_SMALL";
    public static final String IN_USE = "IN_USE";
    public static final String INACTIVE = "INACTIVE";

    public final String tableId;
    public final int tableNumber;
    public final int seats;
    public final String status;
    public final String label;

    public TableMapItem(String tableId, int tableNumber, int seats, String status, String label) {
        this.tableId = tableId;
        this.tableNumber = tableNumber;
        this.seats = seats;
        this.status = status == null ? INACTIVE : status;
        this.label = label;
    }

    public boolean selectable() { return AVAILABLE.equals(status); }
}
