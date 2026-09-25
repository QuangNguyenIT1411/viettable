package com.restaurant.viettable.data;

import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.model.OrderItem;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class MobileOrderStatusMappingTest {
    @Test public void orderStatesFromOrdersApiKeepKitchenProgress() {
        assertEquals(Order.Status.PLACED, MobileRepository.orderStatus("PENDING"));
        assertEquals(Order.Status.CONFIRMED, MobileRepository.orderStatus("CONFIRMED"));
        assertEquals(Order.Status.IN_PROGRESS, MobileRepository.orderStatus("PREPARING"));
        assertEquals(Order.Status.IN_PROGRESS, MobileRepository.orderStatus("READY"));
        assertEquals(Order.Status.COMPLETED, MobileRepository.orderStatus("SERVING"));
        assertEquals(Order.Status.CANCELLED, MobileRepository.orderStatus("CANCELLED"));
    }

    @Test public void servedItemDoesNotFallBackToPending() {
        assertEquals(OrderItem.Status.PENDING, MobileRepository.orderItemStatus("PENDING"));
        assertEquals(OrderItem.Status.CONFIRMED, MobileRepository.orderItemStatus("CONFIRMED"));
        assertEquals(OrderItem.Status.PREPARING, MobileRepository.orderItemStatus("PREPARING"));
        assertEquals(OrderItem.Status.READY, MobileRepository.orderItemStatus("READY"));
        assertEquals(OrderItem.Status.SERVED, MobileRepository.orderItemStatus("SERVING"));
        assertEquals(OrderItem.Status.CANCELLED, MobileRepository.orderItemStatus("CANCELLED"));
    }
}
