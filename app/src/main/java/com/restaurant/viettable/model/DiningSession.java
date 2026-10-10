package com.restaurant.viettable.model;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.List;

public class DiningSession implements Serializable {
    public enum Status {
        CREATED,
        ACTIVE,
        PAYMENT_REQUESTED,
        PAID,
        CLOSED,
        CANCELLED
    }

    private String id;
    private String branchId;
    private String branchName;
    private String tableId; // Internal API identity; never a display number.
    private Status status;
    private String reservationId;
    private String customerName;
    private String startTime;
    private List<Order> orders;
    private List<ServiceRequest> serviceRequests;

    public DiningSession() {
        this.orders = new ArrayList<>();
        this.serviceRequests = new ArrayList<>();
        this.status = Status.ACTIVE;
    }

    public DiningSession(String id, String branchId, String branchName, String tableId,
                         Status status, String reservationId, String customerName,
                         String startTime, List<Order> orders, List<ServiceRequest> serviceRequests) {
        this.id = id;
        this.branchId = branchId;
        this.branchName = branchName;
        this.tableId = tableId;
        this.status = status;
        this.reservationId = reservationId;
        this.customerName = customerName;
        this.startTime = startTime;
        this.orders = orders != null ? orders : new ArrayList<>();
        this.serviceRequests = serviceRequests != null ? serviceRequests : new ArrayList<>();
    }

    public String getId() { return id; }
    public void setId(String id) { this.id = id; }
    public String getBranchId() { return branchId; }
    public String getBranchName() { return branchName; }
    public String getTableId() { return tableId; }
    private String tableLabel = "Chưa có thông tin bàn";
    public String getTableLabel() { return tableLabel; }
    public void setTableLabel(String label) { tableLabel = label; }
    public Status getStatus() { return status; }
    public void setStatus(Status status) { this.status = status; }
    public String getReservationId() { return reservationId; }
    public void setReservationId(String reservationId) { this.reservationId = reservationId; }
    public String getCustomerName() { return customerName; }
    public String getStartTime() { return startTime; }
    public List<Order> getOrders() { return orders; }
    public List<ServiceRequest> getServiceRequests() { return serviceRequests; }

    public int getActiveOrdersCount() {
        int count = 0;
        for (Order o : orders) {
            if (o.getStatus() != Order.Status.COMPLETED && o.getStatus() != Order.Status.CANCELLED) {
                count++;
            }
        }
        return count;
    }

    public long calculateSubtotal() {
        long total = 0;
        for (Order o : orders) {
            if (o.getStatus() != Order.Status.CANCELLED) {
                total += o.getTotalAmount();
            }
        }
        return total;
    }

    public String getFormattedSubtotal() {
        return String.format(java.util.Locale.GERMANY, "%,dđ", calculateSubtotal());
    }

    public boolean hasUncompletedItems() {
        for (Order o : orders) {
            if (o.getStatus() != Order.Status.COMPLETED && o.getStatus() != Order.Status.CANCELLED) {
                return true;
            }
        }
        return false;
    }
}
