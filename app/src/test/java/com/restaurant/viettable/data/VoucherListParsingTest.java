package com.restaurant.viettable.data;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

import com.restaurant.viettable.model.Voucher;
import com.restaurant.viettable.util.BillDiscountText;

import org.json.JSONArray;
import org.junit.Test;

import java.util.List;

/** GET /me/vouchers?quoteId= (owner 28/10/2026): usable first, greyed ones with the server's reason. */
public class VoucherListParsingTest {

    private static final String BODY = "["
            + "{\"voucherId\":3,\"code\":\"GOOD\",\"name\":\"Giảm 30.000đ\",\"discountType\":\"FIXED\",\"discountValue\":30000,"
            + "\"minSubtotal\":0,\"endsAtUtc\":\"2026-11-30T16:59:00Z\",\"usable\":true,\"reasonCode\":null,\"reason\":null,"
            + "\"remainingLabel\":\"Còn 2 lượt\",\"discount\":30000,\"applied\":true,\"usedByMe\":0},"
            + "{\"voucherId\":4,\"code\":\"BIG\",\"name\":\"Giảm 10%\",\"discountType\":\"PERCENT\",\"discountValue\":10,"
            + "\"minSubtotal\":500000,\"endsAtUtc\":\"2026-11-30T16:59:00Z\",\"usable\":false,\"reasonCode\":\"voucher_minimum_not_met\","
            + "\"reason\":\"Chưa đủ đơn tối thiểu 500.000đ (còn thiếu 100.000đ)\",\"remainingLabel\":null,\"discount\":40000,\"applied\":false,\"usedByMe\":0},"
            + "{\"voucherId\":5,\"code\":\"ONCE\",\"name\":\"Chào bạn\",\"discountType\":\"FIXED\",\"discountValue\":50000,"
            + "\"minSubtotal\":0,\"endsAtUtc\":\"2026-11-30T16:59:00Z\",\"usable\":false,\"reasonCode\":\"voucher_used_up\","
            + "\"reason\":\"Đã dùng\",\"remainingLabel\":null,\"discount\":50000,\"applied\":false,\"usedByMe\":1}"
            + "]";

    @Test
    public void parses_usable_and_greyed_vouchers() throws Exception {
        List<Voucher> list = MobileRepository.parseVouchers(new JSONArray(BODY));
        assertEquals(3, list.size());

        Voucher good = list.get(0);
        assertEquals(3, good.getBackendId());
        assertTrue(good.isUsable());
        assertTrue(good.isApplied());
        assertEquals(30_000, good.getBillDiscount());
        assertEquals("Giảm 30.000đ cho hoá đơn này · Còn 2 lượt", BillDiscountText.voucherStatus(good));

        Voucher big = list.get(1);
        assertFalse(big.isUsable());
        assertEquals(10, big.getDiscountPercent());
        assertEquals("Chưa đủ đơn tối thiểu 500.000đ (còn thiếu 100.000đ)", BillDiscountText.voucherStatus(big));

        Voucher once = list.get(2);
        assertTrue(once.isUsed());
        assertEquals("Đã dùng", BillDiscountText.voucherStatus(once));
    }

    @Test
    public void profile_list_without_a_bill_has_no_bill_amount() throws Exception {
        List<Voucher> list = MobileRepository.parseVouchers(new JSONArray("[{\"voucherId\":1,\"code\":\"A\",\"name\":\"A\","
                + "\"discountType\":\"FIXED\",\"discountValue\":1000,\"minSubtotal\":0,\"usable\":true,\"discount\":null,"
                + "\"remainingLabel\":null}]"));
        assertEquals(-1, list.get(0).getBillDiscount());
        assertNull(BillDiscountText.voucherStatus(list.get(0)));
    }

    @Test
    public void old_server_without_the_new_fields_keeps_vouchers_usable() throws Exception {
        List<Voucher> list = MobileRepository.parseVouchers(new JSONArray("[{\"voucherId\":1,\"code\":\"A\",\"name\":\"A\","
                + "\"discountType\":\"FIXED\",\"discountValue\":1000,\"minSubtotal\":0}]"));
        assertTrue(list.get(0).isUsable());
        assertFalse(list.get(0).isUsed());
    }
}
