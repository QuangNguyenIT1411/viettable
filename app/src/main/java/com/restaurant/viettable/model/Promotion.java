package com.restaurant.viettable.model;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.List;

/** A running promotion from GET /api/mobile/v1/promotions (docs/PROMO_PLAN.md); the server applies it to the bill. */
public final class Promotion implements Serializable {
    private final int id;
    private final String name;
    private final String description;
    private final String imageUrl;
    private final String discountType;
    private final double discountValue;
    private final long maxDiscount;
    private final long minSubtotal;
    private final List<Integer> branchIds;
    private final String startsAtUtc;
    private final String endsAtUtc;
    private final String summary;

    public Promotion(int id, String name, String description, String imageUrl, String discountType, double discountValue,
                     long maxDiscount, long minSubtotal, List<Integer> branchIds, String startsAtUtc, String endsAtUtc, String summary) {
        this.id = id;
        this.name = name == null ? "" : name;
        this.description = description;
        this.imageUrl = imageUrl;
        this.discountType = discountType == null ? "PERCENT" : discountType;
        this.discountValue = discountValue;
        this.maxDiscount = maxDiscount;
        this.minSubtotal = minSubtotal;
        this.branchIds = branchIds == null ? new ArrayList<>() : branchIds;
        this.startsAtUtc = startsAtUtc;
        this.endsAtUtc = endsAtUtc;
        this.summary = summary == null ? "" : summary;
    }

    public int getId() { return id; }
    public String getName() { return name; }
    public String getDescription() { return description; }
    public String getImageUrl() { return imageUrl; }
    public String getDiscountType() { return discountType; }
    public double getDiscountValue() { return discountValue; }
    public long getMaxDiscount() { return maxDiscount; }
    public long getMinSubtotal() { return minSubtotal; }
    public List<Integer> getBranchIds() { return branchIds; }
    public String getStartsAtUtc() { return startsAtUtc; }
    public String getEndsAtUtc() { return endsAtUtc; }
    /** "Giảm 10% (tối đa 50.000đ) cho hóa đơn từ 300.000đ", written by the server. */
    public String getSummary() { return summary; }
}
