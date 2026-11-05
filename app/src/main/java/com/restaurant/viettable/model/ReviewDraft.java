package com.restaurant.viettable.model;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * What the guest chose on "Đánh giá" (owner 05/11): overall stars and the three criteria each picked by the guest
 * (never copied from the overall), optional stars per dish of the invoice, quick tags, comment, anonymous name.
 */
public final class ReviewDraft {
    public int overall;
    public int food;
    public int service;
    public int space;
    public String comment = "";
    public boolean anonymous;
    public final List<String> tags = new ArrayList<>();
    /** dishId → 1..5; only dishes the guest rated. */
    public final Map<Integer, Integer> dishRatings = new LinkedHashMap<>();

    /** Every star group must be chosen; per-dish stars stay optional. */
    public boolean complete() {
        return valid(overall) && valid(food) && valid(service) && valid(space);
    }

    /** Which criterion is still missing, for the message under the button; null when complete. */
    public String missing() {
        if (!valid(overall)) return "Chọn số sao tổng thể.";
        if (!valid(food)) return "Chấm sao cho Món ăn.";
        if (!valid(service)) return "Chấm sao cho Phục vụ.";
        if (!valid(space)) return "Chấm sao cho Không gian.";
        return null;
    }

    public static ReviewDraft from(Review review) {
        ReviewDraft draft = new ReviewDraft();
        draft.overall = review.getOverallRating();
        draft.food = review.getFoodRating();
        draft.service = review.getServiceRating();
        draft.space = review.getSpaceRating();
        draft.comment = review.getComment() == null ? "" : review.getComment();
        draft.anonymous = review.isAnonymous();
        draft.tags.addAll(review.getTags());
        for (Review.DishRating rating : review.getDishRatings()) draft.dishRatings.put(rating.dishId, rating.rating);
        return draft;
    }

    private static boolean valid(int stars) {
        return stars >= 1 && stars <= 5;
    }
}
