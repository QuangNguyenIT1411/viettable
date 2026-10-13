package com.restaurant.viettable.util;

import com.restaurant.viettable.model.OrderItem;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class StatusLabelsTest {
    private static OrderItem item(OrderItem.Status status) {
        return new OrderItem("1", "Bò né", 50000, 1, "", status, "2026-10-12T15:58:48Z");
    }

    @Test
    public void readyServedAndCancelledDishesAreNotKitchenUnfinished() {
        assertTrue(item(OrderItem.Status.PENDING).isKitchenUnfinished());
        assertTrue(item(OrderItem.Status.CONFIRMED).isKitchenUnfinished());
        assertTrue(item(OrderItem.Status.PREPARING).isKitchenUnfinished());
        assertFalse(item(OrderItem.Status.READY).isKitchenUnfinished());
        assertFalse(item(OrderItem.Status.SERVED).isKitchenUnfinished());
        assertFalse(item(OrderItem.Status.CANCELLED).isKitchenUnfinished());
    }

    @Test
    public void cookedDishesCannotBeCancelledFromTheApp() {
        assertTrue(item(OrderItem.Status.PREPARING).canRequestCancel());
        assertFalse(item(OrderItem.Status.READY).canRequestCancel());
        assertFalse(item(OrderItem.Status.SERVED).canRequestCancel());
        assertFalse(item(OrderItem.Status.CANCELLED).canRequestCancel());
    }

    @Test
    public void everyItemStatusHasVietnameseText() {
        for (OrderItem.Status status : OrderItem.Status.values()) {
            String text = item(status).getStatusDisplayText();
            assertFalse(text, text.equals(status.name()));
        }
        assertEquals("Đã sẵn sàng", item(OrderItem.Status.READY).getStatusDisplayText());
    }

    @Test
    public void itemKeepsItsOrderTimeForDisplay() {
        assertEquals("2026-10-12T15:58:48Z", item(OrderItem.Status.READY).getCreatedAt());
    }

    @Test
    public void notificationTitlesFollowTheEventType() {
        assertEquals("Thanh toán thành công", StatusLabels.notificationTitle("billing.payment-succeeded.v1"));
        assertEquals("Món ăn đã sẵn sàng", StatusLabels.notificationTitle("order.status-ready.v1"));
        assertEquals("Cập nhật đơn hàng", StatusLabels.notificationTitle("something.else"));
    }

    @Test
    public void longOrderCodeIsReplacedByShortOrderNumber() {
        assertEquals("Bò né đã sẵn sàng. Vui lòng đến nhận món cho đơn #44.",
                StatusLabels.notificationMessage("Bò né đã sẵn sàng. Vui lòng đến nhận món cho đơn ORD-20261012155848-BB3F12AC.", 44));
        assertEquals("Thanh toán thành công", StatusLabels.notificationMessage("Thanh toán thành công", 0));
    }

    @Test
    public void paymentCodesAreShownInVietnamese() {
        assertEquals("Tại nhà hàng", StatusLabels.paymentChannel("ONSITE"));
        assertEquals("Tiền mặt", StatusLabels.paymentMethod("cash"));
        assertEquals("QR chuyển khoản", StatusLabels.paymentMethod("QR"));
        assertEquals("Tại quầy thu ngân", StatusLabels.paymentLocation("CASHIER"));
        assertEquals("", StatusLabels.paymentLocation(null));
    }

    @Test
    public void invoiceDetailUsesVietnamesePaymentSummary() {
        assertEquals("Tại nhà hàng • Tiền mặt • Thành công", StatusLabels.paymentSummary("ONSITE", "CASH", "SUCCEEDED"));
        assertEquals("Tiền mặt • Đang chờ", StatusLabels.paymentSummary(null, "cash", "PENDING"));
        assertEquals("Không rõ", StatusLabels.paymentSummary(null, "", null));
        assertEquals("Thất bại", StatusLabels.paymentStatus("FAILED"));
        assertEquals("Hết hạn", StatusLabels.paymentStatus("expired"));
    }

    @Test
    public void invoiceAndSessionUseShortNumbers() {
        assertEquals("Hóa đơn #12", StatusLabels.invoiceTitle(12, "INV-d81d52c7aa"));
        assertEquals("INV-d81d52c7aa", StatusLabels.invoiceTitle(0, "INV-d81d52c7aa"));
        assertEquals("Hóa đơn", StatusLabels.invoiceTitle(0, null));
        assertEquals("Phiên #29", StatusLabels.sessionLabel(29));
    }

    @Test
    public void memberTiersAreShownInVietnamese() {
        assertEquals("Thành viên", StatusLabels.memberTier("MEMBER"));
        assertEquals("Bạc", StatusLabels.memberTier("silver"));
        assertEquals("Vàng", StatusLabels.memberTier("GOLD"));
        assertEquals("Kim cương", StatusLabels.memberTier("DIAMOND"));
        assertEquals("Thành viên", StatusLabels.memberTier(null));
        assertEquals("PLATINUM", StatusLabels.memberTier("PLATINUM"));
    }
}
