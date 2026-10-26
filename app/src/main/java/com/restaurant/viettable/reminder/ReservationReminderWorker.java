package com.restaurant.viettable.reminder;

import android.content.Context;

import androidx.annotation.NonNull;
import androidx.work.Worker;
import androidx.work.WorkerParameters;

/** Shows one booking reminder at its time; the text was fixed when it was scheduled (see ReservationReminders). */
public final class ReservationReminderWorker extends Worker {
    static final String KEY_RESERVATION_ID = "reservationId";
    static final String KEY_OFFSET = "offsetMinutes";
    static final String KEY_TEXT = "text";

    public ReservationReminderWorker(@NonNull Context context, @NonNull WorkerParameters params) {
        super(context, params);
    }

    @NonNull
    @Override
    public Result doWork() {
        String reservationId = getInputData().getString(KEY_RESERVATION_ID);
        String text = getInputData().getString(KEY_TEXT);
        if (reservationId == null || text == null) return Result.success();
        ReservationReminders.show(getApplicationContext(), reservationId, getInputData().getInt(KEY_OFFSET, 0), text);
        return Result.success();
    }
}
