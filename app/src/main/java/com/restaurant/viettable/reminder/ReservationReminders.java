package com.restaurant.viettable.reminder;

import android.Manifest;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.os.Build;
import android.util.Log;

import androidx.core.app.NotificationCompat;
import androidx.core.app.NotificationManagerCompat;
import androidx.core.content.ContextCompat;
import androidx.work.Data;
import androidx.work.ExistingWorkPolicy;
import androidx.work.OneTimeWorkRequest;
import androidx.work.WorkManager;

import com.restaurant.viettable.BuildConfig;
import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.util.DisplayFormatters;
import com.restaurant.viettable.util.ReminderPlan;

import org.json.JSONObject;

import java.time.Duration;
import java.time.Instant;
import java.util.Iterator;
import java.util.List;
import java.util.concurrent.TimeUnit;

/**
 * Local booking reminders (reservation item 3). Each reminder is one unique WorkManager job
 * "res-reminder-{id}-{minutes}"; a sync re-plans them from the reservation list the app just received (open, list
 * refresh, login, realtime "reservationChanged"), replaces a job whose time or text changed and cancels the ones
 * that no longer apply (cancelled, rejected, absent, checked in). Logout clears them all. Best effort: a failure
 * here never affects the booking screens.
 */
public final class ReservationReminders {
    private ReservationReminders() { }

    public static final String CHANNEL_ID = "reservation_reminders";
    public static final String EXTRA_RESERVATION_ID = "com.restaurant.viettable.extra.RESERVATION_ID";
    static final String TAG = "res-reminder";
    private static final String LOG = "VietTableReminders";
    private static final String PREFS = "reservation_reminders";
    private static final String KEY_SCHEDULED = "scheduled";            // {workName: signature}
    private static final String KEY_PERMISSION_ASKED = "notification_permission_asked";

    private static Context app;

    public static void initialize(Context context) {
        app = context.getApplicationContext();
        createChannel(app);
    }

    /** 60/15 minutes; test APKs are built with -PVIETTABLE_REMINDER_MINUTES=3,1. */
    public static List<Integer> offsets() { return ReminderPlan.parseOffsets(BuildConfig.REMINDER_MINUTES); }

    public static synchronized void sync(List<Reservation> reservations) {
        if (app == null || reservations == null) return;
        try {
            SharedPreferences prefs = app.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
            JSONObject before = new JSONObject(prefs.getString(KEY_SCHEDULED, "{}"));
            JSONObject after = new JSONObject();
            WorkManager work = WorkManager.getInstance(app);
            Instant now = Instant.now();
            for (Reservation r : reservations) {
                if (r == null || r.getId() == null) continue;
                Instant reservedAt = DisplayFormatters.instant(r.getReservedAt());
                for (ReminderPlan.Reminder reminder : ReminderPlan.plan(r.getStatus(), reservedAt, now, offsets())) {
                    String name = TAG + "-" + r.getId() + "-" + reminder.offsetMinutes;
                    String text = ReminderPlan.message(reminder.offsetMinutes, r.getTimeSlot(), r.getBranchName(), r.getTableLabel());
                    String signature = reminder.fireAt.toEpochMilli() + "|" + text;
                    after.put(name, signature);
                    if (signature.equals(before.optString(name, null))) continue; // already scheduled as is
                    long delay = Math.max(0, Duration.between(now, reminder.fireAt).toMillis());
                    OneTimeWorkRequest request = new OneTimeWorkRequest.Builder(ReservationReminderWorker.class)
                            .setInitialDelay(delay, TimeUnit.MILLISECONDS)
                            .addTag(TAG)
                            .setInputData(new Data.Builder()
                                    .putString(ReservationReminderWorker.KEY_RESERVATION_ID, r.getId())
                                    .putInt(ReservationReminderWorker.KEY_OFFSET, reminder.offsetMinutes)
                                    .putString(ReservationReminderWorker.KEY_TEXT, text)
                                    .build())
                            .build();
                    work.enqueueUniqueWork(name, ExistingWorkPolicy.REPLACE, request);
                }
            }
            for (Iterator<String> it = before.keys(); it.hasNext(); ) {
                String name = it.next();
                if (!after.has(name)) work.cancelUniqueWork(name);
            }
            prefs.edit().putString(KEY_SCHEDULED, after.toString()).apply();
        } catch (Exception e) {
            Log.w(LOG, "Reminder sync skipped: " + e.getClass().getSimpleName());
        }
    }

    /** Logout / another account: no reminder of the previous user may fire. */
    public static synchronized void clearAll() {
        if (app == null) return;
        try {
            WorkManager.getInstance(app).cancelAllWorkByTag(TAG);
            app.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().remove(KEY_SCHEDULED).apply();
            NotificationManagerCompat.from(app).cancelAll();
        } catch (Exception e) {
            Log.w(LOG, "Reminder clear skipped: " + e.getClass().getSimpleName());
        }
    }

    /** Android 13+: ask once, after the first successful booking; a refusal is never asked again. */
    public static boolean shouldAskPermission(Context context) {
        if (Build.VERSION.SDK_INT < 33) return false;
        if (ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) == PackageManager.PERMISSION_GRANTED)
            return false;
        return !context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getBoolean(KEY_PERMISSION_ASKED, false);
    }

    public static void markPermissionAsked(Context context) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putBoolean(KEY_PERMISSION_ASKED, true).apply();
    }

    static void show(Context context, String reservationId, int offsetMinutes, String text) {
        if (Build.VERSION.SDK_INT >= 33 && ContextCompat.checkSelfPermission(context,
                Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) return;
        NotificationManagerCompat manager = NotificationManagerCompat.from(context);
        if (!manager.areNotificationsEnabled()) return;
        createChannel(context);
        int id = notificationId(reservationId, offsetMinutes);
        // Opens this reservation's detail; MainActivity waits for the fingerprint unlock first.
        Intent open = new Intent(context, MainActivity.class)
                .putExtra(EXTRA_RESERVATION_ID, reservationId)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_SINGLE_TOP | Intent.FLAG_ACTIVITY_CLEAR_TOP);
        PendingIntent tap = PendingIntent.getActivity(context, id, open,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        NotificationCompat.Builder builder = new NotificationCompat.Builder(context, CHANNEL_ID)
                .setSmallIcon(R.drawable.ic_notification)
                .setContentTitle("Nhắc lịch đặt bàn")
                .setContentText(text)
                .setStyle(new NotificationCompat.BigTextStyle().bigText(text))
                .setPriority(NotificationCompat.PRIORITY_HIGH)
                .setCategory(NotificationCompat.CATEGORY_REMINDER)
                .setAutoCancel(true)
                .setContentIntent(tap);
        try { manager.notify(id, builder.build()); }
        catch (SecurityException e) { Log.w(LOG, "Reminder not shown: permission revoked"); }
    }

    static int notificationId(String reservationId, int offsetMinutes) {
        return (TAG + reservationId + ":" + offsetMinutes).hashCode();
    }

    private static void createChannel(Context context) {
        if (Build.VERSION.SDK_INT < 26) return;
        NotificationManager manager = context.getSystemService(NotificationManager.class);
        if (manager == null || manager.getNotificationChannel(CHANNEL_ID) != null) return;
        NotificationChannel channel = new NotificationChannel(CHANNEL_ID, "Nhắc lịch đặt bàn", NotificationManager.IMPORTANCE_HIGH);
        channel.setDescription("Nhắc trước giờ hẹn của các đặt bàn còn hiệu lực.");
        manager.createNotificationChannel(channel);
    }
}
