package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

import com.restaurant.viettable.model.TableMapItem;

import org.junit.Test;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Map;

/** Reservation item 5 (26/10): grouping, order and wording of the booking map. */
public class TableMapRulesTest {
    private static TableMapItem t(int number, int seats, String status) {
        return new TableMapItem(String.valueOf(100 + number), number, seats, status, "");
    }

    @Test public void tables_are_grouped_by_seats_and_sorted_by_number() {
        List<TableMapItem> tables = Arrays.asList(t(12, 4, "AVAILABLE"), t(3, 2, "BOOKED"), t(1, 2, "AVAILABLE"),
                t(9, 8, "INACTIVE"), t(4, 3, "AVAILABLE"), t(7, 6, "AVAILABLE"));
        Map<String, List<TableMapItem>> groups = TableMapRules.grouped(tables);
        assertEquals(Arrays.asList("Bàn 2 người", "Bàn 4 người", "Bàn 6+ người"), new ArrayList<>(groups.keySet()));
        assertEquals(Arrays.asList(1, 3), numbers(groups.get("Bàn 2 người")));
        assertEquals(Arrays.asList(4, 12), numbers(groups.get("Bàn 4 người")));
        assertEquals(Arrays.asList(7, 9), numbers(groups.get("Bàn 6+ người")));
    }

    @Test public void empty_groups_are_left_out() {
        assertEquals(Arrays.asList("Bàn 4 người"), new ArrayList<>(TableMapRules.grouped(Arrays.asList(t(5, 4, "AVAILABLE"))).keySet()));
    }

    @Test public void tapping_a_table_that_cannot_be_chosen_says_why() {
        assertNull(TableMapRules.reason(t(8, 4, "AVAILABLE"), 2));
        assertEquals("Bàn 8 đã được đặt trong khung giờ này. Vui lòng chọn bàn khác.", TableMapRules.reason(t(8, 4, "BOOKED"), 2));
        assertEquals("Bàn 3 chỉ có 2 ghế, không đủ cho 4 khách.", TableMapRules.reason(t(3, 2, "TOO_SMALL"), 4));
        assertEquals("Bàn 5 đang tạm ngưng phục vụ.", TableMapRules.reason(t(5, 4, "INACTIVE"), 2));
        assertTrue(TableMapRules.reason(t(6, 4, "IN_USE"), 2).contains("đang có khách"));
    }

    @Test public void a_selection_survives_a_reload_only_while_the_table_is_free() {
        List<TableMapItem> before = Arrays.asList(t(8, 4, "AVAILABLE"), t(9, 4, "AVAILABLE"));
        List<TableMapItem> after = Arrays.asList(t(8, 4, "BOOKED"), t(9, 4, "AVAILABLE"));
        assertEquals("108", TableMapRules.keepSelection("108", before));
        assertNull(TableMapRules.keepSelection("108", after));   // someone else took it
        assertNull(TableMapRules.keepSelection("999", before));  // gone from the branch
        assertNull(TableMapRules.keepSelection(null, before));   // nothing is chosen for the guest
        assertEquals(1, TableMapRules.availableCount(after));
    }

    private static List<Integer> numbers(List<TableMapItem> tables) {
        List<Integer> result = new ArrayList<>();
        for (TableMapItem t : tables) result.add(t.tableNumber);
        return result;
    }
}
