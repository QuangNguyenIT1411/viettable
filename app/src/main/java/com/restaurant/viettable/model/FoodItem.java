package com.restaurant.viettable.model;

import java.io.Serializable;
import java.util.List;

public class FoodItem implements Serializable {
    private String id;
    private String categoryId;
    private String branchId;
    private String name;
    private long price;
    private double rating;
    private int soldCount;
    private String description;
    private String ingredients;
    private String allergyInfo;
    private String nutrition;
    private int spicyLevel; // 0: không cay, 1: cay nhẹ, 2: vừa, 3: cay nồng
    private boolean isAvailable;
    private boolean isFavorite;
    private List<String> availableSizes;
    private List<String> availableToppings;
    private String imageUrl;
    private int imageResId;
    private boolean dailySpecial;

    public FoodItem() {}

    public FoodItem(String id, String categoryId, String branchId, String name, long price,
                    double rating, int soldCount, String description, String ingredients,
                    String allergyInfo, String nutrition, int spicyLevel, boolean isAvailable,
                    boolean isFavorite, List<String> availableSizes, List<String> availableToppings) {
        this(id, categoryId, branchId, name, price, rating, soldCount, description, ingredients,
                allergyInfo, nutrition, spicyLevel, isAvailable, isFavorite, availableSizes, availableToppings,
                null, 0);
    }

    public FoodItem(String id, String categoryId, String branchId, String name, long price,
                    double rating, int soldCount, String description, String ingredients,
                    String allergyInfo, String nutrition, int spicyLevel, boolean isAvailable,
                    boolean isFavorite, List<String> availableSizes, List<String> availableToppings,
                    String imageUrl, int imageResId) {
        this.id = id;
        this.categoryId = categoryId;
        this.branchId = branchId;
        this.name = name;
        this.price = price;
        this.rating = rating;
        this.soldCount = soldCount;
        this.description = description;
        this.ingredients = ingredients;
        this.allergyInfo = allergyInfo;
        this.nutrition = nutrition;
        this.spicyLevel = spicyLevel;
        this.isAvailable = isAvailable;
        this.isFavorite = isFavorite;
        this.availableSizes = availableSizes;
        this.availableToppings = availableToppings;
        this.imageUrl = imageUrl;
        this.imageResId = imageResId;
    }

    public String getId() { return id; }
    public String getCategoryId() { return categoryId; }
    public String getBranchId() { return branchId; }
    public String getName() { return name; }
    public long getPrice() { return price; }
    public double getRating() { return rating; }
    public int getSoldCount() { return soldCount; }
    public String getDescription() { return description; }
    public String getIngredients() { return ingredients; }
    public String getAllergyInfo() { return allergyInfo; }
    public String getNutrition() { return nutrition; }
    public int getSpicyLevel() { return spicyLevel; }
    public boolean isAvailable() { return isAvailable; }
    public boolean isFavorite() { return isFavorite; }
    public void setFavorite(boolean favorite) { isFavorite = favorite; }
    public List<String> getAvailableSizes() { return availableSizes; }
    public List<String> getAvailableToppings() { return availableToppings; }
    public String getImageUrl() { return imageUrl; }
    public void setImageUrl(String imageUrl) { this.imageUrl = imageUrl; }
    public int getImageResId() { return imageResId; }
    public void setImageResId(int imageResId) { this.imageResId = imageResId; }
    /** Marked "món đặc biệt trong ngày" in Admin; Trang chủ shows it as "Món của bếp". */
    public boolean isDailySpecial() { return dailySpecial; }
    public void setDailySpecial(boolean dailySpecial) { this.dailySpecial = dailySpecial; }

    public String getFormattedPrice() {
        return String.format(java.util.Locale.GERMANY, "%,dđ", price);
    }
}
