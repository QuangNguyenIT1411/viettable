package com.restaurant.viettable.model;

import java.io.Serializable;

public class User implements Serializable {
    private String id;
    private String fullName;
    private String email;
    private String phone;
    private String memberTier; // Thành viên Bạc, Vàng, Bạch Kim
    private int loyaltyPoints;
    private String defaultBranchId;

    public User() {}

    public User(String id, String fullName, String email, String phone, String memberTier, int loyaltyPoints, String defaultBranchId) {
        this.id = id;
        this.fullName = fullName;
        this.email = email;
        this.phone = phone;
        this.memberTier = memberTier;
        this.loyaltyPoints = loyaltyPoints;
        this.defaultBranchId = defaultBranchId;
    }

    public String getId() { return id; }
    public void setId(String id) { this.id = id; }

    public String getFullName() { return fullName; }
    public void setFullName(String fullName) { this.fullName = fullName; }

    public String getEmail() { return email; }
    public void setEmail(String email) { this.email = email; }

    public String getPhone() { return phone; }
    public void setPhone(String phone) { this.phone = phone; }

    public String getMemberTier() { return memberTier; }
    public String getMembershipTier() { return memberTier; }
    public void setMemberTier(String memberTier) { this.memberTier = memberTier; }

    public int getLoyaltyPoints() { return loyaltyPoints; }
    public void setLoyaltyPoints(int loyaltyPoints) { this.loyaltyPoints = loyaltyPoints; }

    public String getDefaultBranchId() { return defaultBranchId; }
    public void setDefaultBranchId(String defaultBranchId) { this.defaultBranchId = defaultBranchId; }
}
