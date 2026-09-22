package com.restaurant.viettable.network;

import android.widget.LinearLayout;
import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.testing.BackendIntegrationTest;
import org.json.JSONArray;
import org.json.JSONObject;
import org.junit.After;
import org.junit.Before;
import org.junit.Test;
import org.junit.runner.RunWith;
import static androidx.test.espresso.Espresso.onView;
import static androidx.test.espresso.action.ViewActions.*;
import static androidx.test.espresso.matcher.ViewMatchers.withId;
import static org.junit.Assert.*;

@RunWith(AndroidJUnit4.class)
@BackendIntegrationTest
public class FaqUiIntegrationTest {
    private IsolatedUiSession session;
    private ActivityScenario<MainActivity> scenario;

    @Before public void isolateSession() throws Exception {
        session = new IsolatedUiSession();
        session.requireCredentials();
        session.loginThroughGateway();
    }
    @After public void restoreSession() throws Exception {
        if (scenario != null) scenario.close();
        session.close();
    }

    @Test public void faq_openFromProfileAndReload_rendersBackendItemsOnce() throws Exception {
        JSONObject backend = session.getObject("/api/mobile/v1/support/faqs", false);
        JSONArray items = backend.optJSONArray("items");
        assertNotNull(items); assertTrue("Backend FAQ fixture must not be empty", items.length() > 0);
        int expectedChildren = 3 + items.length(); // progress, state, retry + one row per API item

        scenario = ActivityScenario.launch(MainActivity.class);
        UiTestActions.waitForHomeCatalog(scenario);
        onView(withId(R.id.nav_profile)).perform(click());
        UiTestActions.waitFragment(scenario, com.restaurant.viettable.ui.profile.ProfileFragment.class);
        onView(withId(R.id.rowSupport)).perform(scrollTo(), click());
        waitForFaqCount(expectedChildren);
        assertFaqRowsHaveQuestionAndAnswer(items.length());

        scenario.onActivity(activity -> activity.navigateTo(
                com.restaurant.viettable.ui.profile.SupportFragment.newInstance(), false));
        UiTestActions.waitFragment(scenario, com.restaurant.viettable.ui.profile.SupportFragment.class);
        waitForFaqCount(expectedChildren);
        assertFaqRowsHaveQuestionAndAnswer(items.length());
    }

    private void waitForFaqCount(int expected) {
        MainActivity activity = UiTestActions.activity(scenario);
        com.restaurant.viettable.testing.UiWaiter.until("FAQ rows from Gateway", activity, ignored -> {
            LinearLayout rows = activity.findViewById(R.id.layoutFaqItems);
            return rows != null && rows.getChildCount() == expected
                    && activity.findViewById(R.id.progressFaq).getVisibility() != android.view.View.VISIBLE;
        });
    }

    private void assertFaqRowsHaveQuestionAndAnswer(int count) {
        scenario.onActivity(activity -> {
            LinearLayout rows = activity.findViewById(R.id.layoutFaqItems);
            assertEquals(3 + count, rows.getChildCount());
            for (int index = 3; index < rows.getChildCount(); index++) {
                LinearLayout row = (LinearLayout) rows.getChildAt(index);
                assertEquals(2, row.getChildCount());
                assertFalse(((android.widget.TextView) row.getChildAt(0)).getText().toString().trim().isEmpty());
                assertFalse(((android.widget.TextView) row.getChildAt(1)).getText().toString().trim().isEmpty());
            }
        });
    }
}
