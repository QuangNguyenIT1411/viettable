package com.restaurant.viettable.util;

import com.restaurant.viettable.model.FoodItem;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Locale;

/** What Trang chủ shows, kept out of the fragment so it can be unit tested. */
public final class HomeDisplayRules {

    private HomeDisplayRules() {}

    /** Vietnamese names end with the given name: "Nguyễn Vinh Quang" is greeted as "Quang". */
    public static String greetingName(String fullName) {
        String clean = SafeText.clean(fullName).trim();
        if (clean.isEmpty()) return "";
        String[] parts = clean.split("\\s+");
        return parts[parts.length - 1];
    }

    /** Price as on the design's stickers: 65000 -> "65K", 135500 -> "135,5K", 900 -> "900đ". */
    public static String shortPrice(long amount) {
        if (amount < 1000) return amount + "đ";
        long hundreds = Math.round(amount / 100.0);
        long thousands = hundreds / 10;
        long rest = hundreds % 10;
        return rest == 0 ? String.format(Locale.ROOT, "%dK", thousands)
                : String.format(Locale.ROOT, "%d,%dK", thousands, rest);
    }

    /** "Món của bếp hôm nay": the first dish on sale that Admin marked as today's special; null when there is none. */
    public static FoodItem chefPick(List<FoodItem> foods) {
        for (FoodItem food : foods) {
            if (food.isAvailable() && food.isDailySpecial()) return food;
        }
        return null;
    }

    /** Menu order kept, but dishes on sale come before sold-out ones. */
    public static List<FoodItem> onSaleFirst(List<FoodItem> foods) {
        List<FoodItem> sorted = new ArrayList<>(foods);
        Collections.sort(sorted, (a, b) -> a.isAvailable() == b.isAvailable() ? 0 : a.isAvailable() ? -1 : 1);
        return sorted;
    }

    /**
     * "Gọi nhiều nhất": dishes on sale that have been sold, most sold first, at most {@code limit}.
     * Empty when no dish carries a sales count, so the section is hidden rather than showing a made-up ranking.
     */
    public static List<FoodItem> bestSellers(List<FoodItem> foods, int limit) {
        List<FoodItem> sold = new ArrayList<>();
        for (FoodItem food : foods) if (food.isAvailable() && food.getSoldCount() > 0) sold.add(food);
        Collections.sort(sold, (a, b) -> Integer.compare(b.getSoldCount(), a.getSoldCount()));
        return firstN(sold, limit);
    }

    public static <T> List<T> firstN(List<T> values, int limit) {
        return new ArrayList<>(values.subList(0, Math.min(limit, values.size())));
    }
}
