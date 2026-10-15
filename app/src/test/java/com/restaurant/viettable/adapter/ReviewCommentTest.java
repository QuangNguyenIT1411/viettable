package com.restaurant.viettable.adapter;

import com.restaurant.viettable.model.ReviewInputPolicy;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

/** "Đánh giá của tôi": a review without a comment never shows or sends the word "null". */
public class ReviewCommentTest {
    @Test public void reviewWithoutCommentShowsNoComment() {
        assertEquals("Không có nhận xét.", ReviewAdapter.commentText(null));
        assertEquals("Không có nhận xét.", ReviewAdapter.commentText("null"));
        assertEquals("Không có nhận xét.", ReviewAdapter.commentText("   "));
        assertEquals("Món ngon", ReviewAdapter.commentText(" Món ngon "));
    }

    @Test public void editingKeepsNullOutOfTheCommentBox() {
        assertEquals("", ReviewInputPolicy.normalizeComment(null));
        assertEquals("", ReviewInputPolicy.normalizeComment("null"));
        assertEquals("Ngon", ReviewInputPolicy.normalizeComment("  Ngon "));
    }
}
