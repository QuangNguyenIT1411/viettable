package com.restaurant.viettable.model;

import androidx.annotation.Nullable;

import java.util.Collections;
import java.util.List;

/** GET /api/mobile/v1/me/loyalty: the customer's real points, tier and the next tier's threshold. */
public class LoyaltyInfo {
    private final int availablePoints;
    private final String tierCode;
    private final String tierName;
    @Nullable private final String nextTierCode;
    @Nullable private final Integer nextTierAtPoints;
    private final int pointsToNextTier;
    private final int progressPercent;
    private final String cardNumber;
    private final List<String> benefits;

    public LoyaltyInfo(int availablePoints, String tierCode, String tierName, @Nullable String nextTierCode,
                       @Nullable Integer nextTierAtPoints, int pointsToNextTier, int progressPercent) {
        this(availablePoints, tierCode, tierName, nextTierCode, nextTierAtPoints, pointsToNextTier, progressPercent,
                "", Collections.emptyList());
    }

    public LoyaltyInfo(int availablePoints, String tierCode, String tierName, @Nullable String nextTierCode,
                       @Nullable Integer nextTierAtPoints, int pointsToNextTier, int progressPercent,
                       String cardNumber, List<String> benefits) {
        this.availablePoints = availablePoints;
        this.tierCode = tierCode;
        this.tierName = tierName;
        this.nextTierCode = nextTierCode;
        this.nextTierAtPoints = nextTierAtPoints;
        this.pointsToNextTier = pointsToNextTier;
        this.progressPercent = progressPercent;
        this.cardNumber = cardNumber;
        this.benefits = benefits;
    }

    public int getAvailablePoints() { return availablePoints; }
    public String getTierCode() { return tierCode; }
    public String getTierName() { return tierName; }
    @Nullable public String getNextTierCode() { return nextTierCode; }
    @Nullable public Integer getNextTierAtPoints() { return nextTierAtPoints; }
    public int getPointsToNextTier() { return pointsToNextTier; }
    public int getProgressPercent() { return progressPercent; }
    /** The member card number the server issued ("VT-000123"); "" when missing. */
    public String getCardNumber() { return cardNumber; }
    /** What this tier gives, as the server lists it. */
    public List<String> getBenefits() { return benefits; }
}
