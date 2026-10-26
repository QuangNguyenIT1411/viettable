package com.restaurant.viettable.model;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

import java.util.ArrayList;
import java.util.Arrays;

public class CartItemPriceTest {
    /** The app adds no size or topping surcharge: the price is the server's dish price (owner, 26/10). */
    @Test
    public void priceIsTheServersDishPrice() {
        FoodItem pho = new FoodItem("1", "1", "1", "Phở", 70_000, 0, 0, "", "", "", "", 0, true, false,
                new ArrayList<>(), new ArrayList<>());
        CartItem item = new CartItem(pho, 2, "Lớn", Arrays.asList("Trứng", "Quẩy"), 0, "");
        assertEquals(70_000, item.calculateUnitPrice());
        assertEquals(140_000, item.calculateTotalPrice());
    }
}
