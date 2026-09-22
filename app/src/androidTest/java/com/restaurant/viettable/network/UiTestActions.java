package com.restaurant.viettable.network;

import android.app.Activity;
import android.view.View;
import android.widget.LinearLayout;
import androidx.fragment.app.Fragment;
import androidx.test.core.app.ActivityScenario;
import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.testing.UiWaiter;
import java.util.concurrent.atomic.AtomicReference;
import static androidx.test.espresso.Espresso.onView;
import static androidx.test.espresso.action.ViewActions.click;
import static androidx.test.espresso.matcher.ViewMatchers.withId;
import static org.junit.Assert.*;

final class UiTestActions {
    private UiTestActions() { }

    static MainActivity activity(ActivityScenario<MainActivity> scenario) {
        AtomicReference<MainActivity> value = new AtomicReference<>();
        scenario.onActivity(value::set);
        return value.get();
    }

    static void waitFragment(ActivityScenario<MainActivity> scenario, Class<? extends Fragment> type) {
        MainActivity activity = activity(scenario);
        UiWaiter.until("fragment " + type.getSimpleName(), activity, ignored -> {
            Fragment current = activity.getSupportFragmentManager().findFragmentById(R.id.fragment_container);
            return type.isInstance(current);
        });
    }

    static void openLogin(ActivityScenario<MainActivity> scenario) {
        waitFragment(scenario, com.restaurant.viettable.ui.auth.WelcomeFragment.class);
        onView(withId(R.id.btnLogin)).perform(click());
        waitFragment(scenario, com.restaurant.viettable.ui.auth.LoginFragment.class);
    }

    static void waitForHomeCatalog(ActivityScenario<MainActivity> scenario) {
        waitFragment(scenario, com.restaurant.viettable.ui.home.HomeFragment.class);
        MainActivity activity = activity(scenario);
        UiWaiter.until("real branch and menu", activity, ignored -> {
            View branch = activity.findViewById(R.id.tvHeaderBranchName);
            LinearLayout categories = activity.findViewById(R.id.layoutCategoryChips);
            androidx.recyclerview.widget.RecyclerView foods = activity.findViewById(R.id.rvFeaturedFoods);
            return UiWaiter.shown(branch) && categories != null && categories.getChildCount() > 0
                    && foods != null && foods.getAdapter() != null && foods.getAdapter().getItemCount() > 0;
        });
    }
}
