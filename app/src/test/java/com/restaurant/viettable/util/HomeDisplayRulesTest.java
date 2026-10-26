package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

import com.restaurant.viettable.model.FoodItem;

import org.junit.Test;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

public class HomeDisplayRulesTest {

    private static FoodItem dish(String id, long price, int sold, boolean available, boolean special) {
        FoodItem food = new FoodItem(id, "1", "1", "Món " + id, price, 0, sold, "", "", "", "", 0,
                available, false, new ArrayList<>(), new ArrayList<>());
        food.setDailySpecial(special);
        return food;
    }

    @Test
    public void greetsByGivenName() {
        assertEquals("Quang", HomeDisplayRules.greetingName("Nguyễn Vinh Quang"));
        assertEquals("Lan", HomeDisplayRules.greetingName("  Lan "));
        assertEquals("Vinh", HomeDisplayRules.greetingName("quang vinh"));
        assertEquals("Đức", HomeDisplayRules.greetingName("đức"));
        assertEquals("", HomeDisplayRules.greetingName(null));
        assertEquals("", HomeDisplayRules.greetingName("   "));
    }

    @Test
    public void shortPriceLikeTheStickers() {
        assertEquals("65K", HomeDisplayRules.shortPrice(65_000));
        assertEquals("149K", HomeDisplayRules.shortPrice(149_000));
        assertEquals("135,5K", HomeDisplayRules.shortPrice(135_500));
        assertEquals("1200K", HomeDisplayRules.shortPrice(1_200_000));
        assertEquals("900đ", HomeDisplayRules.shortPrice(900));
    }

    @Test
    public void chefPickIsTodaysSpecialOnSale() {
        FoodItem soldOutSpecial = dish("1", 50_000, 0, false, true);
        FoodItem plain = dish("2", 40_000, 0, true, false);
        FoodItem special = dish("3", 149_000, 0, true, true);
        assertEquals(special, HomeDisplayRules.chefPick(Arrays.asList(soldOutSpecial, plain, special)));
        assertNull(HomeDisplayRules.chefPick(Arrays.asList(soldOutSpecial, plain)));
    }

    @Test
    public void bestSellersNeedRealSalesCounts() {
        List<FoodItem> noSales = Arrays.asList(dish("1", 1, 0, true, false), dish("2", 1, 0, true, false));
        assertTrue(HomeDisplayRules.bestSellers(noSales, 4).isEmpty());

        FoodItem a = dish("a", 1, 5, true, false);
        FoodItem b = dish("b", 1, 20, true, false);
        FoodItem soldOut = dish("c", 1, 99, false, false);
        assertEquals(Arrays.asList(b, a), HomeDisplayRules.bestSellers(Arrays.asList(a, soldOut, b), 4));
        assertEquals(Arrays.asList(b), HomeDisplayRules.bestSellers(Arrays.asList(a, soldOut, b), 1));
    }

    @Test
    public void onSaleFirstKeepsMenuOrder() {
        FoodItem a = dish("a", 1, 0, false, false);
        FoodItem b = dish("b", 1, 0, true, false);
        FoodItem c = dish("c", 1, 0, true, false);
        assertEquals(Arrays.asList(b, c, a), HomeDisplayRules.onSaleFirst(Arrays.asList(a, b, c)));
    }
}
