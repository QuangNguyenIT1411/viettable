package com.restaurant.viettable.model;

public final class ReviewInputPolicy {
    private ReviewInputPolicy() {}
    public static boolean isRatingValid(int rating) { return rating >= 1 && rating <= 5; }
    public static String normalizeComment(String value) { return value == null ? "" : value.trim(); }
    public static boolean isCommentValid(String value) { return normalizeComment(value).length() <= 2000; }
}
