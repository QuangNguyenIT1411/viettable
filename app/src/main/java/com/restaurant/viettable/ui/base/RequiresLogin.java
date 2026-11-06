package com.restaurant.viettable.ui.base;

/**
 * Screens that only make sense with an account (owner 07/11: a guest tapping "Thông tin cá nhân" saw an empty screen).
 * {@link BaseFragment#navigateTo} shows "Bạn chưa đăng nhập" with a way to log in instead, when there is no session.
 */
public interface RequiresLogin {
    /** What the guest wanted, for "Đăng nhập để xem …", e.g. "thông tin cá nhân". */
    String loginPurpose();
}
