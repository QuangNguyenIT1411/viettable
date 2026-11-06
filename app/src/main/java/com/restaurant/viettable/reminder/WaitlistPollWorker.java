package com.restaurant.viettable.reminder;

import android.Manifest;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.os.Build;

import androidx.annotation.NonNull;
import androidx.core.app.NotificationCompat;
import androidx.core.app.NotificationManagerCompat;
import androidx.core.content.ContextCompat;
import androidx.work.Constraints;
import androidx.work.ExistingPeriodicWorkPolicy;
import androidx.work.NetworkType;
import androidx.work.PeriodicWorkRequest;
import androidx.work.WorkManager;
import androidx.work.Worker;
import androidx.work.WorkerParameters;

import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.WaitlistEntry;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.util.WaitlistText;

import java.time.ZoneId;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

/**
 * The app has no Firebase push (owner 06/11): while the guest waits for a table this worker reads the waitlist every
 * 15 minutes (the read also lets the server offer freed tables) and shows "Có bàn cho bạn" once per offer. It stops
 * itself when nothing is waiting any more. With the app open the inbox and the waitlist screen show it at once.
 */
public final class WaitlistPollWorker extends Worker {
    public static final String EXTRA_OPEN_WAITLIST = "com.restaurant.viettable.extra.OPEN_WAITLIST";
    static final String WORK = "waitlist-poll";
    private static final String PREFS = "waitlist_poll";
    private static final String KEY_NOTIFIED = "notified";

    public WaitlistPollWorker(@NonNull Context context, @NonNull WorkerParameters params) { super(context, params); }

    /** Starts the 15-minute check (kept if already running). */
    public static void ensureScheduled(Context context) {
        PeriodicWorkRequest request = new PeriodicWorkRequest.Builder(WaitlistPollWorker.class, 15, TimeUnit.MINUTES)
                .setConstraints(new Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build())
                .addTag(ReservationReminders.TAG)
                .build();
        WorkManager.getInstance(context).enqueueUniquePeriodicWork(WORK, ExistingPeriodicWorkPolicy.KEEP, request);
    }

    public static void stop(Context context) {
        WorkManager.getInstance(context).cancelUniqueWork(WORK);
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().clear().apply();
    }

    @NonNull
    @Override
    public Result doWork() {
        if (!MobileRepository.getInstance().hasSession()) { stop(getApplicationContext()); return Result.success(); }
        CountDownLatch done = new CountDownLatch(1);
        AtomicReference<List<WaitlistEntry>> result = new AtomicReference<>();
        MobileRepository.getInstance().loadWaitlist(new MobileRepository.Callback<List<WaitlistEntry>>() {
            @Override public void onSuccess(List<WaitlistEntry> value) { result.set(value); done.countDown(); }
            @Override public void onError(ApiException error) { done.countDown(); }
        });
        try { if (!done.await(90, TimeUnit.SECONDS)) return Result.retry(); }
        catch (InterruptedException e) { Thread.currentThread().interrupt(); return Result.retry(); }
        List<WaitlistEntry> entries = result.get();
        if (entries == null) return Result.retry();
        handle(getApplicationContext(), entries);
        return Result.success();
    }

    /** Notifies each new offer once; stops polling when nothing is active. Also called by the waitlist screen. */
    public static void handle(Context context, List<WaitlistEntry> entries) {
        SharedPreferences prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        Set<String> notified = new HashSet<>(prefs.getStringSet(KEY_NOTIFIED, new HashSet<>()));
        boolean active = false;
        for (WaitlistEntry e : entries) {
            if (e.isActive()) active = true;
            String key = e.id + ":" + e.offeredReservationId;
            if (e.isOffered() && !notified.contains(key)) {
                show(context, e);
                notified.add(key);
            }
        }
        prefs.edit().putStringSet(KEY_NOTIFIED, notified).apply();
        if (active) ensureScheduled(context); else WorkManager.getInstance(context).cancelUniqueWork(WORK);
    }

    private static void show(Context context, WaitlistEntry e) {
        if (Build.VERSION.SDK_INT >= 33 && ContextCompat.checkSelfPermission(context,
                Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) return;
        ReservationReminders.initialize(context);
        ZoneId zone = ZoneId.systemDefault();
        String text = "Có bàn cho " + e.partySize + " khách lúc " + WaitlistText.clock(e.offeredReservedAtUtc, zone)
                + " tại " + e.branchName + ". Xác nhận trước " + WaitlistText.clock(e.offerExpiresAtUtc, zone) + " để giữ bàn.";
        Intent open = new Intent(context, MainActivity.class).putExtra(EXTRA_OPEN_WAITLIST, true)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_SINGLE_TOP | Intent.FLAG_ACTIVITY_CLEAR_TOP);
        PendingIntent tap = PendingIntent.getActivity(context, ("waitlist" + e.id).hashCode(), open,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        NotificationCompat.Builder builder = new NotificationCompat.Builder(context, ReservationReminders.CHANNEL_ID)
                .setSmallIcon(R.drawable.ic_notification)
                .setContentTitle("Có bàn cho bạn")
                .setContentText(text)
                .setStyle(new NotificationCompat.BigTextStyle().bigText(text))
                .setPriority(NotificationCompat.PRIORITY_HIGH)
                .setAutoCancel(true)
                .setContentIntent(tap);
        try { NotificationManagerCompat.from(context).notify(("waitlist" + e.id).hashCode(), builder.build()); }
        catch (SecurityException ignored) { }
    }
}
