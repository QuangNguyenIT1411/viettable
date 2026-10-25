package com.restaurant.viettable.util;

import com.restaurant.viettable.model.NotificationItem;

import java.util.List;

/** Which tab a server notification belongs to, and the bell's unread count (notifications review 25/10). */
public final class NotificationRules {
    private NotificationRules() { }

    /**
     * The server's category wins ("reservation", "order", "payment", "offer"). Older servers sent none: payment
     * events by name, everything else under orders (dish ready).
     */
    public static NotificationItem.Type typeOf(String category, String eventName) {
        if (category != null) switch (category) {
            case "reservation": return NotificationItem.Type.RESERVATION;
            case "payment": return NotificationItem.Type.PAYMENT;
            case "offer": return NotificationItem.Type.PROMOTION;
            case "order": return NotificationItem.Type.ORDER;
            default: break;
        }
        if (eventName != null) {
            if (eventName.startsWith("reservation.")) return NotificationItem.Type.RESERVATION;
            if (eventName.startsWith("billing.")) return NotificationItem.Type.PAYMENT;
            if (eventName.startsWith("loyalty.") || eventName.startsWith("promotion.")) return NotificationItem.Type.PROMOTION;
        }
        return NotificationItem.Type.ORDER;
    }

    public static int unread(List<NotificationItem> items) {
        int count = 0;
        if (items != null) for (NotificationItem item : items) if (!item.isRead()) count++;
        return count;
    }

    /** Bell badge text: hidden at 0, "99+" above 99. */
    public static String badge(int unread) {
        if (unread <= 0) return null;
        return unread > 99 ? "99+" : String.valueOf(unread);
    }
}
