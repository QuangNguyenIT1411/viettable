package com.restaurant.viettable.util;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.os.Handler;
import android.os.Looper;
import android.util.LruCache;
import android.widget.ImageView;

import com.restaurant.viettable.R;
import com.restaurant.viettable.BuildConfig;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.FoodItem;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.io.File;
import java.io.FileOutputStream;
import java.lang.ref.WeakReference;
import java.security.MessageDigest;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Photos of dishes and branches. Owner 07/11 ("Xem tất cả" showed photos slowly): the menu photos are fetched in the
 * background as soon as the menu arrives ({@link #prefetch}), shrunk to {@link #MAX_SIDE_PX} (originals are up to
 * 580 KB) and kept on disk (up to {@link #DISK_CACHE_FILES} files, 7 days), so lists open with them already there.
 */
public class ImageLoader {

    /** Photos are kept at most this many pixels on the long side: list cells are far smaller than the originals. */
    static final int MAX_SIDE_PX = 720;
    static final int DISK_CACHE_FILES = 400;
    private static final long DISK_FRESH_MS = 7 * 24 * 60 * 60 * 1000L;

    private static volatile ImageLoader instance;
    private final LruCache<String, Bitmap> memoryCache;
    private final ExecutorService executor;
    /** Background warm-up, separate so photos on screen are never queued behind it. */
    private final ExecutorService prefetcher;
    private final Handler mainHandler;
    private File diskCache;
    /** Main thread only: url -> views waiting for it (an empty list = a prefetch in flight). */
    private final Map<String, List<WeakReference<ImageView>>> pending = new HashMap<>();

    public void initialize(android.content.Context context) {
        diskCache = new File(context.getApplicationContext().getCacheDir(), "public_images_v1");
    }

    private ImageLoader() {
        int maxMemory = (int) (Runtime.getRuntime().maxMemory() / 1024);
        int cacheSize = maxMemory / 8;
        memoryCache = new LruCache<String, Bitmap>(cacheSize) {
            @Override
            protected int sizeOf(String key, Bitmap bitmap) {
                return bitmap.getByteCount() / 1024;
            }
        };
        executor = Executors.newFixedThreadPool(4);
        prefetcher = Executors.newFixedThreadPool(3);
        mainHandler = new Handler(Looper.getMainLooper());
    }

    public static ImageLoader getInstance() {
        if (instance == null) {
            synchronized (ImageLoader.class) {
                if (instance == null) {
                    instance = new ImageLoader();
                }
            }
        }
        return instance;
    }

    public void load(FoodItem item, ImageView imageView) {
        if (item == null || imageView == null) return;
        load(item.getImageUrl(), item.getImageResId(), imageView);
    }

    public void load(Branch branch, ImageView imageView) {
        if (branch == null || imageView == null) return;
        load(branch.getImageUrl(), branch.getImageResId(), imageView);
    }

    public void load(String url, int fallbackResId, ImageView imageView) {
        if (imageView == null) return;
        imageView.setTag(null);
        imageView.setImageDrawable(null);
        imageView.setBackgroundResource(R.drawable.bg_food_hero);

        if (url == null || url.trim().isEmpty()) {
            if (fallbackResId != 0) {
                imageView.setImageResource(fallbackResId);
            }
            return;
        }

        Bitmap cached = memoryCache.get(url);
        if (cached != null) {
            imageView.setImageBitmap(cached);
            return;
        }

        if (fallbackResId != 0) {
            imageView.setImageResource(fallbackResId);
        }

        imageView.setTag(url);
        List<WeakReference<ImageView>> targets = pending.get(url);
        if (targets != null) { targets.add(new WeakReference<>(imageView)); return; } // joins a load or prefetch in flight
        targets = new ArrayList<>();
        targets.add(new WeakReference<>(imageView));
        pending.put(url, targets);
        executor.execute(() -> deliver(url, fetch(url)));
    }

    /** Fetches the photos in the background (disk first, then network) so the screens that show them open at once. */
    public void prefetch(List<String> urls) {
        if (urls == null || urls.isEmpty()) return;
        List<String> copy = new ArrayList<>(urls);
        mainHandler.post(() -> {
            for (String url : copy) {
                if (url == null || url.trim().isEmpty() || memoryCache.get(url) != null || pending.containsKey(url)) continue;
                pending.put(url, new ArrayList<>());
                prefetcher.execute(() -> deliver(url, fetch(url)));
            }
        });
    }

    private void deliver(String url, Bitmap result) {
        if (result != null) memoryCache.put(url, result);
        mainHandler.post(() -> {
            List<WeakReference<ImageView>> views = pending.remove(url);
            if (views == null || result == null) return;
            for (WeakReference<ImageView> reference : views) {
                ImageView target = reference.get();
                if (target != null && url.equals(target.getTag())) target.setImageBitmap(result);
            }
        });
    }

    /** Disk copy when fresh, else download, shrink and keep the small copy on disk. */
    private Bitmap fetch(String url) {
        File file = imageFile(url);
        if (file != null && file.isFile() && System.currentTimeMillis() - file.lastModified() < DISK_FRESH_MS) {
            Bitmap fromDisk = decode(null, file);
            if (fromDisk != null) return fromDisk;
        }
        byte[] bytes = downloadBytes(url);
        if (bytes == null) return null;
        Bitmap bitmap = decode(bytes, null);
        if (bitmap != null && file != null) {
            try {
                if (diskCache.isDirectory() || diskCache.mkdirs()) {
                    trimDiskCache();
                    try (FileOutputStream out = new FileOutputStream(file)) {
                        bitmap.compress(Bitmap.CompressFormat.JPEG, 85, out);
                    }
                }
            } catch (Exception ignored) { /* Network image remains usable if disk is full. */ }
        }
        return bitmap;
    }

    /** Decodes no larger than needed: big originals decode slowly and crowd the memory cache. */
    private static Bitmap decode(byte[] bytes, File file) {
        try {
            BitmapFactory.Options bounds = new BitmapFactory.Options();
            bounds.inJustDecodeBounds = true;
            if (bytes != null) BitmapFactory.decodeByteArray(bytes, 0, bytes.length, bounds);
            else BitmapFactory.decodeFile(file.getAbsolutePath(), bounds);
            BitmapFactory.Options options = new BitmapFactory.Options();
            options.inPreferredConfig = Bitmap.Config.RGB_565;
            options.inSampleSize = sampleSize(bounds.outWidth, bounds.outHeight, MAX_SIDE_PX);
            Bitmap decoded = bytes != null ? BitmapFactory.decodeByteArray(bytes, 0, bytes.length, options)
                    : BitmapFactory.decodeFile(file.getAbsolutePath(), options);
            if (decoded == null) return null;
            int[] size = scaledSize(decoded.getWidth(), decoded.getHeight(), MAX_SIDE_PX);
            if (size[0] == decoded.getWidth() && size[1] == decoded.getHeight()) return decoded;
            Bitmap scaled = Bitmap.createScaledBitmap(decoded, size[0], size[1], true);
            if (scaled != decoded) decoded.recycle();
            return scaled;
        } catch (Throwable error) {
            return null;
        }
    }

    /** Largest power of two that keeps the long side at or above maxSide (a cheap first shrink while decoding). */
    static int sampleSize(int width, int height, int maxSide) {
        int side = Math.max(width, height);
        int sample = 1;
        while (side > 0 && side / (sample * 2) >= maxSide) sample *= 2;
        return sample;
    }

    /** Width and height with the long side at most maxSide, keeping the shape. */
    static int[] scaledSize(int width, int height, int maxSide) {
        int side = Math.max(width, height);
        if (side <= maxSide || side <= 0) return new int[]{width, height};
        double scale = maxSide / (double) side;
        return new int[]{Math.max(1, (int) Math.round(width * scale)), Math.max(1, (int) Math.round(height * scale))};
    }

    private File imageFile(String url) {
        if (diskCache == null) return null;
        try {
            StringBuilder name = new StringBuilder();
            for (byte b : MessageDigest.getInstance("SHA-256").digest(url.getBytes(StandardCharsets.UTF_8)))
                name.append(String.format(java.util.Locale.ROOT, "%02x", b));
            return new File(diskCache, name + ".jpg");
        } catch (Exception ignored) { return null; }
    }

    private synchronized void trimDiskCache() {
        File[] files = diskCache.listFiles((dir, name) -> name.matches("[a-f0-9]{64}\\.jpg"));
        if (files == null || files.length < DISK_CACHE_FILES) return;
        java.util.Arrays.sort(files, java.util.Comparator.comparingLong(File::lastModified));
        for (int i = 0; i <= files.length - DISK_CACHE_FILES; i++) files[i].delete();
    }

    private byte[] downloadBytes(String urlString) {
        HttpURLConnection conn = null;
        try {
            URL url = new URL(urlString);
            conn = (HttpURLConnection) url.openConnection();
            conn.setConnectTimeout(6000);
            conn.setReadTimeout(BuildConfig.IMAGE_READ_TIMEOUT_MS);
            conn.setInstanceFollowRedirects(true);
            conn.setRequestProperty("User-Agent", "VietTable-Android");
            conn.connect();
            if (conn.getResponseCode() != HttpURLConnection.HTTP_OK) return null;
            try (InputStream is = conn.getInputStream(); ByteArrayOutputStream out = new ByteArrayOutputStream()) {
                byte[] buffer = new byte[16 * 1024];
                int n;
                while ((n = is.read(buffer)) != -1) out.write(buffer, 0, n);
                return out.toByteArray();
            }
        } catch (Throwable ignored) {
            return null;
        } finally {
            if (conn != null) {
                try { conn.disconnect(); } catch (Exception ignored) {}
            }
        }
    }
}
