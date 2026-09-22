package com.restaurant.viettable.model;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.List;

public final class SplitBill implements Serializable {
    private final int splitBillId;
    private final int checkoutQuoteId;
    private final int diningSessionId;
    private final long totalAmount;
    private final String status;
    private final List<Part> parts;

    public SplitBill(int splitBillId, int checkoutQuoteId, int diningSessionId,
                     long totalAmount, String status, List<Part> parts) {
        this.splitBillId = splitBillId;
        this.checkoutQuoteId = checkoutQuoteId;
        this.diningSessionId = diningSessionId;
        this.totalAmount = totalAmount;
        this.status = status;
        this.parts = parts == null ? new ArrayList<>() : parts;
    }

    public int getSplitBillId() { return splitBillId; }
    public int getCheckoutQuoteId() { return checkoutQuoteId; }
    public int getDiningSessionId() { return diningSessionId; }
    public long getTotalAmount() { return totalAmount; }
    public String getStatus() { return status; }
    public List<Part> getParts() { return parts; }
    public boolean isPaid() { return "PAID".equalsIgnoreCase(status); }
    public boolean areAllPartsPaid() {
        if (parts.isEmpty()) return false;
        for (Part part : parts) if (!part.isPaid()) return false;
        return true;
    }

    public static final class Part implements Serializable {
        private final int splitBillPartId;
        private final String label;
        private final long amount;
        private final String status;
        private final Integer paymentId;
        private final String paymentStatus;

        public Part(int splitBillPartId, String label, long amount, String status, Integer paymentId,
                    String paymentStatus) {
            this.splitBillPartId = splitBillPartId;
            this.label = label;
            this.amount = amount;
            this.status = status;
            this.paymentId = paymentId;
            this.paymentStatus = paymentStatus;
        }

        public int getSplitBillPartId() { return splitBillPartId; }
        public String getLabel() { return label; }
        public long getAmount() { return amount; }
        public String getStatus() { return status; }
        public Integer getPaymentId() { return paymentId; }
        public String getPaymentStatus() { return paymentStatus; }
        public boolean isPaid() { return "PAID".equalsIgnoreCase(status); }
        public boolean canStartPayment() { return "UNPAID".equalsIgnoreCase(status); }
        public boolean canRetryPayment() {
            return paymentId != null && ("FAILED".equalsIgnoreCase(paymentStatus)
                    || "CANCELLED".equalsIgnoreCase(paymentStatus)
                    || "EXPIRED".equalsIgnoreCase(paymentStatus));
        }
        public String getFormattedAmount() { return String.format(java.util.Locale.GERMANY, "%,dđ", amount); }
    }
}
