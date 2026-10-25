package com.restaurant.viettable.model;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.List;

public class Reservation implements Serializable {
    public enum Status {
        PENDING,
        PENDING_DEPOSIT,
        CONFIRMED,
        CHECKING_IN,
        CHECKED_IN,
        COMPLETED,
        CANCELLED,
        NO_SHOW
    }

    private String id;
    private String branchId;
    private String branchName;
    private String branchAddress;
    private String date;
    private String timeSlot;
    private int guestCount;
    private String areaName; // Trong nhà, Ngoài trời, Phòng riêng, Gần cửa sổ
    private String tableId; // Internal API identity; display uses tableLabel.
    private String note;
    private List<String> specialTags; // Sinh nhật, Kỷ niệm, Tiếp khách, Cầu hôn
    private long depositAmount;
    private Status status;
    private boolean hasPreOrder;
    private String contactName;
    private String contactPhone;
    private String reservedAt; // server instant (ISO); date/timeSlot are its display texts

    public Reservation() {
        this.specialTags = new ArrayList<>();
        this.status = Status.CONFIRMED;
    }

    public Reservation(String id, String branchId, String branchName, String branchAddress,
                       String date, String timeSlot, int guestCount, String areaName,
                       String tableId, String note, List<String> specialTags,
                       long depositAmount, Status status, boolean hasPreOrder,
                       String contactName, String contactPhone) {
        this.id = id;
        this.branchId = branchId;
        this.branchName = branchName;
        this.branchAddress = branchAddress;
        this.date = date;
        this.timeSlot = timeSlot;
        this.guestCount = guestCount;
        this.areaName = areaName;
        this.tableId = tableId;
        this.note = note;
        this.specialTags = specialTags != null ? specialTags : new ArrayList<>();
        this.depositAmount = depositAmount;
        this.status = status;
        this.hasPreOrder = hasPreOrder;
        this.contactName = contactName;
        this.contactPhone = contactPhone;
    }

    public String getId() { return id; }
    public void setId(String id) { this.id = id; }
    public String getBranchId() { return branchId; }
    public String getBranchName() { return branchName; }
    public String getBranchAddress() { return branchAddress; }
    public String getDate() { return date; }
    public void setDate(String date) { this.date = date; }
    public String getTimeSlot() { return timeSlot; }
    public void setTimeSlot(String timeSlot) { this.timeSlot = timeSlot; }
    public int getGuestCount() { return guestCount; }
    public void setGuestCount(int guestCount) { this.guestCount = guestCount; }
    public String getAreaName() { return areaName; }
    public void setAreaName(String areaName) { this.areaName = areaName; }
    public String getTableId() { return tableId; }
    private String tableLabel = "Chưa có thông tin bàn";
    public String getTableLabel() { return tableLabel; }
    public void setTableLabel(String label) { tableLabel = label; }
    public String getTableNumber() { return tableLabel; }
    public void setTableId(String tableId) { this.tableId = tableId; }
    public String getNote() { return note; }
    public void setNote(String note) { this.note = note; }
    public List<String> getSpecialTags() { return specialTags; }
    public long getDepositAmount() { return depositAmount; }
    public void setDepositAmount(long depositAmount) { this.depositAmount = depositAmount; }
    public Status getStatus() { return status; }
    public void setStatus(Status status) { this.status = status; }
    public String getReservedAt() { return reservedAt; }
    public void setReservedAt(String reservedAt) { this.reservedAt = reservedAt; }
    public boolean hasPreOrder() { return hasPreOrder; }
    public void setHasPreOrder(boolean hasPreOrder) { this.hasPreOrder = hasPreOrder; }
    public String getContactName() { return contactName; }
    public String getContactPhone() { return contactPhone; }

    public String getStatusDisplayText() {
        switch (status) {
            case PENDING: return "Chờ xử lý";
            case PENDING_DEPOSIT: return "Chờ đặt cọc";
            case CONFIRMED: return "Đã xác nhận";
            case CHECKING_IN: return "Đang check-in";
            case CHECKED_IN: return "Đã check-in";
            case COMPLETED: return "Hoàn thành";
            case CANCELLED: return "Đã hủy";
            case NO_SHOW: return "Vắng mặt";
            default: return status.name();
        }
    }
}
