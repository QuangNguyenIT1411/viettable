package com.restaurant.viettable.model;

import java.io.Serializable;

public final class PaymentInfo implements Serializable {
    private final int paymentId;
    private final int checkoutQuoteId;
    private final int diningSessionId;
    private final long amount;
    private final String channel;
    private final String method;
    private final String location;
    private final String status;
    private final String currency;
    private final String providerName;
    private final String providerReference;
    private final String providerTransactionId;
    private final String paymentUrl;
    private final String providerExpiresAtUtc;

    public PaymentInfo(int paymentId, int checkoutQuoteId, int diningSessionId, long amount,
                       String channel, String method, String location, String status, String currency,
                       String providerName, String providerReference, String providerTransactionId,
                       String paymentUrl, String providerExpiresAtUtc) {
        this.paymentId = paymentId;
        this.checkoutQuoteId = checkoutQuoteId;
        this.diningSessionId = diningSessionId;
        this.amount = amount;
        this.channel = channel;
        this.method = method;
        this.location = location;
        this.status = status;
        this.currency = currency;
        this.providerName = providerName;
        this.providerReference = providerReference;
        this.providerTransactionId = providerTransactionId;
        this.paymentUrl = paymentUrl;
        this.providerExpiresAtUtc = providerExpiresAtUtc;
    }

    public int getPaymentId() { return paymentId; }
    public int getCheckoutQuoteId() { return checkoutQuoteId; }
    public int getDiningSessionId() { return diningSessionId; }
    public long getAmount() { return amount; }
    public String getChannel() { return channel; }
    public String getMethod() { return method; }
    public String getLocation() { return location; }
    public String getStatus() { return status; }
    public String getCurrency() { return currency; }
    public String getProviderName() { return providerName; }
    public String getProviderReference() { return providerReference; }
    public String getProviderTransactionId() { return providerTransactionId; }
    public String getPaymentUrl() { return paymentUrl; }
    public String getProviderExpiresAtUtc() { return providerExpiresAtUtc; }
    public boolean hasPaymentUrl() { return paymentUrl != null && !paymentUrl.trim().isEmpty(); }
}
