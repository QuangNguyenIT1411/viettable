package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import com.restaurant.viettable.model.SupportChat;

import org.junit.Test;

/** Owner 07/11 (Samsung): the prefilled message follows the topic; screen readers do not repeat "Tin nhắn tự động". */
public class SupportTextPrefillTest {
    @Test
    public void prefillIsReplacedUntilTheCustomerTypes() {
        assertTrue(SupportText.replacePrefill("", ""));
        assertTrue(SupportText.replacePrefill("Đổi / huỷ đặt bàn: Đặt bàn #40.", "Đổi / huỷ đặt bàn: Đặt bàn #40."));
        assertFalse(SupportText.replacePrefill("Tôi muốn đổi giờ", "Đổi / huỷ đặt bàn: Đặt bàn #40."));
    }

    @Test
    public void automaticMessageIsReadOnce() {
        SupportChat.Message auto = new SupportChat.Message(2, "AUTO", null, "Tin nhắn tự động: Nhân viên sẽ trả lời bạn sau ít phút.", null);
        assertEquals("Tin nhắn tự động: Nhân viên sẽ trả lời bạn sau ít phút. 01:47", SupportText.spoken(auto, "01:47"));
        SupportChat.Message staff = new SupportChat.Message(3, "STAFF", "Lan", "Chào bạn", null);
        assertEquals("Nhân viên Lan: Chào bạn. 01:48", SupportText.spoken(staff, "01:48"));
    }
}
