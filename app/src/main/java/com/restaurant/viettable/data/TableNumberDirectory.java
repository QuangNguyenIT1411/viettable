package com.restaurant.viettable.data;

import java.util.HashMap;
import java.util.Map;

/** Catalog display metadata only; never translates IDs used in commands. */
public final class TableNumberDirectory {
    private final Map<String, Map<String, Integer>> branches = new HashMap<>();

    public void put(String branchId, String tableId, int number) {
        if (branchId == null || branchId.isEmpty() || tableId == null || tableId.isEmpty() || number <= 0) return;
        branches.computeIfAbsent(branchId, ignored -> new HashMap<>()).put(tableId, number);
    }

    public String label(String branchId, String tableId) {
        if (tableId == null || tableId.isEmpty() || "0".equals(tableId)) return "Chưa xếp bàn";
        Map<String, Integer> tables = branches.get(branchId);
        Integer number = tables == null ? null : tables.get(tableId);
        return number == null ? "Chưa có thông tin bàn" : "Bàn " + number;
    }

    public void clear() { branches.clear(); }
}
