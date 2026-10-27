package com.restaurant.viettable.model;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.List;

public class ReservationDraft implements Serializable {
    private Branch branch;
    private String date = "";
    private String timeSlot = "";
    private int guestCount = 4;
    private String areaName = "";
    private String tableId;
    private String note = "";
    private List<String> specialTags = new ArrayList<>();
    private long depositAmount = 0;
    private boolean needsDeposit = false;

    public ReservationDraft() {}

    public Branch getBranch() { return branch; }
    public void setBranch(Branch branch) { this.branch = branch; }

    public String getDate() { return date; }
    public void setDate(String date) { this.date = date; }

    public String getTimeSlot() { return timeSlot; }
    public void setTimeSlot(String timeSlot) { this.timeSlot = timeSlot; }

    // Typed on the note screen; empty means "use my profile" (they used to be ignored, 25/10).
    private String contactName = "";
    private String contactPhone = "";
    public String getContactName() { return contactName; }
    public void setContactName(String contactName) { this.contactName = contactName == null ? "" : contactName.trim(); }
    public String getContactPhone() { return contactPhone; }
    public void setContactPhone(String contactPhone) { this.contactPhone = contactPhone == null ? "" : contactPhone.trim(); }

    public int getGuestCount() { return guestCount; }
    public void setGuestCount(int guestCount) {
        this.guestCount = guestCount;
        // Whether a deposit is owed comes from the server's policy (ResGuestCountFragment), never a fixed rule here.
    }

    public String getAreaName() { return areaName; }
    public void setAreaName(String areaName) { this.areaName = areaName; }

    public String getTableId() { return tableId; }
    public void setTableId(String tableId) { this.tableId = tableId; }

    public String getNote() { return note; }
    public void setNote(String note) { this.note = note; }

    public List<String> getSpecialTags() { return specialTags; }
    public void setSpecialTags(List<String> specialTags) { this.specialTags = specialTags; }

    public long getDepositAmount() { return depositAmount; }
    public void setDepositAmount(long depositAmount) { this.depositAmount = depositAmount; }

    public boolean isNeedsDeposit() { return needsDeposit; }
    public void setNeedsDeposit(boolean needsDeposit) { this.needsDeposit = needsDeposit; }
}
