package com.restaurant.viettable.util;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.os.Handler;
import android.os.Looper;
import android.util.LruCache;
import android.widget.ImageView;

import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.FoodItem;

import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class ImageLoader {

    private static volatile ImageLoader instance;
    private final LruCache<String, Bitmap> memoryCache;
    private final ExecutorService executor;
    private final Handler mainHandler;

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

        executor.execute(() -> {
            Bitmap downloaded = downloadBitmap(url);
            if (downloaded != null) {
                memoryCache.put(url, downloaded);
                mainHandler.post(() -> {
                    Object tag = imageView.getTag();
                    if (url.equals(tag)) {
                        imageView.setImageBitmap(downloaded);
                    }
                });
            }
        });
    }

    private Bitmap downloadBitmap(String urlString) {
        HttpURLConnection conn = null;
        InputStream is = null;
        try {
            URL url = new URL(urlString);
            conn = (HttpURLConnection) url.openConnection();
            conn.setConnectTimeout(6000);
            conn.setReadTimeout(6000);
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
