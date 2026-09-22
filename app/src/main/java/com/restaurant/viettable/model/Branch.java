package com.restaurant.viettable.model;

import java.io.Serializable;
import java.util.List;

public class Branch implements Serializable {
    private String id;
    private String name;
    private String address;
    private String phone;
    private String openingHours;
    private double distanceKm;
    private boolean isOpen;
    private String description;
    private List<String> facilities;
    private double rating;
    private int reviewCount;
    private double latitude;
    private double longitude;
    private String imageUrl;
    private int imageResId;

    public Branch() {}

    public Branch(String id, String name, String address, String phone, String openingHours,
                  double distanceKm, boolean isOpen, String description, List<String> facilities,
                  double rating, int reviewCount, double latitude, double longitude) {
        this.id = id;
        this.name = name;
        this.address = address;
        this.phone = phone;
        this.openingHours = openingHours;
        this.distanceKm = distanceKm;
        this.isOpen = isOpen;
        this.description = description;
        this.facilities = facilities;
        this.rating = rating;
        this.reviewCount = reviewCount;
        this.latitude = latitude;
        this.longitude = longitude;
    }

    public Branch(String id, String name, String address, String phone, String openingHours,
                  double distanceKm, boolean isOpen, String description, List<String> facilities,
                  double rating, int reviewCount, double latitude, double longitude,
                  String imageUrl, int imageResId) {
        this(id, name, address, phone, openingHours, distanceKm, isOpen, description, facilities, rating, reviewCount, latitude, longitude);
        this.imageUrl = imageUrl;
        this.imageResId = imageResId;
    }

    public String getId() { return id; }
    public String getName() { return name; }
    public String getAddress() { return address; }
    public String getPhone() { return phone; }
    public String getOpeningHours() { return openingHours; }
    public double getDistanceKm() { return distanceKm; }
    public boolean isOpen() { return isOpen; }
    public String getDescription() { return description; }
    public List<String> getFacilities() { return facilities; }
    public double getRating() { return rating; }
    public int getReviewCount() { return reviewCount; }
    public double getLatitude() { return latitude; }
    public double getLongitude() { return longitude; }
    public String getImageUrl() { return imageUrl; }
    public void setImageUrl(String imageUrl) { this.imageUrl = imageUrl; }
    public int getImageResId() { return imageResId; }
    public void setImageResId(int imageResId) { this.imageResId = imageResId; }
}
