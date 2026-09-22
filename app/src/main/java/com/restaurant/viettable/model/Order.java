package com.restaurant.viettable.model;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.List;

public class Order implements Serializable {
    public enum Type {
        PRE_ORDER,
        DINE_IN
    }

    public enum Status {
        PLACED,       // Khách vừa gửi
        CONFIRMED,    // Nhà hàng đã xác nhận
        IN_PROGRESS,  // Ít nhất một món đang chuẩn bị / sẵn sàng / phục vụ
        COMPLETED,    // Tất cả món đã phục vụ hoặc hủy hợp lệ
        CANCELLED     // Đơn bị hủy
    }

    private String id;
    private String diningSessionId;
    private String branchName;
    private String tableId;
    private Type orderType;
    private Status status;
    private String orderTime;
    private List<OrderItem> items;
    private long totalAmount;
    private String eta;

    public Order() {
        this.items = new ArrayList<>();
        this.status = Status.PLACED;
        this.orderType = Type.DINE_IN;
    }

    public Order(String id, String diningSessionId, String branchName, String tableId,
                 Type orderType, Status status, String orderTime, List<OrderItem> items,
                 long totalAmount, String eta) {
        this.id = id;
        this.diningSessionId = diningSessionId;
        this.branchName = branchName;
        this.tableId = tableId;
        this.orderType = orderType;
        this.status = status;
        this.orderTime = orderTime;
        this.items = items != null ? items : new ArrayList<>();
        this.totalAmount = totalAmount;
        this.eta = eta;
    }

    public String getId() { return id; }
    public String getDiningSessionId() { return diningSessionId; }
    public String getBranchName() { return branchName; }
    public String getTableId() { return tableId; }
    public Type getOrderType() { return orderType; }
    public Status getStatus() { return status; }
    public void setStatus(Status status) { this.status = status; }
    public String getOrderTime() { return orderTime; }
    public List<OrderItem> getItems() { return items; }
    public long getTotalAmount() { return totalAmount; }
    public String getEta() { return eta; }

    public String getFormattedTotal() {
        return String.format(java.util.Locale.GERMANY, "%,dđ", totalAmount);
    }

    public String getCreatedAt() { return orderTime; }
    public String getFormattedTotalAmount() { return getFormattedTotal(); }
    public String getTableNumber() { return tableId; }

    public String getStatusDisplayText() {
        switch (status) {
            case PLACED: return "Đã đặt";
            case CONFIRMED: return "Nhà hàng xác nhận";
            case IN_PROGRESS: return "Đang xử lý";
            case COMPLETED: return "Hoàn thành";
            case CANCELLED: return "Đã hủy";
            default: return status.name();
        }
    }

    public boolean isAllItemsCompleted() {
        if (items == null || items.isEmpty()) return false;
        for (OrderItem item : items) {
            if (item.getStatus() != OrderItem.Status.SERVED && item.getStatus() != OrderItem.Status.CANCELLED) {
                return false;
            }
        }
        return true;
    }
}
