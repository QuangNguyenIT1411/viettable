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

public class ImageLoader {

    private static volatile ImageLoader instance;
    private final LruCache<String, Bitmap> memoryCache;
    private final ExecutorService executor;
    private final Handler mainHandler;
    private File diskCache;
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
        if (targets != null) { targets.add(new WeakReference<>(imageView)); return; }
        targets = new ArrayList<>();
        targets.add(new WeakReference<>(imageView));
        pending.put(url, targets);
        executor.execute(() -> {
            File file = imageFile(url);
            Bitmap downloaded = null;
            if (file != null && file.isFile() && System.currentTimeMillis() - file.lastModified() < 7 * 24 * 60 * 60 * 1000L)
                downloaded = BitmapFactory.decodeFile(file.getAbsolutePath());
            if (downloaded == null) {
                downloaded = downloadBitmap(url);
                if (downloaded != null && file != null) {
                    try {
                        if (diskCache.isDirectory() || diskCache.mkdirs()) {
                            trimDiskCache();
                            try (FileOutputStream out = new FileOutputStream(file)) {
                                downloaded.compress(Bitmap.CompressFormat.JPEG, 85, out);
                            }
                        }
                    } catch (Exception ignored) { /* Network image remains usable if disk is full. */ }
                }
            }
            Bitmap result = downloaded;
            if (downloaded != null) {
                memoryCache.put(url, downloaded);
            }
            mainHandler.post(() -> {
                List<WeakReference<ImageView>> views = pending.remove(url);
                if (views == null || result == null) return;
                for (WeakReference<ImageView> reference : views) {
                    ImageView target = reference.get();
                    if (target != null && url.equals(target.getTag())) target.setImageBitmap(result);
                }
            });
        });
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
        if (files == null || files.length < 64) return;
        java.util.Arrays.sort(files, java.util.Comparator.comparingLong(File::lastModified));
        for (int i = 0; i <= files.length - 64; i++) files[i].delete();
    }

    private Bitmap downloadBitmap(String urlString) {
        HttpURLConnection conn = null;
        InputStream is = null;
        try {
            URL url = new URL(urlString);
            conn = (HttpURLConnection) url.openConnection();
            conn.setConnectTimeout(6000);
            conn.setReadTimeout(BuildConfig.IMAGE_READ_TIMEOUT_MS);
            conn.setInstanceFollowRedirects(true);
            conn.setRequestProperty("User-Agent", "VietTable-Android");
            conn.connect();

            int code = conn.getResponseCode();
            if (code == HttpURLConnection.HTTP_OK) {
                is = conn.getInputStream();
                BitmapFactory.Options options = new BitmapFactory.Options();
                options.inPreferredConfig = Bitmap.Config.RGB_565;
                return BitmapFactory.decodeStream(is, null, options);
            }
        } catch (Throwable ignored) {
        } finally {
            if (is != null) {
                try { is.close(); } catch (Exception ignored) {}
            }
            if (conn != null) {
                try { conn.disconnect(); } catch (Exception ignored) {}
            }
        }
        return null;
    }
}
