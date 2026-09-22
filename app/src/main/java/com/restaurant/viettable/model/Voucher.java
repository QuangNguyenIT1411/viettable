package com.restaurant.viettable.model;

import java.io.Serializable;

public class Voucher implements Serializable {
    private int backendId;
    private String code;
    private String title;
    private long discountAmount;
    private int discountPercent;
    private long minOrderAmount;
    private String expiryDate;
    private boolean isGlobal;
    private String branchId;
    private String description;
    private boolean isUsed;

    public Voucher() {}

    public Voucher(String code, String title, long discountAmount, int discountPercent,
                   long minOrderAmount, String expiryDate, boolean isGlobal, String branchId,
                   String description, boolean isUsed) {
        this.code = code;
        this.title = title;
        this.discountAmount = discountAmount;
        this.discountPercent = discountPercent;
        this.minOrderAmount = minOrderAmount;
        this.expiryDate = expiryDate;
        this.isGlobal = isGlobal;
        this.branchId = branchId;
        this.description = description;
        this.isUsed = isUsed;
    }

    public String getCode() { return code; }
    public String getTitle() { return title; }
    public long getDiscountAmount() { return discountAmount; }
    public int getDiscountPercent() { return discountPercent; }
    public long getMinOrderAmount() { return minOrderAmount; }
    public String getExpiryDate() { return expiryDate; }
    public boolean isGlobal() { return isGlobal; }
    public String getBranchId() { return branchId; }
    public String getDescription() { return description; }
    public boolean isUsed() { return isUsed; }
    public void setUsed(boolean used) { isUsed = used; }
    public int getBackendId() { return backendId; }
    public void setBackendId(int backendId) { this.backendId = backendId; }

    public long calculateDiscount(long orderSubtotal) {
        if (orderSubtotal < minOrderAmount) return 0;
        if (discountAmount > 0) return discountAmount;
        if (discountPercent > 0) return (orderSubtotal * discountPercent) / 100;
        return 0;
    }
}
