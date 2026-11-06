package com.restaurant.viettable.reminder;

import android.content.Context;

import androidx.annotation.NonNull;
import androidx.work.Worker;
import androidx.work.WorkerParameters;

import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.network.ApiException;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

/** Sends the reminder answer to the server and replaces the notification with the result. */
public final class ReminderActionWorker extends Worker {
    static final String KEY_RESERVATION_ID = "reservationId";
    static final String KEY_ANSWER = "answer";
    static final String KEY_NOTIFICATION_ID = "notificationId";

    public ReminderActionWorker(@NonNull Context context, @NonNull WorkerParameters params) {
        super(context, params);
    }

    @NonNull
    @Override
    public Result doWork() {
        String reservationId = getInputData().getString(KEY_RESERVATION_ID);
        String answer = getInputData().getString(KEY_ANSWER);
        int notificationId = getInputData().getInt(KEY_NOTIFICATION_ID, 0);
        if (reservationId == null || !ReminderActions.isKnown(answer)) return Result.success();
        CountDownLatch done = new CountDownLatch(1);
        AtomicReference<ApiException> failure = new AtomicReference<>();
        MobileRepository.getInstance().answerReminder(reservationId, answer, new MobileRepository.Callback<Reservation>() {
            @Override public void onSuccess(Reservation value) { done.countDown(); }
            @Override public void onError(ApiException error) { failure.set(error); done.countDown(); }
        });
        boolean finished;
        try { finished = done.await(90, TimeUnit.SECONDS); }
        catch (InterruptedException e) { Thread.currentThread().interrupt(); finished = false; }
        ApiException error = failure.get();
        String text = finished && error == null ? ReminderActions.success(answer) : ReminderActions.failure(answer, error);
        ReservationReminders.showOutcome(getApplicationContext(), reservationId, notificationId, text);
        return Result.success();
    }
}
