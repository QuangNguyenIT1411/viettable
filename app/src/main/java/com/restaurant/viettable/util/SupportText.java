package com.restaurant.viettable.util;

import com.restaurant.viettable.model.SupportChat;

import java.time.Instant;
import java.time.ZoneId;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

/** What the support chat screens say (owner 06/11). Pure; unit tested. */
public final class SupportText {
    private SupportText() { }

    private static final DateTimeFormatter TIME_DATE = DateTimeFormatter.ofPattern("HH:mm dd/MM", new Locale("vi", "VN"));
    private static final DateTimeFormatter TIME = DateTimeFormatter.ofPattern("HH:mm", new Locale("vi", "VN"));

    public static String statusLabel(String status) {
        if ("Waiting".equals(status)) return "Đang chờ";
        if ("InProgress".equals(status)) return "Đang xử lý";
        if ("Resolved".equals(status)) return "Đã giải quyết";
        return status == null ? "" : status;
    }

    public static String money(long value) { return String.format(Locale.GERMANY, "%,dđ", value); }

    public static String at(String utc, ZoneId zone) {
        Instant i = DisplayFormatters.instant(utc);
        return i == null ? "" : i.atZone(zone).format(TIME_DATE);
    }

    public static String clock(String utc, ZoneId zone) {
        Instant i = DisplayFormatters.instant(utc);
        return i == null ? "" : i.atZone(zone).format(TIME);
    }

    /** Topic 1/2, already confirmed: "Khoản này đã được xác nhận lúc 19:20 07/11." */
    public static String confirmedText(SupportChat.Check check, ZoneId zone) {
        String when = check.confirmedAtUtc == null ? "" : " lúc " + at(check.confirmedAtUtc, zone);
        return "Khoản này đã được xác nhận" + when + (check.statusText == null || check.statusText.isEmpty() ? "." : " (" + check.statusText + ").");
    }

    /** Topic 1/2, not confirmed yet: what the transfer must say and how much, before opening a chat. */
    public static String notConfirmedText(SupportChat.Check check) {
        StringBuilder b = new StringBuilder("Hệ thống chưa nhận được khoản này");
        if (check.statusText != null && !check.statusText.isEmpty()) b.append(" (").append(check.statusText).append(")");
        b.append(".\nHãy kiểm tra bạn đã chuyển");
        if (check.amount > 0) b.append(" đúng ").append(money(check.amount));
        if (check.transferContent != null && !check.transferContent.isEmpty())
            b.append(" với nội dung chuyển khoản \"").append(check.transferContent).append("\"");
        b.append(". Tiền chuyển đúng nội dung thường được xác nhận tự động sau ít phút. Nếu đã chuyển đúng mà vẫn chưa được xác nhận, hãy nhắn cho nhân viên.");
        return b.toString();
    }

    /** First message pre-filled from the item, so staff see the facts (the customer may edit it). */
    public static String suggestedMessage(SupportChat.Topic topic, SupportChat.Option option, SupportChat.Check check) {
        if (topic == null) return "";
        if (check != null && !check.confirmed && check.transferContent != null)
            return "Tôi đã chuyển " + money(check.amount) + " với nội dung \"" + check.transferContent + "\" nhưng chưa được xác nhận.";
        if (option != null) return topic.label + ": " + option.label + ".";
        return "";
    }

    /** "Đã gửi" until staff read it, then "Đã xem"; nothing for staff or automatic messages. */
    public static String delivery(SupportChat.Message m, SupportChat.Conversation c) {
        if (m == null || c == null || !m.fromCustomer()) return "";
        return m.id <= c.staffLastReadMessageId ? "Đã xem" : "Đã gửi";
    }

    /** Who wrote it, for the bubble label. */
    public static String sender(SupportChat.Message m) {
        if (m.automatic()) return "Tin nhắn tự động";
        if (m.fromCustomer()) return "Bạn";
        return m.senderName == null || m.senderName.isEmpty() ? "Nhân viên" : "Nhân viên " + m.senderName;
    }

    /** Older page + current list, without duplicates, oldest first. */
    public static List<SupportChat.Message> mergeOlder(List<SupportChat.Message> older, List<SupportChat.Message> current) {
        Map<Long, SupportChat.Message> byId = new LinkedHashMap<>();
        if (older != null) for (SupportChat.Message m : older) byId.put(m.id, m);
        if (current != null) for (SupportChat.Message m : current) byId.put(m.id, m);
        List<SupportChat.Message> out = new ArrayList<>(byId.values());
        out.sort((a, b) -> Long.compare(a.id, b.id));
        return out;
    }

    /** The prefilled message may be replaced while it is empty or still exactly the last sentence the app put there. */
    public static boolean replacePrefill(String current, String lastSuggested) {
        String now = current == null ? "" : current.trim();
        return now.isEmpty() || now.equals(lastSuggested == null ? "" : lastSuggested.trim());
    }

    /** What a screen reader says for a bubble; automatic messages already start with "Tin nhắn tự động:". */
    public static String spoken(SupportChat.Message m, String meta) {
        String body = m.body == null ? "" : m.body.trim();
        String head = m.automatic() ? body : sender(m) + ": " + body;
        if (!head.endsWith(".") && !head.endsWith("?") && !head.endsWith("!")) head += ".";
        return meta == null || meta.isEmpty() ? head : head + " " + meta;
    }

    public static int totalUnread(List<SupportChat.Conversation> rows) {
        int n = 0;
        if (rows != null) for (SupportChat.Conversation c : rows) n += Math.max(0, c.unread);
        return n;
    }

    public static final int MAX_LENGTH = 1000;

    /** Null when the text may be sent; otherwise why not (the server checks again). */
    public static String whyNotSendable(String text) {
        if (text == null || text.trim().isEmpty()) return "Vui lòng nhập nội dung tin nhắn.";
        if (text.trim().length() > MAX_LENGTH) return "Tin nhắn tối đa " + MAX_LENGTH + " ký tự.";
        return null;
    }
}
