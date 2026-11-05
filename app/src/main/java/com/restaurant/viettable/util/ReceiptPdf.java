package com.restaurant.viettable.util;

import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.graphics.pdf.PdfDocument;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.provider.MediaStore;
import android.text.Layout;
import android.text.StaticLayout;
import android.text.TextPaint;

import androidx.annotation.Nullable;
import androidx.core.content.FileProvider;
import androidx.core.content.res.ResourcesCompat;

import com.restaurant.viettable.R;
import com.restaurant.viettable.model.Receipt;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.List;

/**
 * "PHIẾU THANH TOÁN" as a PDF (owner 05/11): 80 mm wide like a thermal receipt (one long page), the app's own
 * Vietnamese-capable fonts, every line from the server's snapshot ({@link Receipt} + {@link ReceiptText}). Never
 * "Hoá đơn điện tử" or "Hoá đơn GTGT": the second line says "Không phải hoá đơn GTGT".
 */
public final class ReceiptPdf {
    private ReceiptPdf() { }

    /** 80 mm in PDF points (1 pt = 1/72 inch). */
    public static final int PAGE_WIDTH = 227;
    private static final int MARGIN = 12;
    private static final int CONTENT = PAGE_WIDTH - 2 * MARGIN;

    /** Draws the receipt into a new PDF file in the app's cache; the caller saves or shares it. */
    public static File render(Context context, Receipt r) throws IOException {
        Typeface regular = font(context, R.font.bricolage_regular, Typeface.DEFAULT);
        Typeface bold = font(context, R.font.bricolage_bold, Typeface.DEFAULT_BOLD);
        Typeface title = font(context, R.font.unbounded_bold, Typeface.DEFAULT_BOLD);

        // First pass measures the height so the page is exactly as long as the receipt.
        int height = draw(null, r, regular, bold, title) + MARGIN;
        PdfDocument document = new PdfDocument();
        PdfDocument.Page page = document.startPage(new PdfDocument.PageInfo.Builder(PAGE_WIDTH, height, 1).create());
        page.getCanvas().drawColor(Color.WHITE);
        draw(page.getCanvas(), r, regular, bold, title);
        document.finishPage(page);
        File dir = new File(context.getCacheDir(), "receipts");
        if (!dir.exists() && !dir.mkdirs()) throw new IOException("Không tạo được thư mục tạm.");
        File file = new File(dir, ReceiptText.fileName(r));
        try (FileOutputStream out = new FileOutputStream(file)) {
            document.writeTo(out);
        } finally {
            document.close();
        }
        return file;
    }

    /** Copies the PDF into Downloads (Android 10+: MediaStore; older: the app's own Downloads folder). */
    @Nullable
    public static Uri saveToDownloads(Context context, File pdf) throws IOException {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            ContentResolver resolver = context.getContentResolver();
            ContentValues values = new ContentValues();
            values.put(MediaStore.Downloads.DISPLAY_NAME, pdf.getName());
            values.put(MediaStore.Downloads.MIME_TYPE, "application/pdf");
            values.put(MediaStore.Downloads.RELATIVE_PATH, Environment.DIRECTORY_DOWNLOADS + "/VietTable");
            values.put(MediaStore.Downloads.IS_PENDING, 1);
            Uri uri = resolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values);
            if (uri == null) throw new IOException("Không lưu được vào Tải xuống.");
            try (OutputStream out = resolver.openOutputStream(uri); InputStream in = new FileInputStream(pdf)) {
                if (out == null) throw new IOException("Không lưu được vào Tải xuống.");
                copy(in, out);
            }
            values.clear();
            values.put(MediaStore.Downloads.IS_PENDING, 0);
            resolver.update(uri, values, null, null);
            return uri;
        }
        File dir = context.getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS);
        if (dir == null) return null;
        File target = new File(dir, pdf.getName());
        try (InputStream in = new FileInputStream(pdf); OutputStream out = new FileOutputStream(target)) {
            copy(in, out);
        }
        return FileProvider.getUriForFile(context, context.getPackageName() + ".files", target);
    }

    /** A content:// link other apps (Zalo, email…) may read for sharing. */
    public static Uri shareUri(Context context, File pdf) {
        return FileProvider.getUriForFile(context, context.getPackageName() + ".files", pdf);
    }

    // ---- drawing ----

    private static int draw(@Nullable Canvas canvas, Receipt r, Typeface regular, Typeface bold, Typeface titleFace) {
        TextPaint body = paint(regular, 8.5f, Color.BLACK);
        TextPaint strong = paint(bold, 8.5f, Color.BLACK);
        TextPaint small = paint(regular, 7f, Color.DKGRAY);
        TextPaint title = paint(titleFace, 11f, Color.BLACK);
        TextPaint total = paint(bold, 10f, Color.BLACK);
        int y = MARGIN;

        y = centered(canvas, ReceiptText.TITLE, title, y);
        y = centered(canvas, ReceiptText.NOT_VAT_INVOICE, small, y + 1);
        y += 6;
        if (r.branchName != null) y = centered(canvas, r.branchName, strong, y);
        if (r.branchAddress != null) y = centered(canvas, r.branchAddress, small, y);
        y = rule(canvas, y + 4);

        y = pair(canvas, "Mã hoá đơn", r.invoiceId > 0 ? "#" + r.invoiceId : (r.invoiceCode == null ? "" : r.invoiceCode), body, strong, y);
        if (r.tableName != null) y = pair(canvas, "Bàn", r.tableName, body, body, y);
        String time = ReceiptText.timeRange(r.sessionStartedAtUtc, r.paidAtUtc != null ? r.paidAtUtc : r.issuedAtUtc);
        if (!time.isEmpty()) y = pair(canvas, "Giờ vào – thanh toán", time, body, body, y);
        if (r.cashierName != null) y = pair(canvas, "Thu ngân", r.cashierName, body, body, y);
        y = rule(canvas, y + 2);

        if (r.lines.isEmpty()) {
            y = block(canvas, "Hoá đơn này không lưu danh sách món.", small, MARGIN, CONTENT, Layout.Alignment.ALIGN_NORMAL, y);
        } else {
            for (Receipt.Line line : r.lines) {
                y = block(canvas, line.name, strong, MARGIN, CONTENT, Layout.Alignment.ALIGN_NORMAL, y);
                y = pair(canvas, line.quantity + " × " + ReceiptText.money(line.unitPrice), ReceiptText.money(line.lineTotal), small, body, y);
                y += 2;
            }
        }
        y = rule(canvas, y + 2);

        List<ReceiptText.Row> amounts = ReceiptText.amountRows(r);
        for (ReceiptText.Row row : amounts) {
            TextPaint left = row.kind == ReceiptText.Kind.TOTAL ? total : body;
            TextPaint right = row.kind == ReceiptText.Kind.TOTAL ? total : row.kind == ReceiptText.Kind.PAID ? strong : body;
            y = pair(canvas, row.label, row.value, left, right, y);
        }
        List<ReceiptText.Row> payment = ReceiptText.paymentRows(r);
        if (!payment.isEmpty()) {
            y = rule(canvas, y + 2);
            for (ReceiptText.Row row : payment) y = pair(canvas, row.label, row.value, body, body, y);
        }
        y = rule(canvas, y + 2);
        y = centered(canvas, "Cảm ơn quý khách — hẹn gặp lại!", small, y + 2);
        return y;
    }

    private static TextPaint paint(Typeface face, float size, int color) {
        TextPaint p = new TextPaint(Paint.ANTI_ALIAS_FLAG);
        p.setTypeface(face);
        p.setTextSize(size);
        p.setColor(color);
        return p;
    }

    private static int centered(@Nullable Canvas canvas, String text, TextPaint paint, int y) {
        return block(canvas, text, paint, MARGIN, CONTENT, Layout.Alignment.ALIGN_CENTER, y);
    }

    /** Label on the left (wraps), value on the right; returns the y under the taller of the two. */
    private static int pair(@Nullable Canvas canvas, String label, String value, TextPaint left, TextPaint right, int y) {
        int valueWidth = Math.min(CONTENT / 2, (int) Math.ceil(right.measureText(value)) + 2);
        int labelWidth = CONTENT - valueWidth - 6;
        int a = block(canvas, label, left, MARGIN, labelWidth, Layout.Alignment.ALIGN_NORMAL, y);
        int b = block(canvas, value, right, MARGIN + CONTENT - valueWidth, valueWidth, Layout.Alignment.ALIGN_OPPOSITE, y);
        return Math.max(a, b) + 1;
    }

    private static int block(@Nullable Canvas canvas, String text, TextPaint paint, int x, int width, Layout.Alignment align, int y) {
        StaticLayout layout = StaticLayout.Builder.obtain(text, 0, text.length(), paint, Math.max(10, width))
                .setAlignment(align).setIncludePad(false).build();
        if (canvas != null) {
            canvas.save();
            canvas.translate(x, y);
            layout.draw(canvas);
            canvas.restore();
        }
        return y + layout.getHeight();
    }

    private static int rule(@Nullable Canvas canvas, int y) {
        if (canvas != null) {
            Paint dash = new Paint(Paint.ANTI_ALIAS_FLAG);
            dash.setColor(Color.GRAY);
            dash.setStrokeWidth(0.6f);
            for (float x = MARGIN; x < PAGE_WIDTH - MARGIN; x += 4) canvas.drawLine(x, y, Math.min(x + 2, PAGE_WIDTH - MARGIN), y, dash);
        }
        return y + 5;
    }

    private static Typeface font(Context context, int id, Typeface fallback) {
        try {
            Typeface face = ResourcesCompat.getFont(context, id);
            return face == null ? fallback : face;
        } catch (Exception ignored) {
            return fallback;
        }
    }

    private static void copy(InputStream in, OutputStream out) throws IOException {
        byte[] buffer = new byte[8192];
        int read;
        while ((read = in.read(buffer)) > 0) out.write(buffer, 0, read);
    }
}
