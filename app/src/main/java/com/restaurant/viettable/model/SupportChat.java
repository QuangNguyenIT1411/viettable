package com.restaurant.viettable.model;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/** "Chat với nhân viên hỗ trợ" (owner 06/11): everything comes from the server; nothing here is invented. */
public final class SupportChat {
    private SupportChat() { }

    public static final class Topic implements Serializable {
        public final String code, label, contextType;
        public final boolean checksPayment;
        public Topic(String code, String label, String contextType, boolean checksPayment) {
            this.code = code; this.label = label; this.contextType = contextType; this.checksPayment = checksPayment;
        }
        public static Topic parse(JSONObject o) {
            return new Topic(o.optString("code"), o.optString("label"), o.isNull("contextType") ? null : o.optString("contextType", null),
                    o.optBoolean("checksPayment"));
        }
    }

    /** One of the customer's own items a conversation can be about. */
    public static final class Option implements Serializable {
        public final String contextType, label, detail;
        public final int contextId, branchId;
        public Option(String contextType, int contextId, int branchId, String label, String detail) {
            this.contextType = contextType; this.contextId = contextId; this.branchId = branchId; this.label = label; this.detail = detail;
        }
        public static Option parse(JSONObject o) {
            return new Option(o.optString("contextType"), o.optInt("contextId"), o.optInt("branchId"), o.optString("label"),
                    o.isNull("detail") ? null : o.optString("detail", null));
        }
    }

    /** Topic 1/2 check: is this deposit / transfer already confirmed? */
    public static final class Check {
        public final boolean confirmed;
        public final String confirmedAtUtc, transferContent, statusText;
        public final long amount;
        public final int branchId;
        public Check(boolean confirmed, String confirmedAtUtc, long amount, String transferContent, String statusText, int branchId) {
            this.confirmed = confirmed; this.confirmedAtUtc = confirmedAtUtc; this.amount = amount; this.transferContent = transferContent;
            this.statusText = statusText; this.branchId = branchId;
        }
        public static Check parse(JSONObject o) {
            return new Check(o.optBoolean("confirmed"), str(o, "confirmedAtUtc"), o.optLong("amount"), str(o, "transferContent"),
                    str(o, "statusText"), o.optInt("branchId"));
        }
    }

    public static final class Message {
        public final long id;
        public final String senderType, senderName, body, createdAtUtc;
        public Message(long id, String senderType, String senderName, String body, String createdAtUtc) {
            this.id = id; this.senderType = senderType; this.senderName = senderName; this.body = body; this.createdAtUtc = createdAtUtc;
        }
        public boolean fromCustomer() { return "CUSTOMER".equals(senderType); }
        public boolean automatic() { return "AUTO".equals(senderType); }
        public static Message parse(JSONObject o) {
            return new Message(o.optLong("supportMessageId"), o.optString("senderType"), str(o, "senderName"), o.optString("body"),
                    str(o, "createdAtUtc"));
        }
    }

    public static final class Conversation implements Serializable {
        public final int id, branchId, unread;
        public final Integer contextId;
        public final String topic, topicLabel, contextType, contextLabel, status, lastMessageAtUtc, lastMessagePreview, createdAtUtc;
        public final long customerLastReadMessageId, staffLastReadMessageId;
        public Conversation(int id, int branchId, String topic, String topicLabel, String contextType, Integer contextId, String contextLabel,
                            String status, int unread, String lastMessageAtUtc, String lastMessagePreview, long customerLastRead,
                            long staffLastRead, String createdAtUtc) {
            this.id = id; this.branchId = branchId; this.topic = topic; this.topicLabel = topicLabel; this.contextType = contextType;
            this.contextId = contextId; this.contextLabel = contextLabel; this.status = status; this.unread = unread;
            this.lastMessageAtUtc = lastMessageAtUtc; this.lastMessagePreview = lastMessagePreview; this.customerLastReadMessageId = customerLastRead;
            this.staffLastReadMessageId = staffLastRead; this.createdAtUtc = createdAtUtc;
        }
        public boolean resolved() { return "Resolved".equals(status); }
        public static Conversation parse(JSONObject o) {
            JSONObject last = o.optJSONObject("lastMessage");
            return new Conversation(o.optInt("supportConversationId"), o.optInt("branchId"), o.optString("topic"), o.optString("topicLabel"),
                    o.optString("contextType", "NONE"), o.isNull("contextId") || !o.has("contextId") ? null : o.optInt("contextId"),
                    str(o, "contextLabel"), o.optString("status"), o.optInt("unread"), str(o, "lastMessageAtUtc"),
                    last == null ? null : last.optString("body"), o.optLong("customerLastReadMessageId"), o.optLong("staffLastReadMessageId"),
                    str(o, "createdAtUtc"));
        }
    }

    public static final class Detail {
        public final Conversation conversation;
        public final List<Message> messages;
        public final boolean hasOlder;
        public Detail(Conversation conversation, List<Message> messages, boolean hasOlder) {
            this.conversation = conversation; this.messages = Collections.unmodifiableList(messages); this.hasOlder = hasOlder;
        }
        public static Detail parse(JSONObject o) {
            List<Message> list = new ArrayList<>();
            JSONArray rows = o.optJSONArray("messages");
            if (rows != null) for (int i = 0; i < rows.length(); i++) { JSONObject m = rows.optJSONObject(i); if (m != null) list.add(Message.parse(m)); }
            JSONObject c = o.optJSONObject("conversation");
            return new Detail(c == null ? null : Conversation.parse(c), list, o.optBoolean("hasOlder"));
        }
    }

    public static <T> List<T> items(JSONObject o, java.util.function.Function<JSONObject, T> parse) {
        List<T> out = new ArrayList<>();
        JSONArray rows = o == null ? null : o.optJSONArray("items");
        if (rows != null) for (int i = 0; i < rows.length(); i++) { JSONObject r = rows.optJSONObject(i); if (r != null) out.add(parse.apply(r)); }
        return out;
    }

    private static String str(JSONObject o, String name) { return o.isNull(name) || !o.has(name) ? null : o.optString(name, null); }
}
