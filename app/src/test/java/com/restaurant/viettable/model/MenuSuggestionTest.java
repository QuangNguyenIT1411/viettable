package com.restaurant.viettable.model;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

import org.json.JSONObject;
import org.junit.Test;

public class MenuSuggestionTest {
    private static final String RESPONSE = "{\"feasible\":true,\"items\":["
            + "{\"dishId\":5,\"name\":\"Đậu hũ sốt cà\",\"price\":50000,\"quantity\":1,\"course\":\"MAIN\",\"reason\":\"Món chay · đủ cho 1 người\",\"forVegetarians\":true,"
            + "\"alternatives\":[{\"dishId\":6,\"name\":\"Rau xào chay\",\"price\":45000,\"isVegetarian\":true}]},"
            + "{\"dishId\":10,\"name\":\"Trà đá\",\"price\":5000,\"quantity\":4,\"course\":\"DRINK\",\"reason\":\"Đồ uống cho 4 người\",\"forVegetarians\":false,\"alternatives\":[]}],"
            + "\"subtotal\":70000,\"serviceFeePercent\":5,\"serviceFee\":3500,\"vatPercent\":8,\"vat\":5880,\"total\":79380,"
            + "\"budget\":800000,\"minimumTotal\":null,\"notes\":[\"1 món chưa được gắn loại món nên chưa được gợi ý.\"]}";

    @Test
    public void parses_lines_totals_and_notes_from_the_server() throws Exception {
        MenuSuggestion s = MenuSuggestion.parse(new JSONObject(RESPONSE));
        assertTrue(s.feasible);
        assertEquals(2, s.lines.size());
        assertEquals("Đậu hũ sốt cà", s.lines.get(0).name);
        assertTrue(s.lines.get(0).forVegetarians);
        assertEquals(1, s.lines.get(0).alternatives.size());
        assertEquals(20000, s.lines.get(1).lineTotal());
        assertEquals(79380, s.totals.total);
        assertEquals(3500, s.totals.fee);
        assertNull(s.minimumTotal);
        assertEquals(5, s.dishCount());
        assertEquals(1, s.notes.size());
        assertFalse(s.overBudget());
        assertEquals("Còn dư 720.620đ so với ngân sách", s.budgetLine());
    }

    @Test
    public void swap_keeps_quantity_and_lets_the_guest_swap_back() throws Exception {
        MenuSuggestion s = MenuSuggestion.parse(new JSONObject(RESPONSE));
        MenuSuggestion swapped = s.swap(0, s.lines.get(0).alternatives.get(0));
        assertEquals("Rau xào chay", swapped.lines.get(0).name);
        assertEquals(1, swapped.lines.get(0).quantity);
        assertTrue(swapped.lines.get(0).forVegetarians);
        assertEquals("5", swapped.lines.get(0).alternatives.get(0).dishId);
        assertEquals("Đậu hũ sốt cà", s.lines.get(0).name); // the original is unchanged
        JSONObject body = swapped.estimateBody("1");
        assertEquals(1, body.getInt("branchId"));
        assertEquals(6, body.getJSONArray("items").getJSONObject(0).getInt("dishId"));
        assertEquals(4, body.getJSONArray("items").getJSONObject(1).getInt("quantity"));
    }

    @Test
    public void new_totals_come_from_the_server() throws Exception {
        MenuSuggestion s = MenuSuggestion.parse(new JSONObject(RESPONSE));
        MenuSuggestion priced = s.withTotals(new MenuSuggestion.Totals(900000, 0, 0, 0, 0, 900000));
        assertTrue(priced.overBudget());
        assertEquals("Vượt 100.000đ so với ngân sách", priced.budgetLine());
    }

    @Test
    public void a_short_budget_keeps_the_minimum() throws Exception {
        MenuSuggestion s = MenuSuggestion.parse(new JSONObject("{\"feasible\":false,\"items\":[],\"total\":0,\"budget\":150000,\"minimumTotal\":420000,\"notes\":[\"Ngân sách chưa đủ cho 6 người.\"]}"));
        assertFalse(s.feasible);
        assertEquals(Long.valueOf(420000), s.minimumTotal);
    }

    @Test
    public void reads_the_budget_the_way_people_type_it() {
        assertEquals(800_000, MenuSuggestion.parseBudget("800000"));
        assertEquals(800_000, MenuSuggestion.parseBudget("800.000"));
        assertEquals(800_000, MenuSuggestion.parseBudget("800k"));
        assertEquals(1_500_000, MenuSuggestion.parseBudget("1,5tr"));
        assertEquals(2_000_000, MenuSuggestion.parseBudget("2 triệu"));
        assertEquals(500_000, MenuSuggestion.parseBudget("500.000đ"));
        assertEquals(0, MenuSuggestion.parseBudget("abc"));
        assertEquals(0, MenuSuggestion.parseBudget(""));
    }

    @Test
    public void the_total_says_it_is_before_discounts() {
        assertEquals("Tổng dự kiến, chưa gồm khuyến mãi/voucher/điểm", MenuSuggestion.TOTAL_NOTE);
    }

    @Test
    public void course_labels() {
        assertEquals("Món chính", MenuSuggestion.courseLabel("MAIN"));
        assertEquals("Đồ uống", MenuSuggestion.courseLabel("drink"));
        assertEquals("", MenuSuggestion.courseLabel(null));
    }
}
