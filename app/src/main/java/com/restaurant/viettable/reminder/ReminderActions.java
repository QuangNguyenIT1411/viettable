package com.restaurant.viettable.reminder;

import com.restaurant.viettable.network.ApiException;

/** The two buttons on a booking reminder and what the guest reads afterwards (owner 06/11). Pure, unit tested. */
public final class ReminderActions {
    private ReminderActions() { }

    public static final String WILL_COME = "will_come";
    public static final String CANCEL = "cancel";
    public static final String LABEL_WILL_COME = "Sẽ đến";
    public static final String LABEL_CANCEL = "Huỷ lịch";

    /** True once the guest answered "Sẽ đến" (a cancelled booking gets no reminder at all). */
    public static boolean isAnswered(String arrivalConfirmation) {
        return "WillCome".equalsIgnoreCase(arrivalConfirmation);
    }

    public static boolean isKnown(String action) {
        return WILL_COME.equals(action) || CANCEL.equals(action);
    }

    public static String sending(String action) {
        return CANCEL.equals(action) ? "Đang huỷ đặt bàn…" : "Đang gửi xác nhận…";
    }

    public static String success(String action) {
        return CANCEL.equals(action) ? "Đã huỷ đặt bàn. Bàn đã được nhả cho khách khác."
                : "Đã báo nhà hàng bạn sẽ đến. Hẹn gặp bạn!";
    }

    /** The server's own message when it gave one (e.g. "Đặt bàn đã quá giờ hẹn."), else a plain retry hint. */
    public static String failure(String action, ApiException error) {
        String server = error == null ? null : error.getMessage();
        boolean network = error == null || error.getStatusCode() <= 0 || error.getStatusCode() >= 500;
        if (!network && server != null && !server.trim().isEmpty()) return server.trim();
        return CANCEL.equals(action) ? "Chưa huỷ được. Chạm để mở đặt bàn và thử lại."
                : "Chưa gửi được xác nhận. Chạm để mở đặt bàn và thử lại.";
    }
}
