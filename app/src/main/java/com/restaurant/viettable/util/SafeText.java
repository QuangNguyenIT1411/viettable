package com.restaurant.viettable.util;

import androidx.annotation.Nullable;

import org.json.JSONArray;
import org.json.JSONObject;

/**
 * Text that is safe to show: never the words "null" / "undefined" / "NaN".
 * Android's {@code JSONObject.optString(key, fallback)} returns the text "null" when the API sends a JSON null,
 * so every JSON string is read through {@link #json} instead.
 */
public final class SafeText {
    private SafeText() { }

    /** String field of a JSON object; "" when it is missing, JSON null or meaningless. */
    public static String json(@Nullable JSONObject object, String key) {
        return json(object, key, "");
    }

    /** String field of a JSON object; {@code fallback} (may be null) when it is missing, JSON null or meaningless. */
    @Nullable
    public static String json(@Nullable JSONObject object, String key, @Nullable String fallback) {
        if (object == null || !object.has(key) || object.isNull(key)) return fallback;
        String value = object.optString(key, "");
        return isMeaningless(value) ? fallback : value;
    }

    /** String item of a JSON array; "" when it is missing, JSON null or meaningless. */
    public static String json(@Nullable JSONArray array, int index) {
        if (array == null || index < 0 || index >= array.length() || array.isNull(index)) return "";
        String value = array.optString(index, "");
        return isMeaningless(value) ? "" : value;
    }

    /** Trimmed text for display; "" for null, blank, "null", "undefined" or "NaN". */
    public static String clean(@Nullable CharSequence value) {
        if (value == null) return "";
        String text = value.toString().trim();
        return isMeaningless(text) ? "" : text;
    }

    /** {@link #clean} or the fallback when there is nothing to show. */
    public static String or(@Nullable CharSequence value, String fallback) {
        String text = clean(value);
        return text.isEmpty() ? fallback : text;
    }

    /** Joins the parts that have something to show, e.g. join(" • ", "Bàn 3", null, "Quận 1") = "Bàn 3 • Quận 1". */
    public static String join(String separator, CharSequence... parts) {
        StringBuilder text = new StringBuilder();
        if (parts != null) for (CharSequence part : parts) {
            String value = clean(part);
            if (value.isEmpty()) continue;
            if (text.length() > 0) text.append(separator);
            text.append(value);
        }
        return text.toString();
    }

    public static boolean isBlank(@Nullable CharSequence value) {
        return clean(value).isEmpty();
    }

    private static boolean isMeaningless(@Nullable String value) {
        if (value == null) return true;
        String text = value.trim();
        return text.isEmpty() || text.equalsIgnoreCase("null") || text.equalsIgnoreCase("undefined") || text.equals("NaN");
    }
}
