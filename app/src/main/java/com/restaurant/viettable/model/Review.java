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

    /** Stars one dish of the invoice got in this review (owner 05/11). */
    public static final class DishRating implements Serializable {
        public final int dishId;
        public final String dishName;
        public final int rating;

        public DishRating(int dishId, String dishName, int rating) {
            this.dishId = dishId;
            this.dishName = dishName;
            this.rating = rating;
        }
    }

    // Filled from the server's review (owner 05/11); defaults describe a review from before the upgrade.
    private final ArrayList<String> tags = new ArrayList<>();
    private final ArrayList<DishRating> dishRatings = new ArrayList<>();
    private boolean anonymous;
    private boolean canEdit;
    private String editableUntilUtc;
    private String replyText;
    private String repliedAtUtc;
    private boolean hidden;
    private String hiddenReason;

    public List<String> getTags() { return Collections.unmodifiableList(tags); }
    public List<DishRating> getDishRatings() { return Collections.unmodifiableList(dishRatings); }
    public boolean isAnonymous() { return anonymous; }
    /** True for 24 hours after the review was sent (server's clock). */
    public boolean canEdit() { return canEdit; }
    public String getEditableUntilUtc() { return editableUntilUtc; }
    public String getReplyText() { return replyText; }
    public String getRepliedAtUtc() { return repliedAtUtc; }
    public boolean isHidden() { return hidden; }
    public String getHiddenReason() { return hiddenReason; }

    public void setDetails(List<String> tags, List<DishRating> dishRatings, boolean anonymous, boolean canEdit,
                           String editableUntilUtc, String replyText, String repliedAtUtc, boolean hidden, String hiddenReason) {
        this.tags.clear();
        if (tags != null) this.tags.addAll(tags);
        this.dishRatings.clear();
        if (dishRatings != null) this.dishRatings.addAll(dishRatings);
        this.anonymous = anonymous;
        this.canEdit = canEdit;
        this.editableUntilUtc = editableUntilUtc;
        this.replyText = replyText;
        this.repliedAtUtc = repliedAtUtc;
        this.hidden = hidden;
        this.hiddenReason = hiddenReason;
    }
}
