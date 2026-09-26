package com.restaurant.viettable.ui.auth;

import com.restaurant.viettable.network.ApiException;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class GoogleSignInErrorMapperTest {
    @Test public void providerConflict_explainsPasswordLogin() {
        ApiException error = new ApiException(409, "email_provider_conflict", "Email này đã đăng ký bằng mật khẩu.", null);

        assertEquals("Email này đã đăng ký bằng mật khẩu. Vui lòng đăng nhập bằng tài khoản và mật khẩu.",
                GoogleSignInErrorMapper.messageFor(error));
    }

    @Test public void backendMessage_isPreservedForOtherHttpErrors() {
        ApiException error = new ApiException(401, "invalid_google_token", "Không thể xác thực tài khoản Google.", null);

        assertEquals("Không thể xác thực tài khoản Google.", GoogleSignInErrorMapper.messageFor(error));
    }
}
