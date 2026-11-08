package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

import org.json.JSONObject;
import org.junit.Test;

/**
 * Owner 08/11: "Quên mật khẩu" said the request failed although the server answered 200. On Production the server
 * never returns the code (it is e-mailed); the app treated the missing "resetToken" as an error and stopped.
 */
public class PasswordResetRulesTest {
    @Test
    public void productionAnswerWithoutCodeIsAccepted() throws Exception {
        JSONObject production = new JSONObject("{\"message\":\"Nếu tài khoản tồn tại, mã đặt lại mật khẩu đã được gửi.\",\"resetToken\":null,\"expiresAtUtc\":null}");
        assertTrue(PasswordResetRules.accepted(production));
        assertEquals("Nếu tài khoản tồn tại, mã đặt lại mật khẩu đã được gửi.", PasswordResetRules.message(production));
    }

    @Test
    public void emptyBodyIsAcceptedWithAPlainSentence() {
        assertTrue(PasswordResetRules.accepted(new JSONObject()));
        assertTrue(PasswordResetRules.accepted(null));
        assertEquals(PasswordResetRules.DEFAULT_SENT, PasswordResetRules.message(null));
    }

    @Test
    public void codeIsSixDigits() {
        assertNull(PasswordResetRules.whyBadCode("123456"));
        assertNull(PasswordResetRules.whyBadCode(" 123 456 "));
        assertEquals("Mã xác nhận gồm 6 chữ số.", PasswordResetRules.whyBadCode("12345"));
        assertEquals("Vui lòng nhập mã xác nhận.", PasswordResetRules.whyBadCode(""));
        assertEquals("123456", PasswordResetRules.digits(" 12-34 56"));
    }
}
