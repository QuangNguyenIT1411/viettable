package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;

import com.restaurant.viettable.model.NotificationItem;

import org.junit.Test;

import java.util.Arrays;

/** Notifications review 25/10: tabs by server category, real unread count, and the booking contact phone. */
public class NotificationAndPhoneRulesTest {
    @Test public void tab_follows_the_server_category_then_the_event_name() {
        assertEquals(NotificationItem.Type.RESERVATION, NotificationRules.typeOf("reservation", "reservation.created.v1"));
        assertEquals(NotificationItem.Type.ORDER, NotificationRules.typeOf("order", "service.request-completed.v1"));
        assertEquals(NotificationItem.Type.PAYMENT, NotificationRules.typeOf("payment", "billing.payment-cancelled.v1"));
        assertEquals(NotificationItem.Type.PROMOTION, NotificationRules.typeOf("offer", "loyalty.points-earned.v1"));
        // An older server without category.
        assertEquals(NotificationItem.Type.PAYMENT, NotificationRules.typeOf(null, "billing.payment-succeeded.v1"));
        assertEquals(NotificationItem.Type.RESERVATION, NotificationRules.typeOf(null, "reservation.no-show.v1"));
        assertEquals(NotificationItem.Type.ORDER, NotificationRules.typeOf(null, "order.status-ready.v1"));
    }

    @Test public void bell_shows_the_unread_count_and_hides_at_zero() {
        NotificationItem read = new NotificationItem("1", NotificationItem.Type.ORDER, "t", "m", "", true);
        NotificationItem unread = new NotificationItem("2", NotificationItem.Type.ORDER, "t", "m", "", false);
        assertEquals(1, NotificationRules.unread(Arrays.asList(read, unread)));
        assertNull(NotificationRules.badge(0));
        assertEquals("7", NotificationRules.badge(7));
        assertEquals("99+", NotificationRules.badge(150));
    }

    @Test public void contact_phone_accepts_common_vietnamese_formats() {
        assertEquals("0343000273", PhoneRules.normalize("0343000273"));
        assertEquals("0343000273", PhoneRules.normalize(" 0343 000 273 "));
        assertEquals("0343000273", PhoneRules.normalize("0343-000-273"));
        assertEquals("0343000273", PhoneRules.normalize("+84 343 000 273"));
        assertEquals("0343000273", PhoneRules.normalize("84343000273"));
        assertNull(PhoneRules.normalize(""));
        assertNull(PhoneRules.normalize("034300027"));     // 9 digits
        assertNull(PhoneRules.normalize("1343000273"));    // not starting with 0
        assertNull(PhoneRules.normalize("0343abc273"));
        assertNull(PhoneRules.normalize("03430+00273"));   // "+" only in front
    }
}
