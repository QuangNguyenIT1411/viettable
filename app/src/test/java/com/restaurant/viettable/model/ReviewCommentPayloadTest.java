package com.restaurant.viettable.model;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertSame;

/** Saving a review sends a real JSON null for an empty comment, never the text "null". */
public class ReviewCommentPayloadTest {
    @Test public void emptyCommentIsSentAsJsonNull() {
        assertSame(JSONObject.NULL, ReviewInputPolicy.commentPayload(null));
        assertSame(JSONObject.NULL, ReviewInputPolicy.commentPayload(""));
        assertSame(JSONObject.NULL, ReviewInputPolicy.commentPayload("null"));
        assertEquals("Phục vụ nhanh", ReviewInputPolicy.commentPayload(" Phục vụ nhanh "));
    }
}
