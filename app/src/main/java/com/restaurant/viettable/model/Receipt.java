package com.restaurant.viettable.model;

import androidx.annotation.Nullable;

import com.restaurant.viettable.util.SafeText;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/**
 * One paid invoice exactly as the server stored it when it was paid (owner 05/11): dishes, every amount and the
 * service fee / VAT percentages, where, when and who. History cards, the detail screen and the "PHIẾU THANH TOÁN" PDF
 * all read this and never compute an amount themselves. Unknown values stay null so the screens hide those rows.
 */
public final class Receipt implements Serializable {
    public static final class Line implements Serializable {
        public final int dishId;
        public final String name;
        public final int quantity;
        public final long unitPrice;
        public final long lineTotal;

        public Line(int dishId, String name, int quantity, long unitPrice, long lineTotal) {
            this.dishId = dishId;
            this.name = name;
            this.quantity = quantity;
            this.unitPrice = unitPrice;
            this.lineTotal = lineTotal;
        }
    }

    public int invoiceId;
    public String invoiceCode;
    public int diningSessionId;
    public int snapshotVersion;
    public int branchId;
    @Nullable public String branchName;
    @Nullable public String branchAddress;
    @Nullable public String tableName;
    @Nullable public String sessionStartedAtUtc;
    @Nullable public String paidAtUtc;
    @Nullable public String issuedAtUtc;
    @Nullable public String cashierName;
    public List<Line> lines = new ArrayList<>();
    public int itemCount;
    public long subtotal;
    @Nullable public String promotionName;
    public long promotionDiscount;
    @Nullable public String voucherCode;
    public long voucherDiscount;
    /** Counter bills only: the cashier's own discount. */
    public long manualDiscount;
    public int pointsUsed;
    public long pointsDiscount;
    /** Null = an invoice from before service fee / VAT existed (no row shown). */
    @Nullable public Double serviceFeePercent;
    public long serviceFeeAmount;
    @Nullable public Double vatPercent;
    public long vatAmount;
    public long depositApplied;
    public long grossTotal;
    public long totalAmount;
    public long amountPaid;
    public long changeAmount;
    @Nullable public String paymentMethod;
    @Nullable public String paymentStatus;
    @Nullable public String bankTransactionRef;
    @Nullable public Integer pointsEarned;

    public static Receipt fromJson(JSONObject row) {
        Receipt r = new Receipt();
        r.invoiceId = row.optInt("invoiceId");
        r.invoiceCode = text(row, "invoiceCode");
        r.diningSessionId = row.optInt("diningSessionId");
        r.snapshotVersion = row.optInt("snapshotVersion");
        r.branchId = row.optInt("branchId");
        r.branchName = text(row, "branchName");
        r.branchAddress = text(row, "branchAddress");
        r.tableName = text(row, "tableName");
        r.sessionStartedAtUtc = text(row, "sessionStartedAtUtc");
        r.paidAtUtc = text(row, "paidAtUtc");
        r.issuedAtUtc = text(row, "issuedAtUtc");
        r.cashierName = text(row, "cashierName");
        JSONArray items = row.optJSONArray("items");
        if (items != null) for (int i = 0; i < items.length(); i++) {
            JSONObject line = items.optJSONObject(i);
            if (line == null) continue;
            String name = text(line, "dishName");
            r.lines.add(new Line(line.optInt("dishId"), name == null ? "Món" : name, line.optInt("quantity"),
                    line.optLong("unitPrice"), line.optLong("lineTotal")));
        }
        int count = 0;
        for (Line line : r.lines) count += Math.max(0, line.quantity);
        r.itemCount = row.has("itemCount") ? row.optInt("itemCount") : count;
        r.subtotal = row.optLong("subtotal");
        r.promotionName = text(row, "promotionName");
        r.promotionDiscount = row.optLong("promotionDiscount");
        r.voucherCode = text(row, "voucherCode");
        r.voucherDiscount = row.optLong("voucherDiscount");
        r.manualDiscount = row.optLong("manualDiscount");
        r.pointsUsed = row.optInt("pointsUsed");
        r.pointsDiscount = row.optLong("pointsDiscount");
        r.serviceFeePercent = number(row, "serviceFeePercent");
        r.serviceFeeAmount = row.optLong("serviceFeeAmount");
        r.vatPercent = number(row, "vatPercent");
        r.vatAmount = row.optLong("vatAmount");
        r.depositApplied = row.optLong("depositApplied");
        r.totalAmount = row.optLong("totalAmount");
        r.grossTotal = row.has("grossTotal") ? row.optLong("grossTotal") : r.totalAmount + r.depositApplied;
        r.amountPaid = row.has("amountPaid") ? row.optLong("amountPaid") : r.totalAmount;
        r.changeAmount = row.optLong("changeAmount");
        r.paymentMethod = text(row, "paymentMethod");
        r.paymentStatus = text(row, "paymentStatus");
        r.bankTransactionRef = text(row, "bankTransactionRef");
        r.pointsEarned = row.isNull("pointsEarned") || !row.has("pointsEarned") ? null : row.optInt("pointsEarned");
        return r;
    }

    public List<Line> getLines() { return Collections.unmodifiableList(lines); }

    @Nullable
    private static String text(JSONObject row, String key) {
        String value = SafeText.json(row, key, null);
        return value == null || value.trim().isEmpty() ? null : value.trim();
    }

    @Nullable
    private static Double number(JSONObject row, String key) {
        if (!row.has(key) || row.isNull(key)) return null;
        double value = row.optDouble(key, Double.NaN);
        return Double.isNaN(value) ? null : value;
    }
}
