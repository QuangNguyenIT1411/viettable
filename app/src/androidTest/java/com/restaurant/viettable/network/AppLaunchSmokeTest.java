package com.restaurant.viettable.network;

import androidx.lifecycle.Lifecycle;
import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.testing.BackendIntegrationTest;
import com.restaurant.viettable.testing.SmokeTest;
import org.junit.After;
import org.junit.Before;
import org.junit.Test;
import org.junit.runner.RunWith;
import static org.junit.Assert.*;

@RunWith(AndroidJUnit4.class)
@SmokeTest
public class AppLaunchSmokeTest {
    private IsolatedUiSession session;
    private ActivityScenario<MainActivity> scenario;

    @Before public void isolateSession() throws Exception { session = new IsolatedUiSession(); }
    @After public void restoreSession() throws Exception {
        if (scenario != null) scenario.close();
        session.close();
    }

    @Test public void coldLaunch_withoutSession_opensAuthenticationWithoutCrash() {
        scenario = ActivityScenario.launch(MainActivity.class);
        UiTestActions.openLogin(scenario);
        assertFalse(session.client.hasSession());
    }

    @Test @BackendIntegrationTest
    public void coldLaunch_authenticatedSession_loadsRealBranchMenu_andSurvivesBackgroundForeground() throws Exception {
        session.loginThroughGateway();
        scenario = ActivityScenario.launch(MainActivity.class);
        UiTestActions.waitForHomeCatalog(scenario);
        int categoryCount = AppStateManager.getInstance().getCategories().size();
        int foodCount = AppStateManager.getInstance().getFoodItems().size();
        assertTrue(categoryCount > 0); assertTrue(foodCount > 0);
        scenario.moveToState(Lifecycle.State.CREATED);
        scenario.moveToState(Lifecycle.State.RESUMED);
        UiTestActions.waitForHomeCatalog(scenario);
        assertEquals(categoryCount, AppStateManager.getInstance().getCategories().size());
        assertEquals(foodCount, AppStateManager.getInstance().getFoodItems().size());
        MainActivity activity = UiTestActions.activity(scenario);
        com.restaurant.viettable.testing.UiWaiter.until("SignalR connected after foreground", activity,
                ignored -> SignalRManager.getInstance().isConnected());
    }
}
