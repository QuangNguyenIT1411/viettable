package com.restaurant.viettable.reminder;

import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

import androidx.work.Constraints;
import androidx.work.Data;
import androidx.work.ExistingWorkPolicy;
import androidx.work.NetworkType;
import androidx.work.OneTimeWorkRequest;
import androidx.work.WorkManager;

/** A tap on "Sẽ đến" / "Huỷ lịch": the answer is sent by a worker, so it goes through even when the app is closed. */
public final class ReminderActionReceiver extends BroadcastReceiver {
    static final String ACTION = "com.restaurant.viettable.action.REMINDER_ANSWER";
    static final String EXTRA_ANSWER = "answer";
    static final String EXTRA_NOTIFICATION_ID = "notificationId";

    static PendingIntent intent(Context context, String reservationId, int notificationId, String answer) {
        Intent intent = new Intent(context, ReminderActionReceiver.class)
                .setAction(ACTION)
                .putExtra(ReservationReminders.EXTRA_RESERVATION_ID, reservationId)
                .putExtra(EXTRA_NOTIFICATION_ID, notificationId)
                .putExtra(EXTRA_ANSWER, answer);
        int requestCode = (notificationId + ":" + answer).hashCode();
        return PendingIntent.getBroadcast(context, requestCode, intent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }

    @Override
    public void onReceive(Context context, Intent intent) {
        if (intent == null || !ACTION.equals(intent.getAction())) return;
        String reservationId = intent.getStringExtra(ReservationReminders.EXTRA_RESERVATION_ID);
        String answer = intent.getStringExtra(EXTRA_ANSWER);
        int notificationId = intent.getIntExtra(EXTRA_NOTIFICATION_ID, 0);
        if (reservationId == null || !ReminderActions.isKnown(answer)) return;
        ReservationReminders.showOutcome(context, reservationId, notificationId, ReminderActions.sending(answer));
        OneTimeWorkRequest work = new OneTimeWorkRequest.Builder(ReminderActionWorker.class)
                .setConstraints(new Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build())
                .setInputData(new Data.Builder()
                        .putString(ReminderActionWorker.KEY_RESERVATION_ID, reservationId)
                        .putString(ReminderActionWorker.KEY_ANSWER, answer)
                        .putInt(ReminderActionWorker.KEY_NOTIFICATION_ID, notificationId)
                        .build())
                .build();
        WorkManager.getInstance(context).enqueueUniqueWork("reminder-answer-" + reservationId, ExistingWorkPolicy.REPLACE, work);
    }
}
