package com.restaurant.viettable;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;

import android.content.Context;
import android.os.Build;
import android.view.ContextThemeWrapper;
import android.widget.FrameLayout;

import androidx.recyclerview.widget.RecyclerView;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;

import com.restaurant.viettable.adapter.ReceiptHistoryAdapter;
import com.restaurant.viettable.assistant.BookingIntent;
import com.restaurant.viettable.assistant.BookingIntentParser;
import com.restaurant.viettable.model.Receipt;

import org.json.JSONObject;
import org.junit.Test;
import org.junit.runner.RunWith;

import java.time.LocalDate;
import java.util.Collections;

/**
 * Owner 08/11: crashes on Android 7/8 (Lint NewApi). Runs on the API 24 and API 26 emulators: the assistant's time
 * parsing (named regex groups, API 26) and the invoice history month header (View#setAccessibilityHeading, API 28).
 */
@RunWith(AndroidJUnit4.class)
public class OldAndroidCrashTest {
    @Test
    public void assistantReadsATimeOnThisAndroid() {
        BookingIntent intent = BookingIntentParser.parse("tối mai 7 giờ rưỡi 4 người", LocalDate.of(2026, 11, 8), Collections.emptyList());
        assertNotNull("Android " + Build.VERSION.SDK_INT, intent.time);
        assertEquals(19, intent.time.getHour());
        assertEquals(30, intent.time.getMinute());
        assertEquals(Integer.valueOf(4), intent.partySize);
    }

    @Test
    public void invoiceHistoryMonthHeaderBuildsOnThisAndroid() throws Exception {
        Context context = new ContextThemeWrapper(InstrumentationRegistry.getInstrumentation().getTargetContext(), R.style.Theme_VietTable);
        ReceiptHistoryAdapter adapter = new ReceiptHistoryAdapter(r -> { });
        Receipt receipt = Receipt.fromJson(new JSONObject("{\"invoiceId\":44,\"invoiceCode\":\"HD44\",\"issuedAtUtc\":\"2026-11-05T12:00:00Z\",\"totalAmount\":250000,\"branchName\":\"Chi nhánh Quận 1\"}"));
        adapter.submit(Collections.singletonList(receipt), false);
        assertEquals(0, adapter.getItemViewType(0)); // the month header row
        final RecyclerView.ViewHolder[] holder = new RecyclerView.ViewHolder[1];
        InstrumentationRegistry.getInstrumentation().runOnMainSync(() -> {
            FrameLayout parent = new FrameLayout(context);
            holder[0] = adapter.onCreateViewHolder(parent, adapter.getItemViewType(0));
            adapter.onBindViewHolder(holder[0], 0);
        });
        assertNotNull("Android " + Build.VERSION.SDK_INT, holder[0].itemView);
    }
}
