package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

import com.restaurant.viettable.model.SupportChat;

import org.json.JSONObject;
import org.junit.Test;

import java.time.ZoneId;
import java.util.Arrays;
import java.util.List;

public class SupportTextTest {
    private static final ZoneId VN = ZoneId.of("Asia/Ho_Chi_Minh");

    private static final String DETAIL = "{\"conversation\":{\"supportConversationId\":7,\"branchId\":1,\"topic\":\"DEPOSIT_TRANSFER\","
            + "\"topicLabel\":\"Đã chuyển khoản đặt cọc nhưng chưa được xác nhận\",\"contextType\":\"DEPOSIT\",\"contextId\":37,"
            + "\"contextLabel\":\"Cọc đặt bàn #37 · 500.000đ\",\"status\":\"InProgress\",\"unread\":1,\"lastMessageAtUtc\":\"2026-11-07T03:00:00Z\","
            + "\"customerLastReadMessageId\":2,\"staffLastReadMessageId\":1,\"createdAtUtc\":\"2026-11-07T02:00:00Z\","
            + "\"lastMessage\":{\"supportMessageId\":3,\"senderType\":\"STAFF\",\"senderName\":\"Lan\",\"body\":\"Mình kiểm tra ngay\"}},"
            + "\"messages\":[{\"supportMessageId\":1,\"senderType\":\"CUSTOMER\",\"body\":\"Tôi đã chuyển\",\"createdAtUtc\":\"2026-11-07T02:00:00Z\"},"
            + "{\"supportMessageId\":2,\"senderType\":\"AUTO\",\"body\":\"Tin nhắn tự động: Nhân viên sẽ trả lời bạn sau ít phút.\",\"createdAtUtc\":\"2026-11-07T02:00:00Z\"},"
            + "{\"supportMessageId\":3,\"senderType\":\"STAFF\",\"senderName\":\"Lan\",\"body\":\"Mình kiểm tra ngay\",\"createdAtUtc\":\"2026-11-07T03:00:00Z\"},"
            + "{\"supportMessageId\":4,\"senderType\":\"CUSTOMER\",\"body\":\"Cảm ơn\",\"createdAtUtc\":\"2026-11-07T03:01:00Z\"}],\"hasOlder\":true}";

    @Test
    public void parsesTheServerAnswer() throws Exception {
        SupportChat.Detail d = SupportChat.Detail.parse(new JSONObject(DETAIL));
        assertEquals(7, d.conversation.id);
        assertEquals(Integer.valueOf(37), d.conversation.contextId);
        assertEquals("Mình kiểm tra ngay", d.conversation.lastMessagePreview);
        assertEquals(4, d.messages.size());
        assertTrue(d.hasOlder);
        assertTrue(d.messages.get(1).automatic());
    }

    @Test
    public void sentThenSeen() throws Exception {
        SupportChat.Detail d = SupportChat.Detail.parse(new JSONObject(DETAIL));
        assertEquals("Đã xem", SupportText.delivery(d.messages.get(0), d.conversation));
        assertEquals("Đã gửi", SupportText.delivery(d.messages.get(3), d.conversation));
        assertEquals("", SupportText.delivery(d.messages.get(2), d.conversation)); // staff message
        assertEquals("Nhân viên Lan", SupportText.sender(d.messages.get(2)));
        assertEquals("Tin nhắn tự động", SupportText.sender(d.messages.get(1)));
        assertEquals("Bạn", SupportText.sender(d.messages.get(0)));
    }

    @Test
    public void confirmedDepositSaysWhen() {
        SupportChat.Check ok = new SupportChat.Check(true, "2026-11-07T12:20:00Z", 500000, "VT DC37", "Đã nhận tiền cọc", 1);
        assertEquals("Khoản này đã được xác nhận lúc 19:20 07/11 (Đã nhận tiền cọc).", SupportText.confirmedText(ok, VN));
    }

    @Test
    public void notConfirmedRemindsTheRightContentAndAmount() {
        SupportChat.Check wait = new SupportChat.Check(false, null, 500000, "VT DC37", "Chưa nhận được tiền cọc", 1);
        String text = SupportText.notConfirmedText(wait);
        assertTrue(text, text.contains("đúng 500.000đ"));
        assertTrue(text, text.contains("\"VT DC37\""));
        assertTrue(text, text.startsWith("Hệ thống chưa nhận được khoản này (Chưa nhận được tiền cọc)."));
        SupportChat.Topic topic = new SupportChat.Topic("DEPOSIT_TRANSFER", "Đã chuyển khoản đặt cọc nhưng chưa được xác nhận", "DEPOSIT", true);
        assertEquals("Tôi đã chuyển 500.000đ với nội dung \"VT DC37\" nhưng chưa được xác nhận.", SupportText.suggestedMessage(topic, null, wait));
    }

    @Test
    public void olderPagesMergeWithoutDuplicates() throws Exception {
        List<SupportChat.Message> current = SupportChat.Detail.parse(new JSONObject(DETAIL)).messages;
        SupportChat.Message older = new SupportChat.Message(0, "CUSTOMER", null, "cũ", null);
        List<SupportChat.Message> merged = SupportText.mergeOlder(Arrays.asList(older, current.get(0)), current);
        assertEquals(5, merged.size());
        assertEquals(0, merged.get(0).id);
    }

    @Test
    public void statusWordsAndLimits() {
        assertEquals("Đang chờ", SupportText.statusLabel("Waiting"));
        assertEquals("Đang xử lý", SupportText.statusLabel("InProgress"));
        assertEquals("Đã giải quyết", SupportText.statusLabel("Resolved"));
        assertNull(SupportText.whyNotSendable("Xin chào"));
        assertFalse(SupportText.whyNotSendable("  ") == null);
        assertFalse(SupportText.whyNotSendable(new String(new char[1001]).replace('\0', 'a')) == null);
    }
}
