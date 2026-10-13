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

    public static String paymentStatus(String value) {
        switch (upper(value)) {
            case "SUCCEEDED": case "PAID": case "COMPLETED": return "Thành công";
            case "PENDING": case "UNPAID": case "OPEN": return "Đang chờ";
            case "PROCESSING": return "Đang xử lý";
            case "FAILED": return "Thất bại";
            case "CANCELLED": return "Đã hủy";
            case "EXPIRED": return "Hết hạn";
            case "REFUNDED": return "Đã hoàn tiền";
            default: return value == null ? "" : value;
        }
    }

    /** Short invoice title; the long INV-&lt;guid&gt; code is only a fallback when the number is unknown. */
    public static String invoiceTitle(int invoiceId, String invoiceCode) {
        if (invoiceId > 0) return "Hóa đơn #" + invoiceId;
        return invoiceCode == null || invoiceCode.trim().isEmpty() ? "Hóa đơn" : invoiceCode.trim();
    }

    public static String sessionLabel(int sessionId) {
        return sessionId > 0 ? "Phiên #" + sessionId : "Phiên";
    }

    /** e.g. "Tại nhà hàng • Tiền mặt • Thành công"; empty parts are skipped. */
    public static String paymentSummary(String channel, String method, String status) {
        StringBuilder value = new StringBuilder();
        for (String part : new String[]{paymentChannel(channel), paymentMethod(method), paymentStatus(status)}) {
            if (part == null || part.trim().isEmpty()) continue;
            if (value.length() > 0) value.append(" • ");
            value.append(part.trim());
        }
        return value.length() == 0 ? "Không rõ" : value.toString();
    }

    /** Display name of a membership tier; the API code (MEMBER/SILVER/GOLD/DIAMOND) is unchanged. */
    public static String memberTier(String value) {
        switch (upper(value)) {
            case "MEMBER": return "Thành viên";
            case "SILVER": return "Bạc";
            case "GOLD": return "Vàng";
            case "DIAMOND": return "Kim cương";
            default: return value == null || value.trim().isEmpty() ? "Thành viên" : value.trim();
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
