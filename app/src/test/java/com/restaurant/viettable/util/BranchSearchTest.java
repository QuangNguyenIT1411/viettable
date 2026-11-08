package com.restaurant.viettable.util;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

/** Owner 09/11: "Quan 1" found no branch; guests type without tone marks, like the dish search. */
public class BranchSearchTest {
    @Test
    public void findsBranchesWithoutToneMarks() {
        assertTrue(BranchSearch.matches("Chi nhánh Quận 1", "123 Nguyễn Huệ, Quận 1, TP.HCM", "Quan 1"));
        assertTrue(BranchSearch.matches("Chi nhánh Bình Thạnh", "789 Điện Biên Phủ", "binh thanh"));
        assertTrue(BranchSearch.matches("Chi nhánh Gò Vấp", "456 Quang Trung", "dien bien") == false);
        assertTrue(BranchSearch.matches("Chi nhánh Bình Thạnh", "789 Điện Biên Phủ", "dien bien")); // by address, đ = d
        assertTrue(BranchSearch.matches("Chi nhánh Quận 1", null, "QUẬN 1"));
        assertTrue(BranchSearch.matches("Chi nhánh Quận 1", null, "  "));      // empty query shows all
        assertFalse(BranchSearch.matches("Chi nhánh Quận 1", "Nguyễn Huệ", "go vap"));
    }
}
