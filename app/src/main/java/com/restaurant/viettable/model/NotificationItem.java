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

    // The reservation / order / payment the notification is about (0 when unknown), and the server event name.
    private long refId;
    private String eventName;
    public long getRefId() { return refId; }
    public void setRefId(long refId) { this.refId = refId; }
    public String getEventName() { return eventName; }
    public void setEventName(String eventName) { this.eventName = eventName; }
}
