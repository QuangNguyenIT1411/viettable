package com.restaurant.viettable.model;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Locale;

/**
 * "Gợi ý món" (owner 06/11): what the server suggested for a group and a budget. Every price and total comes from
 * the server; a swap only changes which dish is on a line, the new total is asked from the server again.
 */
public final class MenuSuggestion {
    public static final class Alternative {
        public final String dishId; public final String name; public final long price; public final boolean vegetarian;
        public Alternative(String dishId, String name, long price, boolean vegetarian) {
            this.dishId = dishId; this.name = name; this.price = price; this.vegetarian = vegetarian;
        }
    }

    public static final class Line {
        public final String dishId; public final String name; public final long price; public final int quantity;
        public final String course; public final String reason; public final boolean forVegetarians;
        public final List<Alternative> alternatives;
        public Line(String dishId, String name, long price, int quantity, String course, String reason,
                    boolean forVegetarians, List<Alternative> alternatives) {
            this.dishId = dishId; this.name = name; this.price = price; this.quantity = quantity; this.course = course;
            this.reason = reason; this.forVegetarians = forVegetarians;
            this.alternatives = alternatives == null ? Collections.emptyList() : Collections.unmodifiableList(alternatives);
        }
        public long lineTotal() { return price * quantity; }
    }

    /** Server totals (bill formula, before discounts). */
    public static final class Totals {
        public final long subtotal; public final double feePercent; public final long fee; public final double vatPercent;
        public final long vat; public final long total;
        public Totals(long subtotal, double feePercent, long fee, double vatPercent, long vat, long total) {
            this.subtotal = subtotal; this.feePercent = feePercent; this.fee = fee; this.vatPercent = vatPercent;
            this.vat = vat; this.total = total;
        }
        public static Totals parse(JSONObject row) {
            return new Totals(row.optLong("subtotal"), row.optDouble("serviceFeePercent", 0), row.optLong("serviceFee"),
                    row.optDouble("vatPercent", 0), row.optLong("vat"), row.optLong("total"));
        }
    }

    public final boolean feasible;
    public final List<Line> lines;
    public final Totals totals;
    public final long budget;
    public final Long minimumTotal;
    public final List<String> notes;

    public MenuSuggestion(boolean feasible, List<Line> lines, Totals totals, long budget, Long minimumTotal, List<String> notes) {
        this.feasible = feasible; this.lines = Collections.unmodifiableList(new ArrayList<>(lines)); this.totals = totals;
        this.budget = budget; this.minimumTotal = minimumTotal;
        this.notes = notes == null ? Collections.emptyList() : Collections.unmodifiableList(notes);
    }

    public static MenuSuggestion parse(JSONObject row) {
        List<Line> lines = new ArrayList<>();
        JSONArray items = row.optJSONArray("items");
        if (items != null) for (int i = 0; i < items.length(); i++) {
            JSONObject x = items.optJSONObject(i);
            if (x == null) continue;
            List<Alternative> alts = new ArrayList<>();
            JSONArray altRows = x.optJSONArray("alternatives");
            if (altRows != null) for (int j = 0; j < altRows.length(); j++) {
                JSONObject a = altRows.optJSONObject(j);
                if (a == null) continue;
                alts.add(new Alternative(String.valueOf(a.optInt("dishId")), a.optString("name"), a.optLong("price"), a.optBoolean("isVegetarian")));
            }
            lines.add(new Line(String.valueOf(x.optInt("dishId")), x.optString("name"), x.optLong("price"), x.optInt("quantity", 1),
                    x.isNull("course") ? null : x.optString("course"), x.optString("reason"), x.optBoolean("forVegetarians"), alts));
        }
        List<String> notes = new ArrayList<>();
        JSONArray noteRows = row.optJSONArray("notes");
        if (noteRows != null) for (int i = 0; i < noteRows.length(); i++) if (!noteRows.isNull(i)) notes.add(noteRows.optString(i));
        Long minimum = row.isNull("minimumTotal") || !row.has("minimumTotal") ? null : row.optLong("minimumTotal");
        return new MenuSuggestion(row.optBoolean("feasible"), lines, Totals.parse(row), row.optLong("budget"), minimum, notes);
    }

    /**
     * Line {@code index} now uses {@code alt}; the dish it replaces becomes an alternative so the guest can swap back.
     * Totals stay the old ones until the server prices the new basket ({@link #withTotals}).
     */
    public MenuSuggestion swap(int index, Alternative alt) {
        if (index < 0 || index >= lines.size() || alt == null) return this;
        Line old = lines.get(index);
        List<Alternative> alts = new ArrayList<>();
        alts.add(new Alternative(old.dishId, old.name, old.price, old.forVegetarians));
        for (Alternative a : old.alternatives) if (!a.dishId.equals(alt.dishId)) alts.add(a);
        List<Line> next = new ArrayList<>(lines);
        next.set(index, new Line(alt.dishId, alt.name, alt.price, old.quantity, old.course, old.reason, old.forVegetarians, alts));
        return new MenuSuggestion(feasible, next, totals, budget, minimumTotal, notes);
    }

    public MenuSuggestion withTotals(Totals priced) {
        return new MenuSuggestion(feasible, lines, priced, budget, minimumTotal, notes);
    }

    /** Body for POST /api/mobile/v1/menu/estimate. */
    public JSONObject estimateBody(String branchId) {
        JSONObject body = new JSONObject();
        try {
            body.put("branchId", Integer.parseInt(branchId));
            JSONArray rows = new JSONArray();
            for (Line l : lines) rows.put(new JSONObject().put("dishId", Integer.parseInt(l.dishId)).put("quantity", l.quantity));
            body.put("items", rows);
        } catch (Exception ignored) { }
        return body;
    }

    public boolean overBudget() { return totals.total > budget; }

    public int dishCount() { int n = 0; for (Line l : lines) n += l.quantity; return n; }

    public static String courseLabel(String course) {
        if (course == null) return "";
        switch (course.toUpperCase(Locale.ROOT)) {
            case "MAIN": return "Món chính";
            case "STARTER": return "Khai vị";
            case "SOUP": return "Canh / súp";
            case "SIDE": return "Món phụ";
            case "DESSERT": return "Tráng miệng";
            case "DRINK": return "Đồ uống";
            default: return "";
        }
    }

    /** "Còn dư 35.000đ" / "Vượt 12.000đ" against the guest's budget. */
    public String budgetLine() {
        long diff = budget - totals.total;
        return diff >= 0 ? "Còn dư " + money(diff) + " so với ngân sách" : "Vượt " + money(-diff) + " so với ngân sách";
    }

    public static String money(long value) { return String.format(Locale.GERMANY, "%,dđ", value); }

    /** Parses what the guest typed: "800000", "800.000", "800k", "1,5tr", "1 triệu". Returns 0 when unreadable. */
    public static long parseBudget(String text) {
        if (text == null) return 0;
        String t = text.trim().toLowerCase(Locale.ROOT).replace(" ", "").replace("đ", "").replace("vnd", "");
        if (t.isEmpty()) return 0;
        long unit = 1;
        if (t.endsWith("triệu")) { unit = 1_000_000; t = t.substring(0, t.length() - 5); }
        else if (t.endsWith("tr")) { unit = 1_000_000; t = t.substring(0, t.length() - 2); }
        else if (t.endsWith("k") || t.endsWith("n")) { unit = 1_000; t = t.substring(0, t.length() - 1); }
        if (t.isEmpty()) return 0;
        try {
            if (unit == 1) return Long.parseLong(t.replace(".", "").replace(",", ""));
            return Math.round(Double.parseDouble(t.replace(",", ".")) * unit);
        } catch (NumberFormatException e) { return 0; }
    }
}
