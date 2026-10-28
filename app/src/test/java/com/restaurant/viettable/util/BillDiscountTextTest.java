package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;

import com.restaurant.viettable.model.Voucher;

import org.junit.Test;

/** Bill discount rows (owner 28/10/2026): the automatic promotion is never mistaken for a voucher. */
public class BillDiscountTextTest {

    @Test
    public void promotion_is_named_as_automatic() {
        assertEquals("Khuyến mãi tự động: Giờ vàng", BillDiscountText.promotionLabel("Giờ vàng"));
        assertEquals("Khuyến mãi tự động", BillDiscountText.promotionLabel(null));
    }

    @Test
    public void promotion_amount_says_applied_declined_or_not_applied() {
        assertEquals("-50.000đ", BillDiscountText.promotionAmount(50_000, false));
        assertEquals("Không dùng", BillDiscountText.promotionAmount(0, true));
        assertEquals("Không áp", BillDiscountText.promotionAmount(0, false)); // the voucher is better
    }

    @Test
    public void promotion_button_toggles() {
        assertEquals("Không dùng khuyến mãi", BillDiscountText.promotionToggle(false));
        assertEquals("Dùng lại khuyến mãi", BillDiscountText.promotionToggle(true));
    }

    @Test
    public void voucher_row_shows_code_and_amount_or_not_applied() {
        assertEquals("Voucher CHAOBAN50", BillDiscountText.voucherLabel("CHAOBAN50"));
        assertEquals("-30.000đ", BillDiscountText.voucherAmount(30_000));
        assertEquals("Không áp", BillDiscountText.voucherAmount(0));
    }

    @Test
    public void voucher_card_description_shows_code_and_minimum() {
        Voucher withMin = new Voucher("A50", "Giảm", 50_000, 0, 300_000, "", true, null, "", false);
        Voucher any = new Voucher("B", "Giảm", 1_000, 0, 0, "", true, null, "", false);
        assertEquals("A50 · Đơn từ 300.000đ", BillDiscountText.voucherDescription(withMin));
        assertEquals("B · Mọi hoá đơn", BillDiscountText.voucherDescription(any));
    }

    @Test
    public void greyed_voucher_without_a_reason_still_says_something() {
        Voucher voucher = new Voucher("C", "C", 1_000, 0, 0, "", true, null, "", false);
        voucher.setAvailability(false, "voucher_sold_out", null, null, -1, false, 0);
        assertEquals("Không dùng được", BillDiscountText.voucherStatus(voucher));
    }
}
