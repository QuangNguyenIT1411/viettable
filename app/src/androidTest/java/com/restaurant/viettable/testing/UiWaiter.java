package com.restaurant.viettable.testing;

import android.app.Activity;
import android.os.Handler;
import android.os.Looper;
import android.view.View;
import androidx.annotation.Nullable;
import androidx.test.espresso.IdlingPolicies;
import androidx.test.espresso.IdlingRegistry;
import androidx.test.espresso.IdlingResource;
import java.util.concurrent.TimeUnit;
import java.util.function.Predicate;
import static androidx.test.espresso.Espresso.onView;
import static androidx.test.espresso.matcher.ViewMatchers.isRoot;

/** Callback-aware polling on the main looper; tests never block UI callbacks with Thread.sleep. */
public final class UiWaiter {
    private UiWaiter() { }

    public static void until(String name, Activity activity, Predicate<Activity> condition) {
        IdlingPolicies.setIdlingResourceTimeout(40, TimeUnit.SECONDS);
        ConditionResource resource = new ConditionResource(name, activity, condition);
        IdlingRegistry.getInstance().register(resource);
        try {
            onView(isRoot()).check((view, error) -> { if (error != null) throw error; });
        } finally {
            IdlingRegistry.getInstance().unregister(resource);
            resource.close();
        }
    }

    public static boolean shown(@Nullable View view) {
        return view != null && view.getVisibility() == View.VISIBLE && view.isShown();
    }

    private static final class ConditionResource implements IdlingResource {
        private final String name;
        private final Activity activity;
        private final Predicate<Activity> condition;
        private final Handler main = new Handler(Looper.getMainLooper());
        private volatile ResourceCallback callback;
        private volatile boolean closed;
        private final Runnable poll = new Runnable() {
            @Override public void run() {
                if (closed) return;
                if (condition.test(activity)) {
                    ResourceCallback current = callback;
                    if (current != null) current.onTransitionToIdle();
                } else main.postDelayed(this, 50);
            }
        };

        ConditionResource(String name, Activity activity, Predicate<Activity> condition) {
            this.name = name;
            this.activity = activity;
            this.condition = condition;
        }
        @Override public String getName() { return name; }
        @Override public boolean isIdleNow() { return !closed && condition.test(activity); }
        @Override public void registerIdleTransitionCallback(ResourceCallback value) {
            callback = value;
            main.removeCallbacks(poll);
            main.post(poll);
        }
        void close() { closed = true; main.removeCallbacks(poll); }
    }
}
