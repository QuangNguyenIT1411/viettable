package com.restaurant.viettable.model;

import java.io.Serializable;

public class Category implements Serializable {
    private String id;
    private String name;
    private int iconRes;
    private int dishCount;

    public Category() {}

    public Category(String id, String name, int iconRes, int dishCount) {
        this.id = id;
        this.name = name;
        this.iconRes = iconRes;
        this.dishCount = dishCount;
    }

    public String getId() { return id; }
    public String getName() { return name; }
    public int getIconRes() { return iconRes; }
    public int getDishCount() { return dishCount; }
}
