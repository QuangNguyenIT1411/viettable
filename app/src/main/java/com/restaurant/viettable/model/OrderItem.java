package com.restaurant.viettable.model;

import java.io.Serializable;

public class OrderItem implements Serializable {
    public enum Status {
        PENDING,     // Đã gửi
        CONFIRMED,   // Nhà hàng đã nhận
        PREPARING,   // Đang chuẩn bị
        READY,       // Đã sẵn sàng
        SERVED,      // Đã phục vụ
        CANCELLED    // Đã hủy
    }

    private String id;
    private String foodName;
    private long unitPrice;
    private int quantity;
    private String optionsSummary;
    private Status status;
    private String statusTime;

    public OrderItem() {
        this.status = Status.PENDING;
    }

    public OrderItem(String id, String foodName, long unitPrice, int quantity, String optionsSummary, Status status, String statusTime) {
        this.id = id;
        this.foodName = foodName;
        this.unitPrice = unitPrice;
        this.quantity = quantity;
        this.optionsSummary = optionsSummary;
        this.status = status;
        this.statusTime = statusTime;
    }

    public String getId() { return id; }
    public String getFoodName() { return foodName; }
    public long getUnitPrice() { return unitPrice; }
    public int getQuantity() { return quantity; }
    public String getOptionsSummary() { return optionsSummary; }
    public Status getStatus() { return status; }
    public void setStatus(Status status) { this.status = status; }
    public String getStatusTime() { return statusTime; }
    public void setStatusTime(String statusTime) { this.statusTime = statusTime; }

    public long getTotalPrice() { return unitPrice * quantity; }

    public String getFormattedPrice() {
        return String.format(java.util.Locale.GERMANY, "%,dđ", getTotalPrice());
    }

    public String getCreatedAt() { return statusTime; }
    public String getFormattedTotalPrice() { return getFormattedPrice(); }

    public String getStatusDisplayText() {
        switch (status) {
            case PENDING: return "Đã gửi";
            case CONFIRMED: return "Nhà hàng đã nhận";
            case PREPARING: return "Đang chuẩn bị";
            case READY: return "Đã sẵn sàng";
            case SERVED: return "Đã phục vụ";
            case CANCELLED: return "Đã hủy";
            default: return status.name();
        }
    }
}
