package com.restaurant.viettable.model;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.List;

public class CartItem implements Serializable {
    private String id;
    private FoodItem foodItem;
    private int quantity;
    private String selectedSize;
    private List<String> selectedToppings;
    private int spicyLevel;
    private String note;

    public CartItem() {
        this.selectedToppings = new ArrayList<>();
    }

    public CartItem(FoodItem foodItem, int quantity, String selectedSize,
                    List<String> selectedToppings, int spicyLevel, String note) {
        this.id = java.util.UUID.randomUUID().toString();
        this.foodItem = foodItem;
        this.quantity = quantity;
        this.selectedSize = selectedSize != null ? selectedSize : "Tiêu chuẩn";
        this.selectedToppings = selectedToppings != null ? selectedToppings : new ArrayList<>();
        this.spicyLevel = spicyLevel;
        this.note = note != null ? note : "";
    }

    public String getId() { return id; }
    public FoodItem getFoodItem() { return foodItem; }
    public int getQuantity() { return quantity; }
    public void setQuantity(int quantity) { this.quantity = quantity; }
    public String getSelectedSize() { return selectedSize; }
    public void setSelectedSize(String selectedSize) { this.selectedSize = selectedSize; }
    public List<String> getSelectedToppings() { return selectedToppings; }
    public int getSpicyLevel() { return spicyLevel; }
    public void setSpicyLevel(int spicyLevel) { this.spicyLevel = spicyLevel; }
    public String getNote() { return note; }
    public void setNote(String note) { this.note = note; }

    public long calculateUnitPrice() {
        if (foodItem == null) return 0;
        long unit = foodItem.getPrice();
        if ("Lớn".equalsIgnoreCase(selectedSize)) {
            unit += 15000;
        } else if ("Đặc biệt".equalsIgnoreCase(selectedSize)) {
            unit += 25000;
        }
        if (selectedToppings != null) {
            unit += (selectedToppings.size() * 15000L);
        }
        return unit;
    }

    public long calculateTotalPrice() {
        return calculateUnitPrice() * quantity;
    }

    public String getFormattedTotalPrice() {
        return String.format(java.util.Locale.GERMANY, "%,dđ", calculateTotalPrice());
    }

    public String getOptionsSummary() {
        StringBuilder sb = new StringBuilder();
        if (selectedSize != null && !selectedSize.isEmpty()) {
            sb.append("Size: ").append(selectedSize);
        }
        if (selectedToppings != null && !selectedToppings.isEmpty()) {
            sb.append(" | Topping: ").append(String.join(", ", selectedToppings));
        }
        if (spicyLevel > 0) {
            sb.append(" | Cay: ").append(spicyLevel).append("/3");
        }
        if (note != null && !note.trim().isEmpty()) {
            sb.append(" | ").append(note.trim());
        }
        return sb.toString();
    }
}
