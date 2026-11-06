package com.restaurant.viettable.model;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/** Pushed-together tables the server proposes when no single table seats the party (owner 06/11): "Bàn 5 + 6". */
public final class TableCombination implements Serializable {
    public final List<String> tableIds;
    public final String label;
    public final int seats;

    public TableCombination(List<String> tableIds, String label, int seats) {
        this.tableIds = tableIds == null ? Collections.emptyList() : new ArrayList<>(tableIds);
        this.label = label;
        this.seats = seats;
    }

    public boolean contains(String tableId) { return tableIds.contains(tableId); }

    /** "Ghép Bàn 5 + 6 · 10 chỗ". */
    public String buttonText() { return "Ghép " + label + " · " + seats + " chỗ"; }
}
