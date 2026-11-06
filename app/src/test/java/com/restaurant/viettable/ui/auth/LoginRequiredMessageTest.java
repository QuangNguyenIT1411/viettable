package com.restaurant.viettable.ui.auth;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

/** Owner 07/11: a guest on an account-only screen reads why and can log in. */
public class LoginRequiredMessageTest {
    @Test
    public void saysWhatNeedsAnAccount() {
        assertEquals("Đăng nhập để xem thông tin cá nhân.", LoginRequiredFragment.message("thông tin cá nhân"));
        assertEquals("Đăng nhập để dùng chức năng này.", LoginRequiredFragment.message(null));
        assertEquals("Đăng nhập để dùng chức năng này.", LoginRequiredFragment.message("  "));
    }
}
