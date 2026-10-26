package com.restaurant.viettable.ui.payment;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

public class TransferQrCopyTest {
    @Test
    public void amountIsCopiedAsPlainDigits() {
        assertEquals("245000", TransferQrImages.amountDigits(245000.0));
        assertEquals("245001", TransferQrImages.amountDigits(245000.6));
        assertEquals("0", TransferQrImages.amountDigits(-5));
    }
}
