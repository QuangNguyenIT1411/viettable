package com.restaurant.viettable.util;

import java.util.Locale;
import java.util.regex.Pattern;

/** Vietnamese labels for API codes that would otherwise leak into customer-facing text. */
public final class StatusLabels {
    public static final String EVENT_ORDER_READY = "order.status-ready.v1";
    public static final String EVENT_PAYMENT_SUCCEEDED = "billing.payment-succeeded.v1";
    private static final Pattern ORDER_CODE = Pattern.compile("đơn ORD-[A-Za-z0-9-]+");

    private StatusLabels() {}

    public static String paymentChannel(String value) {
        switch (upper(value)) {
            case "ONSITE": return "Tại nhà hàng";
            case "ONLINE": return "Trực tuyến";
            default: return value == null ? "" : value;
        }
    }

    public static String paymentMethod(String value) {
        switch (upper(value)) {
            case "CASH": return "Tiền mặt";
            case "QR": return "QR chuyển khoản";
            case "TRANSFER": return "Chuyển khoản";
            case "CARD": return "Thẻ";
            case "VNPAY": return "VNPAY";
            default: return value == null ? "" : value;
        }
    }

    public static String paymentLocation(String value) {
        switch (upper(value)) {
            case "TABLE": return "Tại bàn";
            case "CASHIER": return "Tại quầy thu ngân";
            default: return value == null ? "" : value;
        }
    }

    public static String notificationTitle(String eventName) {
        if (EVENT_PAYMENT_SUCCEEDED.equals(eventName)) return "Thanh toán thành công";
        if (EVENT_ORDER_READY.equals(eventName)) return "Món ăn đã sẵn sàng";
        return "Cập nhật đơn hàng";
    }

    /** Older notifications embed the long internal order code; show the short order number instead. */
    public static String notificationMessage(String message, long orderId) {
        if (message == null) return "";
        if (orderId <= 0) return message;
        return ORDER_CODE.matcher(message).replaceAll("đơn #" + orderId);
    }

    private static String upper(String value) {
        return value == null ? "" : value.trim().toUpperCase(Locale.ROOT);
    }
}
