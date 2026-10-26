package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;

import com.restaurant.viettable.model.LoyaltyInfo;
import com.restaurant.viettable.model.NotificationItem;

import org.junit.Test;

import java.util.Arrays;
import java.util.Collections;

public class LoyaltyDisplayRulesTest {

    private static NotificationItem inbox(String event, long refId, String message) {
        NotificationItem item = new NotificationItem("1", NotificationItem.Type.PAYMENT, "", message, "", false);
        item.setEventName(event);
        item.setRefId(refId);
        return item;
    }

    @Test
    public void earnedPointsComeFromThisPaymentsInboxMessage() {
        NotificationItem other = inbox(LoyaltyDisplayRules.POINTS_EARNED_EVENT, 7,
                "Bạn được cộng 99 điểm thưởng. Tổng điểm hiện có: 100.");
        NotificationItem mine = inbox(LoyaltyDisplayRules.POINTS_EARNED_EVENT, 33,
                "Bạn được cộng 1.234 điểm thưởng. Tổng điểm hiện có: 5.678.");
        assertEquals(Integer.valueOf(1234), LoyaltyDisplayRules.earnedForPayment(Arrays.asList(other, mine), 33));
        assertEquals(Integer.valueOf(99), LoyaltyDisplayRules.earnedForPayment(Arrays.asList(other, mine), 7));
    }

    @Test
    public void noMessageMeansNoNumber() {
        assertNull(LoyaltyDisplayRules.earnedForPayment(Collections.emptyList(), 33));
        assertNull(LoyaltyDisplayRules.earnedForPayment(Collections.singletonList(
                inbox("order.ready.v1", 33, "Bạn được cộng 5 điểm thưởng.")), 33));
        assertNull(LoyaltyDisplayRules.earnedForPayment(Collections.singletonList(
                inbox(LoyaltyDisplayRules.POINTS_EARNED_EVENT, 33, "Tin khác")), 33));
        assertNull(LoyaltyDisplayRules.earnedForPayment(Collections.singletonList(
                inbox(LoyaltyDisplayRules.POINTS_EARNED_EVENT, 33, "Bạn được cộng 5 điểm thưởng.")), 0));
    }

    @Test
    public void progressUsesTheServersThreshold() {
        assertEquals("1.200 / 5.000", LoyaltyDisplayRules.progressPoints(
                new LoyaltyInfo(1200, "SILVER", "Bạc", "GOLD", 5000, 3800, 5)));
        assertNull(LoyaltyDisplayRules.progressPoints(
                new LoyaltyInfo(25000, "DIAMOND", "Kim Cương", null, null, 0, 100)));
    }
}
