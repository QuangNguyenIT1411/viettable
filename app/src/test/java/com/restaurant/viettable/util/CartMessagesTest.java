package com.restaurant.viettable.util;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class CartMessagesTest {
    @Test
    public void confirmsCartWhenSeatedAtTable() {
        assertEquals("Đã thêm Phở bò vào giỏ!", CartMessages.added("Phở bò", true));
    }

    @Test
    public void asksToScanQrWhenNoTable() {
        assertEquals("Đã lưu món. Quét QR tại bàn để xem giỏ và gửi đơn", CartMessages.added("Phở bò", false));
    }
}
