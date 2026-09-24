package com.restaurant.viettable.network;

import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.testing.BackendIntegrationTest;
import org.junit.After;
import org.junit.Before;
import org.junit.Test;
import org.junit.runner.RunWith;
import android.view.View;
import androidx.test.espresso.UiController;
import androidx.test.espresso.ViewAction;
import org.hamcrest.Matcher;
import static androidx.test.espresso.Espresso.onView;
import static androidx.test.espresso.action.ViewActions.*;
import static androidx.test.espresso.assertion.ViewAssertions.matches;
import static androidx.test.espresso.matcher.RootMatchers.isDialog;
import static androidx.test.espresso.matcher.ViewMatchers.*;
import static org.hamcrest.Matchers.*;
import static org.junit.Assert.*;

@RunWith(AndroidJUnit4.class)
@BackendIntegrationTest
public class AuthUiIntegrationTest {
    private IsolatedUiSession session;
    private ActivityScenario<MainActivity> scenario;

    @Before public void isolateSession() throws Exception {
        session = new IsolatedUiSession();
        session.requireCredentials();
    }
    @After public void restoreSession() throws Exception {
        if (scenario != null) scenario.close();
        session.close();
    }

    @Test public void login_validCredentials_opensHomeWithCanonicalMenu_afterRecreate() {
        scenario = ActivityScenario.launch(MainActivity.class);
        UiTestActions.openLogin(scenario);
        onView(withId(R.id.etEmail)).perform(replaceText(session.username), closeSoftKeyboard());
        onView(withId(R.id.etPassword)).perform(replaceText(session.password), closeSoftKeyboard());
        scenario.recreate();
        UiTestActions.waitFragment(scenario, com.restaurant.viettable.ui.auth.LoginFragment.class);
        onView(withId(R.id.etEmail)).check(matches(withText(session.username)));
        onView(withId(R.id.btnSubmitLogin)).perform(click());
        UiTestActions.waitFragment(scenario, com.restaurant.viettable.ui.branch.BranchListFragment.class);
        onView(withId(R.id.rvBranches)).perform(clickChildInFirstRecyclerRow(R.id.btnSelectBranch));
        UiTestActions.waitForHomeCatalog(scenario);
        assertTrue(session.client.hasSession());
        String blob = session.encryptedBlob();
        assertTrue(blob.startsWith("v1:"));
        assertFalse(blob.contains("eyJ"));
    }

    @Test public void login_invalidCredentials_showsBackendError_andDoesNotOpenHome() {
        scenario = ActivityScenario.launch(MainActivity.class);
        UiTestActions.openLogin(scenario);
        onView(withId(R.id.etEmail)).perform(replaceText(session.username + "_invalid"), closeSoftKeyboard());
        onView(withId(R.id.etPassword)).perform(replaceText(session.password), closeSoftKeyboard());
        onView(withId(R.id.btnSubmitLogin)).perform(click());
        MainActivity activity = UiTestActions.activity(scenario);
        com.restaurant.viettable.testing.UiWaiter.until("backend login error", activity,
                ignored -> com.restaurant.viettable.testing.UiWaiter.shown(activity.findViewById(R.id.tvError)));
        onView(withId(R.id.tvError)).check(matches(allOf(isDisplayed(), not(withText("")))));
        assertFalse(session.client.hasSession());
        assertTrue(activity.getSupportFragmentManager().findFragmentById(R.id.fragment_container)
                instanceof com.restaurant.viettable.ui.auth.LoginFragment);
    }

    @Test public void logout_clearsSecureSession_stopsSignalR_andReturnsToAuth() throws Exception {
        session.loginThroughGateway();
        scenario = ActivityScenario.launch(MainActivity.class);
        UiTestActions.waitForHomeCatalog(scenario);
        scenario.onActivity(activity -> SignalRManager.getInstance().pause());
        com.restaurant.viettable.testing.UiWaiter.until("SignalR stopped before reconnect",
                UiTestActions.activity(scenario), ignored -> SignalRManager.getInstance().getState()
                        == SignalRManager.ConnectionState.STOPPED);
        scenario.onActivity(activity -> SignalRManager.getInstance().resume());
        com.restaurant.viettable.testing.UiWaiter.until("SignalR reconnected before logout",
                UiTestActions.activity(scenario), ignored -> SignalRManager.getInstance().isConnected());
        onView(withId(R.id.nav_profile)).perform(click());
        UiTestActions.waitFragment(scenario, com.restaurant.viettable.ui.profile.ProfileFragment.class);
        onView(withId(R.id.btnLogout)).perform(scrollTo(), click());
        onView(withText("Đăng xuất")).inRoot(isDialog()).perform(click());
        UiTestActions.waitFragment(scenario, com.restaurant.viettable.ui.auth.WelcomeFragment.class);
        assertFalse(session.client.hasSession());
        assertEquals(SignalRManager.ConnectionState.STOPPED, SignalRManager.getInstance().getState());
        UiTestActions.openLogin(scenario);
        onView(withId(R.id.etEmail)).perform(replaceText(session.username), closeSoftKeyboard());
        onView(withId(R.id.etPassword)).perform(replaceText(session.password), closeSoftKeyboard());
        onView(withId(R.id.btnSubmitLogin)).perform(click());
        UiTestActions.waitFragment(scenario, com.restaurant.viettable.ui.branch.BranchListFragment.class);
        onView(withId(R.id.rvBranches)).perform(clickChildInFirstRecyclerRow(R.id.btnSelectBranch));
        UiTestActions.waitForHomeCatalog(scenario);
        com.restaurant.viettable.testing.UiWaiter.until("SignalR connected after login again",
                UiTestActions.activity(scenario), ignored -> SignalRManager.getInstance().isConnected());
    }

    private static ViewAction clickChildInFirstRecyclerRow(int childId) {
        return new ViewAction() {
            @Override public Matcher<View> getConstraints() {
                return isAssignableFrom(androidx.recyclerview.widget.RecyclerView.class);
            }
            @Override public String getDescription() { return "click child in first canonical RecyclerView row"; }
            @Override public void perform(UiController controller, View view) {
                androidx.recyclerview.widget.RecyclerView list = (androidx.recyclerview.widget.RecyclerView) view;
                assertNotNull("Branch adapter missing", list.getAdapter());
                assertTrue("Gateway returned no branches", list.getAdapter().getItemCount() > 0);
                list.scrollToPosition(0);
                controller.loopMainThreadUntilIdle();
                androidx.recyclerview.widget.RecyclerView.ViewHolder holder = list.findViewHolderForAdapterPosition(0);
                assertNotNull("First branch row is not attached", holder);
                View child = holder.itemView.findViewById(childId);
                assertNotNull("Select action missing", child);
                assertTrue("First branch cannot be selected", child.performClick());
                controller.loopMainThreadUntilIdle();
            }
        };
    }

}
