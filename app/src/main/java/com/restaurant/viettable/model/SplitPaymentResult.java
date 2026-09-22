package com.restaurant.viettable.model;

public final class SplitPaymentResult {
    private final PaymentInfo payment;
    private final SplitBill splitBill;

    public SplitPaymentResult(PaymentInfo payment, SplitBill splitBill) {
        this.payment = payment;
        this.splitBill = splitBill;
    }

    public PaymentInfo getPayment() { return payment; }
    public SplitBill getSplitBill() { return splitBill; }
}
