package com.restaurant.viettable.model;

public final class ReviewInputPolicy {
    private ReviewInputPolicy() {}
    public static boolean isRatingValid(int rating) { return rating >= 1 && rating <= 5; }
    /** Typed comment, trimmed; "" when empty or a leftover "null". */
    public static String normalizeComment(String value) { return com.restaurant.viettable.util.SafeText.clean(value); }
    public static boolean isCommentValid(String value) { return normalizeComment(value).length() <= 2000; }
    /** Value sent for "comment": JSON null when empty, never the text "null". */
    public static Object commentPayload(String value) {
        String text = normalizeComment(value);
        return text.isEmpty() ? org.json.JSONObject.NULL : text;
    }
}
