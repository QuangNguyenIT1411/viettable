package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;

import com.restaurant.viettable.model.OrderItem;

import org.junit.Test;

import java.util.Arrays;
import java.util.Collections;

public class DishProgressTest {
    private static OrderItem dish(OrderItem.Status status) {
        return new OrderItem("1", "Món", 1, 1, "", status, "");
    }

    @Test
    public void readyAndServedCountAsDoneCancelledIsLeftOut() {
        DishProgress p = DishProgress.of(Arrays.asList(dish(OrderItem.Status.SERVED), dish(OrderItem.Status.READY),
                dish(OrderItem.Status.PREPARING), dish(OrderItem.Status.PENDING), dish(OrderItem.Status.CONFIRMED),
                dish(OrderItem.Status.CANCELLED)));
        assertEquals("2/5", p.label());
        assertEquals(40, p.percent());
    }

    @Test
    public void noDishes() {
        DishProgress p = DishProgress.of(Collections.emptyList());
        assertEquals(0, p.total);
        assertEquals(0, p.percent());
    }
}
