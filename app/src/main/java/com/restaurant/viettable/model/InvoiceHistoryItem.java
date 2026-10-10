package com.restaurant.viettable.model;

import java.util.ArrayList;
import java.util.List;

public class InvoiceHistoryItem {
    public static class Line {
        private final String name;
        private final int quantity;
        private final long total;
        public Line(String name, int quantity, long total) { this.name = name; this.quantity = quantity; this.total = total; }
        public String getName() { return name; }
        public int getQuantity() { return quantity; }
        public long getTotal() { return total; }
    }

    private final int invoiceId;
    private final String invoiceCode;
    private final int checkoutQuoteId;
    private final int paymentId;
    private final int diningSessionId;
    private final String diningSessionCode;
    private final String branchName;
    private final String tableId;
    private final long subtotal;
    private final long discount;
    private final long totalAmount;
    private final String issuedAtUtc;
    private final String paymentCode;
    private final String paymentChannel;
    private final String paymentMethod;
    private final String paymentLocation;
    private final String paymentStatus;
    private final List<Line> items;

    public InvoiceHistoryItem(int invoiceId, String invoiceCode, int checkoutQuoteId, int paymentId,
                              int diningSessionId, String diningSessionCode, String branchName, String tableId,
                              long subtotal, long discount, long totalAmount, String issuedAtUtc,
                              String paymentCode, String paymentChannel, String paymentMethod,
                              String paymentLocation, String paymentStatus, List<Line> items) {
        this.invoiceId = invoiceId; this.invoiceCode = invoiceCode; this.checkoutQuoteId = checkoutQuoteId;
        this.paymentId = paymentId; this.diningSessionId = diningSessionId; this.diningSessionCode = diningSessionCode;
        this.branchName = branchName; this.tableId = tableId; this.subtotal = subtotal; this.discount = discount;
        this.totalAmount = totalAmount; this.issuedAtUtc = issuedAtUtc; this.paymentCode = paymentCode;
        this.paymentChannel = paymentChannel; this.paymentMethod = paymentMethod; this.paymentLocation = paymentLocation;
        this.paymentStatus = paymentStatus; this.items = items == null ? new ArrayList<>() : items;
    }

    public int getInvoiceId() { return invoiceId; }
    public String getInvoiceCode() { return invoiceCode; }
    public int getCheckoutQuoteId() { return checkoutQuoteId; }
    public int getPaymentId() { return paymentId; }
    public int getDiningSessionId() { return diningSessionId; }
    public String getDiningSessionCode() { return diningSessionCode; }
    public String getBranchName() { return branchName; }
    public String getTableId() { return tableId; }
    private String tableLabel = "Chưa có thông tin bàn";
    public String getTableLabel() { return tableLabel; }
    public void setTableLabel(String label) { tableLabel = label; }
    public long getSubtotal() { return subtotal; }
    public long getDiscount() { return discount; }
    public long getTotalAmount() { return totalAmount; }
    public String getIssuedAtUtc() { return issuedAtUtc; }
    public String getPaymentCode() { return paymentCode; }
    public String getPaymentChannel() { return paymentChannel; }
    public String getPaymentMethod() { return paymentMethod; }
    public String getPaymentLocation() { return paymentLocation; }
    public String getPaymentStatus() { return paymentStatus; }
    public List<Line> getItems() { return items; }
}
