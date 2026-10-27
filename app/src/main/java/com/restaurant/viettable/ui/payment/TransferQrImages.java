package com.restaurant.viettable.ui.payment;

import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.net.Uri;
import android.os.Build;
import android.provider.MediaStore;
import android.util.Base64;

import org.json.JSONObject;

import java.io.OutputStream;

/** VietQR image from the server (PNG in base64): decode, describe and save to the gallery. */
final class TransferQrImages {
    private TransferQrImages() { }

    static Bitmap decode(String base64) {
        if (base64 == null || base64.isEmpty()) return null;
        try {
            byte[] png = Base64.decode(base64, Base64.DEFAULT);
            return BitmapFactory.decodeByteArray(png, 0, png.length);
        } catch (IllegalArgumentException e) {
            return null;
        }
    }

    /** Amount as bank apps want it pasted: digits only ("245000", not "245.000 đ"). */
    static String amountDigits(double amount) {
        return String.valueOf(Math.max(0, Math.round(amount)));
    }

    /** Puts {@code text} on the clipboard; false when there is nothing to copy. */
    static boolean copy(Context context, String label, String text) {
        if (text == null || text.trim().isEmpty()) return false;
        android.content.ClipboardManager clipboard = (android.content.ClipboardManager)
                context.getSystemService(Context.CLIPBOARD_SERVICE);
        if (clipboard == null) return false;
        clipboard.setPrimaryClip(android.content.ClipData.newPlainText(label, text.trim()));
        return true;
    }

    /** Saves into Pictures/VietTable (Android 10+, no permission needed). Older Android: returns false. */
    static boolean saveToGallery(Context context, Bitmap image, String name) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) return false;
        ContentResolver resolver = context.getContentResolver();
        ContentValues values = new ContentValues();
        values.put(MediaStore.Images.Media.DISPLAY_NAME, name + ".png");
        values.put(MediaStore.Images.Media.MIME_TYPE, "image/png");
        values.put(MediaStore.Images.Media.RELATIVE_PATH, "Pictures/VietTable");
        Uri uri = resolver.insert(MediaStore.Images.Media.EXTERNAL_CONTENT_URI, values);
        if (uri == null) return false;
        try (OutputStream out = resolver.openOutputStream(uri)) {
            return out != null && image.compress(Bitmap.CompressFormat.PNG, 100, out);
        } catch (Exception e) {
            resolver.delete(uri, null, null);
            return false;
        }
    }
}
