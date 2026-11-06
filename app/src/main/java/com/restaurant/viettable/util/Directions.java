package com.restaurant.viettable.util;

import android.content.ActivityNotFoundException;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;

import java.util.Locale;

/**
 * "Chỉ đường" (owner 06/11): opens the phone's map app at the branch, no API key. geo: first (any map app);
 * a Google Maps link when no app handles geo:.
 */
public final class Directions {
    private Directions() { }

    public static boolean isValid(double latitude, double longitude) {
        if (Double.isNaN(latitude) || Double.isNaN(longitude)) return false;
        if (latitude == 0 && longitude == 0) return false;
        return latitude >= -90 && latitude <= 90 && longitude >= -180 && longitude <= 180;
    }

    /** geo:10.776889,106.700806?q=10.776889,106.700806(Chi%20nh%C3%A1nh%20Qu%E1%BA%ADn%201) */
    public static String geoUri(double latitude, double longitude, String label) {
        String point = String.format(Locale.ROOT, "%.6f,%.6f", latitude, longitude);
        String name = label == null ? "" : label.replace('(', ' ').replace(')', ' ').trim();
        return "geo:" + point + "?q=" + encode(point + (name.isEmpty() ? "" : "(" + name + ")"));
    }

    /** https://www.google.com/maps/dir/?api=1&destination=10.776889,106.700806 */
    public static String webUri(double latitude, double longitude) {
        return String.format(Locale.ROOT, "https://www.google.com/maps/dir/?api=1&destination=%.6f,%.6f", latitude, longitude);
    }

    /** False when the position is missing or nothing on the phone can show a map. */
    public static boolean open(Context context, double latitude, double longitude, String label) {
        if (context == null || !isValid(latitude, longitude)) return false;
        try {
            context.startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(geoUri(latitude, longitude, label))));
            return true;
        } catch (ActivityNotFoundException noMapApp) {
            try {
                context.startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(webUri(latitude, longitude))));
                return true;
            } catch (ActivityNotFoundException nothing) {
                return false;
            }
        }
    }

    // Plain Java (android.net.Uri.encode is not available to unit tests); keeps "," and "(" ")" readable.
    static String encode(String value) {
        try {
            return java.net.URLEncoder.encode(value, "UTF-8").replace("+", "%20")
                    .replace("%2C", ",").replace("%28", "(").replace("%29", ")");
        } catch (java.io.UnsupportedEncodingException e) { return value; }
    }
}
