package com.restaurant.viettable.model;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public final class Review implements Serializable {
    private final int reviewId;
    private final int invoiceId;
    private final int diningSessionId;
    private final int overallRating;
    private final int foodRating;
    private final int serviceRating;
    private final int spaceRating;
    private final String comment;
    private final ArrayList<String> imageUrls;
    private final String createdAtUtc;
    private final String updatedAtUtc;

    public Review(int reviewId, int invoiceId, int diningSessionId, int overallRating,
                  int foodRating, int serviceRating, int spaceRating, String comment,
                  List<String> imageUrls, String createdAtUtc, String updatedAtUtc) {
        this.reviewId = reviewId;
        this.invoiceId = invoiceId;
        this.diningSessionId = diningSessionId;
        this.overallRating = overallRating;
        this.foodRating = foodRating;
        this.serviceRating = serviceRating;
        this.spaceRating = spaceRating;
        this.comment = comment;
        this.imageUrls = new ArrayList<>(imageUrls == null ? Collections.emptyList() : imageUrls);
        this.createdAtUtc = createdAtUtc;
        this.updatedAtUtc = updatedAtUtc;
    }

    public int getReviewId() { return reviewId; }
    public int getInvoiceId() { return invoiceId; }
    public int getDiningSessionId() { return diningSessionId; }
    public int getOverallRating() { return overallRating; }
    public int getFoodRating() { return foodRating; }
    public int getServiceRating() { return serviceRating; }
    public int getSpaceRating() { return spaceRating; }
    public String getComment() { return comment; }
    public List<String> getImageUrls() { return Collections.unmodifiableList(imageUrls); }
    public String getCreatedAtUtc() { return createdAtUtc; }
    public String getUpdatedAtUtc() { return updatedAtUtc; }
}
