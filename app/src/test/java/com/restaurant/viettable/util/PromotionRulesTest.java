package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

import com.restaurant.viettable.model.Promotion;

import org.junit.Test;

import java.util.ArrayList;
import java.util.Arrays;

/** Promotion texts in the app (docs/PROMO_PLAN.md); the amounts themselves are always the server's. */
public class PromotionRulesTest {

    private static Promotion promo(String type, double value, long cap, long min, Integer... branches) {
        return new Promotion(1, "Giờ vàng", null, null, type, value, cap, min, new ArrayList<>(Arrays.asList(branches)),
                "2026-10-27T00:00:00Z", "2026-10-30T16:59:00Z", "Giảm 10%");
    }

    @Test public void badge_shows_percent_or_amount() {
        assertEquals("−10%", PromotionRules.badge(promo("PERCENT", 10, 0, 0)));
        assertEquals("−12.5%", PromotionRules.badge(promo("PERCENT", 12.5, 0, 0)));
        assertEquals("−30.000đ", PromotionRules.badge(promo("FIXED", 30_000, 0, 0)));
    }

    @Test public void end_time_is_in_vietnam_time() {
        assertEquals("Đến 23:59, 30/10/2026", PromotionRules.until("2026-10-30T16:59:00Z"));
        assertEquals("", PromotionRules.until(null));
    }

    @Test public void conditions_say_minimum_cap_and_no_stacking() {
        String text = PromotionRules.condition(promo("PERCENT", 10, 50_000, 300_000));
        assertTrue(text, text.startsWith("Hóa đơn từ 300.000đ · giảm tối đa 50.000đ."));
        assertTrue(text.contains("không cộng dồn với voucher"));
        assertTrue(PromotionRules.condition(promo("FIXED", 30_000, 0, 0)).startsWith("Mọi hóa đơn."));
    }

    @Test public void where_it_applies() {
        assertEquals("Áp dụng: mọi chi nhánh", PromotionRules.where(promo("PERCENT", 10, 0, 0)));
        assertEquals("Áp dụng tại chi nhánh đang chọn", PromotionRules.where(promo("PERCENT", 10, 0, 0, 1, 2)));
    }
}
