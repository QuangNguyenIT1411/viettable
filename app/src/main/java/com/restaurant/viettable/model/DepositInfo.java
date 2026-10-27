package com.restaurant.viettable.model;

import org.json.JSONObject;

/**
 * A booking's deposit from GET /api/mobile/v1/reservations/{id}/deposit. Status: NOT_REQUIRED, PENDING, EXPIRED,
 * CANCELLED, PAID, APPLIED, REFUND_REQUIRED, REFUNDED, FORFEITED. The VietQR is there only while PENDING.
 */
public final class DepositInfo {
    private final boolean required;
    private final long amount;
    private final String status;
    private final String message;
    private final String dueAtUtc;
    private final String closedReason;
    private final String reservationStatus;
    private final JSONObject transfer;

    public DepositInfo(boolean required, long amount, String status, String message, String dueAtUtc, String closedReason,
                       String reservationStatus, JSONObject transfer) {
        this.required = required;
        this.amount = amount;
        this.status = status == null ? "NOT_REQUIRED" : status;
        this.message = message == null ? "" : message;
        this.dueAtUtc = dueAtUtc;
        this.closedReason = closedReason;
        this.reservationStatus = reservationStatus;
        this.transfer = transfer;
    }

    public boolean isRequired() { return required; }
    public long getAmount() { return amount; }
    public String getStatus() { return status; }
    public String getMessage() { return message; }
    public String getDueAtUtc() { return dueAtUtc; }
    public String getClosedReason() { return closedReason; }
    public String getReservationStatus() { return reservationStatus; }
    /** bankName, accountNumber, accountName, amount, content, qrPngBase64, autoConfirm; null when not waiting. */
    public JSONObject getTransfer() { return transfer; }
}
