package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

/** Incident 25/10 (phiên #34): raw English errors and a payment screen polling every 3 s forever. */
public class PaymentGuardsTest {
    @Test public void raw_english_server_errors_become_vietnamese() {
        assertTrue(UserText.isTechnicalEnglish("Response status code does not indicate success: 409 (Conflict)."));
        assertTrue(UserText.isTechnicalEnglish("An error occurred while saving the entity changes."));
        assertFalse(UserText.isTechnicalEnglish("Phiên này đã được thanh toán."));
        assertFalse(UserText.isTechnicalEnglish("VT B1 TT28"));
        assertEquals("Thao tác không thực hiện được ở trạng thái hiện tại. Vui lòng tải lại và thử lại.",
                UserText.serverMessage("Response status code does not indicate success: 409 (Conflict).", 409));
        assertEquals("Máy chủ đang gặp sự cố. Vui lòng thử lại sau ít phút.", UserText.serverMessage("null", 500));
        assertEquals("Bạn đang có một giao dịch chờ xác nhận.", UserText.serverMessage("Bạn đang có một giao dịch chờ xác nhận.", 409));
    }

    @Test public void finished_payment_never_requests_its_transfer_qr() {
        assertTrue(PaymentPollPolicy.showsTransferQr("PENDING"));
        assertTrue(PaymentPollPolicy.showsTransferQr("PROCESSING"));
        assertTrue(PaymentPollPolicy.showsTransferQr(null));
        assertFalse(PaymentPollPolicy.showsTransferQr("SUCCEEDED")); // TT33, 25/10: Back from success -> QR 409
        assertFalse(PaymentPollPolicy.showsTransferQr("CANCELLED"));
        assertFalse(PaymentPollPolicy.showsTransferQr("FAILED"));
        assertFalse(PaymentPollPolicy.showsTransferQr("EXPIRED"));
    }

    @Test public void payment_polling_slows_down_and_stops_after_15_minutes() {
        assertEquals(3_000, PaymentPollPolicy.nextDelayMs(0));
        assertEquals(3_000, PaymentPollPolicy.nextDelayMs(29_000));
        assertEquals(5_000, PaymentPollPolicy.nextDelayMs(60_000));
        assertEquals(10_000, PaymentPollPolicy.nextDelayMs(3 * 60_000));
        assertEquals(30_000, PaymentPollPolicy.nextDelayMs(10 * 60_000));
        assertEquals(-1, PaymentPollPolicy.nextDelayMs(15 * 60_000));
        // 15 minutes now cost about 3 + 18 + 18 + 20 = ~60 reads instead of ~300.
        int reads = 0;
        for (long t = 0; PaymentPollPolicy.nextDelayMs(t) > 0; t += PaymentPollPolicy.nextDelayMs(t)) reads++;
        assertTrue("reads=" + reads, reads < 80);
    }
}
