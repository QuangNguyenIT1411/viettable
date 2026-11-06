package com.restaurant.viettable.data;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;

import com.restaurant.viettable.model.TableCombination;

import org.junit.Test;

import java.util.Arrays;
import java.util.Collections;

public class TableGroupLabelTest {
    @Test
    public void groupedTablesReadAsOneLabel() {
        assertEquals("Bàn 5 + 6", TableNumberDirectory.groupLabel(Arrays.asList("Bàn 5", "Bàn 6")));
        assertEquals("Bàn 2 + 3 + 4", TableNumberDirectory.groupLabel(Arrays.asList("Bàn 2", "Bàn 3", "Bàn 4")));
    }

    @Test
    public void unknownNumbersOrASingleTableGiveNoGroupLabel() {
        assertNull(TableNumberDirectory.groupLabel(Collections.singletonList("Bàn 5")));
        assertNull(TableNumberDirectory.groupLabel(Arrays.asList("Bàn 5", "Chưa có thông tin bàn")));
    }

    @Test
    public void combinationButton() {
        TableCombination combo = new TableCombination(Arrays.asList("11", "12"), "Bàn 5 + 6", 10);
        assertEquals("Ghép Bàn 5 + 6 · 10 chỗ", combo.buttonText());
        assertEquals(true, combo.contains("12"));
    }
}
