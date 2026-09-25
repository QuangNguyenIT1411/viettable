package com.restaurant.viettable.model;

import java.io.Serializable;

public class ServiceRequest implements Serializable {
    public enum Status {
        REQUESTED,
        ACCEPTED,
        COMPLETED,
        CANCELLED
    }

    private String id;
    private String requestType;
    private String note;
    private Status status;
    private String time;

    public ServiceRequest() {}

    public ServiceRequest(String id, String requestType, String note, Status status, String time) {
        this.id = id;
        this.requestType = requestType;
        this.note = note;
        this.status = status;
        this.time = time;
    }

    public String getId() { return id; }
    public String getRequestType() { return requestType; }
    public String getType() { return requestType; }
    public String getNote() { return note; }
    public Status getStatus() { return status; }
    public void setStatus(Status status) { this.status = status; }
    public String getTime() { return time; }
    public String getCreatedAt() { return time; }

    public String getStatusDisplayText() {
        switch (status) {
            case REQUESTED: return "Đã gửi yêu cầu";
            case ACCEPTED: return "Nhân viên đã nhận";
            case COMPLETED: return "Đã hoàn tất";
            case CANCELLED: return "Đã hủy";
            default: return status.name();
        }
    }
}
