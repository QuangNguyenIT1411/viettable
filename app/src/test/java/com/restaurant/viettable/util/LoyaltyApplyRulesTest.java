package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import com.restaurant.viettable.network.ApiException;

import org.junit.Test;

public class LoyaltyApplyRulesTest {

    @Test
    public void serverDiscountAboveZeroIsSuccess() {
        LoyaltyApplyRules.Outcome outcome = LoyaltyApplyRules.ofServerQuote(10_000);
        assertTrue(outcome.applied());
        assertEquals(LoyaltyApplyRules.Kind.APPLIED, outcome.kind);
        assertEquals(10_000, outcome.discountVnd);
        assertEquals("Đã áp dụng −10.000đ", outcome.message);
    }

    @Test
    public void serverAnswerOfZeroIsNotSuccess() {
        LoyaltyApplyRules.Outcome outcome = LoyaltyApplyRules.ofServerQuote(0);
        assertFalse(outcome.applied());
        assertEquals(LoyaltyApplyRules.Kind.ZERO, outcome.kind);
        assertTrue(outcome.message.contains("chưa thay đổi"));
    }

    @Test
    public void insufficientPointsIsExplained() {
        LoyaltyApplyRules.Outcome outcome = LoyaltyApplyRules.ofError(
                new ApiException(409, "insufficient_points", "Điểm khả dụng không đủ.", null));
        assertFalse(outcome.applied());
        assertEquals(LoyaltyApplyRules.Kind.INSUFFICIENT_POINTS, outcome.kind);
        assertTrue(outcome.message.contains("không đủ"));
        assertTrue(outcome.message.contains("chưa thay đổi"));
    }

    @Test
    public void stateConflictTellsHowToUnlock() {
        LoyaltyApplyRules.Outcome outcome = LoyaltyApplyRules.ofError(
                new ApiException(409, "state_conflict", "Trạng thái hiện tại không cho phép thao tác này.", null));
        assertFalse(outcome.applied());
        assertEquals(LoyaltyApplyRules.Kind.QUOTE_LOCKED, outcome.kind);
        assertEquals(LoyaltyApplyRules.LOCKED_EXPLANATION, outcome.message);
        assertTrue(outcome.message.contains("Hủy yêu cầu"));
    }

    @Test
    public void expiredQuoteAndUnknownErrorsKeepTheBillUnchanged() {
        assertEquals(LoyaltyApplyRules.Kind.QUOTE_EXPIRED, LoyaltyApplyRules.ofError(
                new ApiException(409, "quote_expired", "Báo giá đã hết hạn.", null)).kind);
        LoyaltyApplyRules.Outcome other = LoyaltyApplyRules.ofError(new ApiException(503, "boom", "Máy chủ bận.", null));
        assertEquals(LoyaltyApplyRules.Kind.OTHER, other.kind);
        assertTrue(other.message.startsWith("Máy chủ bận."));
        assertEquals(LoyaltyApplyRules.Kind.OTHER, LoyaltyApplyRules.ofError(null).kind);
    }

    @Test
    public void onlyAnOpenQuoteIsEditable() {
        assertFalse(LoyaltyApplyRules.isQuoteLocked("OPEN"));
        assertFalse(LoyaltyApplyRules.isQuoteLocked(null));
        assertFalse(LoyaltyApplyRules.isQuoteLocked(""));
        assertTrue(LoyaltyApplyRules.isQuoteLocked("PAYMENT_REQUESTED"));
        assertTrue(LoyaltyApplyRules.isQuoteLocked("CANCELLED"));
        assertTrue(LoyaltyApplyRules.isQuoteLocked("EXPIRED"));
    }
}
