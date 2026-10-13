package com.restaurant.viettable.util;

/** Toast text after adding a dish; the cart is only visible once the customer is seated at a table. */
public final class CartMessages {
    private CartMessages() {}

    public static String added(String what, boolean hasTable) {
        return hasTable
                ? "Đã thêm " + what + " vào giỏ!"
                : "Đã lưu món. Quét QR tại bàn để xem giỏ và gửi đơn";
    }
}
