package com.restaurant.viettable.data;

import com.restaurant.viettable.util.StatusLabels;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class IssuedInvoiceLabelsTest {
    @Test public void branchComesFromBranchListEvenAfterTheSessionClosed() {
        assertEquals("Chi nhánh Quận 1", MobileRepository.issuedBranchName("Chi nhánh Quận 1", null));
        assertEquals("Chi nhánh Quận 1", MobileRepository.issuedBranchName("Chi nhánh Quận 1", "Chi nhánh Gò Vấp"));
    }

    @Test public void branchFallsBackToSessionThenEmptyNeverAppName() {
        assertEquals("Chi nhánh Gò Vấp", MobileRepository.issuedBranchName(null, "Chi nhánh Gò Vấp"));
        assertEquals("", MobileRepository.issuedBranchName("", null));
    }

    @Test public void paymentMethodFromServerIsShownInVietnamese() {
        assertEquals("Tiền mặt", StatusLabels.paymentMethod("CASH"));
        assertEquals("Chuyển khoản", StatusLabels.paymentMethod("TRANSFER"));
        assertEquals("", StatusLabels.paymentMethod(""));
    }
}
