package com.restaurant.viettable.model;

import java.io.Serializable;

public class Invoice implements Serializable {
    private String id;
    private String diningSessionId;
    private String branchName;
    private String tableId;
    private long subtotal;
    private long serviceFee;
    private long tax;
    private long voucherDiscount;
    private long loyaltyDiscount;
    private long finalTotal;
    private String paymentMethod;
    private String paymentLocation;
    private String timestamp;
    private int backendInvoiceId;

    public Invoice() {}

    public Invoice(String id, String diningSessionId, String branchName, String tableId,
                   long subtotal, long serviceFee, long tax, long voucherDiscount,
                   long loyaltyDiscount, long finalTotal, String paymentMethod,
                   String paymentLocation, String timestamp) {
        this.id = id;
        this.diningSessionId = diningSessionId;
        this.branchName = branchName;
        this.tableId = tableId;
        this.subtotal = subtotal;
        this.serviceFee = serviceFee;
        this.tax = tax;
        this.voucherDiscount = voucherDiscount;
        this.loyaltyDiscount = loyaltyDiscount;
        this.finalTotal = finalTotal;
        this.paymentMethod = paymentMethod;
        this.paymentLocation = paymentLocation;
        this.timestamp = timestamp;
    }

    public String getId() { return id; }
    public String getDiningSessionId() { return diningSessionId; }
    public String getBranchName() { return branchName; }
    public String getTableId() { return tableId; }
    private String tableLabel = "Chưa có thông tin bàn";
    public String getTableLabel() { return tableLabel; }
    public void setTableLabel(String label) { tableLabel = label; }
    public long getSubtotal() { return subtotal; }
    public long getServiceFee() { return serviceFee; }
    public long getTax() { return tax; }
    public long getVoucherDiscount() { return voucherDiscount; }
    public long getLoyaltyDiscount() { return loyaltyDiscount; }
    public long getFinalTotal() { return finalTotal; }
    public String getPaymentMethod() { return paymentMethod; }
    public String getPaymentLocation() { return paymentLocation; }
    public String getTimestamp() { return timestamp; }
    public int getBackendInvoiceId() { return backendInvoiceId; }
    public void setBackendInvoiceId(int backendInvoiceId) { this.backendInvoiceId = backendInvoiceId; }

    public String getFormattedFinalTotal() {
        return String.format(java.util.Locale.GERMANY, "%,dđ", finalTotal);
    }
}
