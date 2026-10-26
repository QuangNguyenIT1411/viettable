package com.restaurant.viettable.util;

import com.restaurant.viettable.model.TableMapItem;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * The booking map (reservation item 5, 26/10): tables grouped by seats, sorted by number, and why a table cannot be
 * chosen. The server decides each table's state; this only arranges and words it.
 */
public final class TableMapRules {
    private TableMapRules() { }

    public static final String GROUP_2 = "Bàn 2 người";
    public static final String GROUP_4 = "Bàn 4 người";
    public static final String GROUP_6 = "Bàn 6+ người";

    public static String group(int seats) {
        if (seats <= 2) return GROUP_2;
        if (seats <= 4) return GROUP_4;
        return GROUP_6;
    }

    /** Groups in the order 2 → 4 → 6+, each sorted by table number; empty groups are left out. */
    public static Map<String, List<TableMapItem>> grouped(List<TableMapItem> tables) {
        Map<String, List<TableMapItem>> result = new LinkedHashMap<>();
        for (String name : new String[] { GROUP_2, GROUP_4, GROUP_6 }) result.put(name, new ArrayList<>());
        for (TableMapItem t : tables) result.get(group(t.seats)).add(t);
        for (List<TableMapItem> list : result.values())
            Collections.sort(list, (a, b) -> a.tableNumber != b.tableNumber
                    ? Integer.compare(a.tableNumber, b.tableNumber) : a.tableId.compareTo(b.tableId));
        result.values().removeIf(List::isEmpty);
        return result;
    }

    /** Short reason shown when a guest taps a table that cannot be chosen; null when it can. */
    public static String reason(TableMapItem t, int partySize) {
        String name = "Bàn " + t.tableNumber;
        switch (t.status) {
            case TableMapItem.AVAILABLE: return null;
            case TableMapItem.BOOKED: return name + " đã được đặt trong khung giờ này. Vui lòng chọn bàn khác.";
            case TableMapItem.TOO_SMALL: return name + " chỉ có " + t.seats + " ghế, không đủ cho " + partySize + " khách.";
            case TableMapItem.IN_USE: return name + " đang có khách, chưa kịp trống vào giờ bạn chọn.";
            default: return name + " đang tạm ngưng phục vụ.";
        }
    }

    /** The selection survives a reload only while that table is still free (someone else may have taken it). */
    public static String keepSelection(String selectedTableId, List<TableMapItem> tables) {
        if (selectedTableId == null) return null;
        for (TableMapItem t : tables) if (t.tableId.equals(selectedTableId)) return t.selectable() ? selectedTableId : null;
        return null;
    }

    public static int availableCount(List<TableMapItem> tables) {
        int count = 0;
        for (TableMapItem t : tables) if (t.selectable()) count++;
        return count;
    }
}
