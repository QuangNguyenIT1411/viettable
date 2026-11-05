package com.restaurant.viettable.data;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

import android.content.Context;
import android.graphics.pdf.PdfRenderer;
import android.os.ParcelFileDescriptor;

import androidx.test.core.app.ApplicationProvider;
import androidx.test.ext.junit.runners.AndroidJUnit4;

import com.restaurant.viettable.model.Receipt;
import com.restaurant.viettable.util.ReceiptPdf;

import org.json.JSONObject;
import org.junit.Test;
import org.junit.runner.RunWith;

import java.io.File;

/** "PHIẾU THANH TOÁN" (owner 05/11): a real PDF, one 80 mm page, from a server snapshot, offline. */
@RunWith(AndroidJUnit4.class)
public class ReceiptPdfTest {
    @Test
    public void receiptPdfIsCreatedAsOneEightyMillimetrePage() throws Exception {
        Context context = ApplicationProvider.getApplicationContext();
        Receipt receipt = Receipt.fromJson(new JSONObject("{\"invoiceId\":44,\"snapshotVersion\":1,\"branchName\":\"Chi nhánh Quận 1\","
                + "\"branchAddress\":\"123 Nguyễn Huệ, Quận 1\",\"tableName\":\"Bàn 6\",\"sessionStartedAtUtc\":\"2026-11-05T12:02:00Z\","
                + "\"paidAtUtc\":\"2026-11-05T13:15:00Z\",\"cashierName\":\"Lan\",\"items\":[{\"dishId\":1,\"dishName\":\"Phở bò tái lăn\","
                + "\"quantity\":2,\"unitPrice\":65000,\"lineTotal\":130000}],\"subtotal\":130000,\"serviceFeePercent\":5,"
                + "\"serviceFeeAmount\":6500,\"vatPercent\":8,\"vatAmount\":10920,\"totalAmount\":147420,\"amountPaid\":147420,"
                + "\"paymentMethod\":\"TRANSFER\",\"bankTransactionRef\":\"FT2611\",\"pointsEarned\":147}"));
        File pdf = ReceiptPdf.render(context, receipt);
        assertTrue(pdf.exists());
        assertTrue(pdf.length() > 1_000);
        try (ParcelFileDescriptor fd = ParcelFileDescriptor.open(pdf, ParcelFileDescriptor.MODE_READ_ONLY);
             PdfRenderer renderer = new PdfRenderer(fd)) {
            assertEquals(1, renderer.getPageCount());
            try (PdfRenderer.Page page = renderer.openPage(0)) {
                assertEquals(ReceiptPdf.PAGE_WIDTH, page.getWidth());
                assertTrue("a full receipt is longer than it is wide", page.getHeight() > page.getWidth());
            }
        }
    }
}
