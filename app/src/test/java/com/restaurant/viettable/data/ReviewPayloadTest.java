package com.restaurant.viettable.data;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import com.restaurant.viettable.model.Review;
import com.restaurant.viettable.model.ReviewDraft;

import org.json.JSONArray;
import org.json.JSONObject;
import org.junit.Test;

/** Review sent to the server (owner 05/11): each criterion as the guest picked it, never a copy of the overall stars. */
public class ReviewPayloadTest {
    @Test
    public void criteriaAreTheGuestsOwnAndOptionalDishStarsAreSent() throws Exception {
        ReviewDraft draft = new ReviewDraft();
        draft.overall = 4; draft.food = 5; draft.service = 3; draft.space = 2;
        draft.comment = "Phở ngon"; draft.anonymous = true;
        draft.tags.add("Ngon");
        draft.dishRatings.put(7, 5);
        JSONObject body = MobileRepository.reviewBody(draft);
        assertEquals(4, body.getInt("overallRating"));
        assertEquals(5, body.getInt("foodRating"));
        assertEquals(3, body.getInt("serviceRating"));
        assertEquals(2, body.getInt("spaceRating"));
        assertTrue(body.getBoolean("isAnonymous"));
        assertEquals("Ngon", body.getJSONArray("tags").getString(0));
        JSONArray dishes = body.getJSONArray("dishRatings");
        assertEquals(1, dishes.length());
        assertEquals(7, dishes.getJSONObject(0).getInt("dishId"));
    }

    @Test
    public void everyCriterionMustBePicked() {
        ReviewDraft draft = new ReviewDraft();
        draft.overall = 5;
        assertFalse(draft.complete());
        assertEquals("Chấm sao cho Món ăn.", draft.missing());
        draft.food = 5; draft.service = 4; draft.space = 4;
        assertTrue(draft.complete());
    }

    @Test
    public void serverReviewKeepsReplyEditWindowAndHiddenReason() throws Exception {
        Review review = MobileRepository.parseReview(new JSONObject("{\"reviewId\":3,\"invoiceId\":44,\"overallRating\":5,"
                + "\"foodRating\":5,\"serviceRating\":4,\"spaceRating\":4,\"tags\":[\"Ngon\"],\"isAnonymous\":true,"
                + "\"dishRatings\":[{\"dishId\":1,\"dishName\":\"Phở bò\",\"rating\":5}],\"canEdit\":false,"
                + "\"editableUntilUtc\":\"2026-11-06T13:00:00Z\",\"replyText\":\"Cảm ơn bạn!\",\"isHidden\":true,"
                + "\"hiddenReason\":\"Spam\"}"));
        assertFalse(review.canEdit());
        assertEquals("Cảm ơn bạn!", review.getReplyText());
        assertTrue(review.isHidden());
        assertEquals("Spam", review.getHiddenReason());
        assertEquals("Phở bò", review.getDishRatings().get(0).dishName);
        ReviewDraft again = ReviewDraft.from(review);
        assertEquals(Integer.valueOf(5), again.dishRatings.get(1));
        assertTrue(again.anonymous);
    }
}
