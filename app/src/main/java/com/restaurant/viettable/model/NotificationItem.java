package com.restaurant.viettable.model;

import java.io.Serializable;

public class NotificationItem implements Serializable {
    public enum Type {
        RESERVATION,
        ORDER,
        PAYMENT,
        PROMOTION
    }

    private String id;
    private Type type;
    private String title;
    private String message;
    private String timestamp;
    private boolean isRead;

    public NotificationItem() {}

    public NotificationItem(String id, Type type, String title, String message, String timestamp, boolean isRead) {
        this.id = id;
        this.type = type;
        this.title = title;
        this.message = message;
        this.timestamp = timestamp;
        this.isRead = isRead;
    }

    public String getId() { return id; }
    public Type getType() { return type; }
    public String getTitle() { return title; }
    public String getMessage() { return message; }
    public String getTimestamp() { return timestamp; }
    public boolean isRead() { return isRead; }
    public void setRead(boolean read) { isRead = read; }
}
