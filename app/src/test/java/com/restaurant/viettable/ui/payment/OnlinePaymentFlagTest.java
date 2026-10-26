package com.restaurant.viettable.ui.payment;

import static org.junit.Assert.assertFalse;

import com.restaurant.viettable.BuildConfig;

import org.junit.Test;

public class OnlinePaymentFlagTest {
    /** "Thanh toán Online" (VNPAY) stays hidden unless a build explicitly turns it on (owner, 26/10). */
    @Test
    public void onlinePaymentIsOffByDefault() {
        assertFalse(BuildConfig.ONLINE_PAYMENT_ENABLED);
    }
}
