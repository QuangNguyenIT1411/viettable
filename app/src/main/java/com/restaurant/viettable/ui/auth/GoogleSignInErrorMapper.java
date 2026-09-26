package com.restaurant.viettable.ui.auth;

import androidx.annotation.Nullable;

import com.restaurant.viettable.network.ApiException;

final class GoogleSignInErrorMapper {
    private GoogleSignInErrorMapper() { }

    static String messageFor(@Nullable ApiException error) {
        if (error == null) return "Không thể đăng nhập bằng Google. Vui lòng thử lại.";
        if (error.getStatusCode() == 409 || "email_provider_conflict".equals(error.getCode())) {
            return "Email này đã đăng ký bằng mật khẩu. Vui lòng đăng nhập bằng tài khoản và mật khẩu.";
        }
        if (error.getMessage() != null && !error.getMessage().trim().isEmpty()) return error.getMessage();
        if (error.getStatusCode() >= 500) return "Dịch vụ đăng nhập đang tạm thời không khả dụng. Vui lòng thử lại sau.";
        if (error.getStatusCode() == 401 || error.getStatusCode() == 403) {
            return "Google không chấp nhận thông tin đăng nhập. Vui lòng chọn lại tài khoản.";
        }
        if (error.getStatusCode() == 400) return "Thông tin đăng nhập Google không hợp lệ. Vui lòng thử lại.";
        return "Không thể đăng nhập bằng Google. Vui lòng thử lại.";
    }
}
