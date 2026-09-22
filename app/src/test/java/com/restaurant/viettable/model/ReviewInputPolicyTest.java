package com.restaurant.viettable.model;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class ReviewInputPolicyTest {
    @Test public void rating_acceptsOnlyBackendRange() {
        assertFalse(ReviewInputPolicy.isRatingValid(0));
        assertTrue(ReviewInputPolicy.isRatingValid(1));
        assertTrue(ReviewInputPolicy.isRatingValid(5));
        assertFalse(ReviewInputPolicy.isRatingValid(6));
    }

    @Test public void comment_isTrimmedAndLimitedToTwoThousandCharacters() {
        assertEquals("Nội dung", ReviewInputPolicy.normalizeComment("  Nội dung  "));
        assertTrue(ReviewInputPolicy.isCommentValid("a".repeat(2000)));
        assertFalse(ReviewInputPolicy.isCommentValid("a".repeat(2001)));
    }
}
