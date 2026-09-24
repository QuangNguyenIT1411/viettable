package com.restaurant.viettable.network;

import androidx.lifecycle.Lifecycle;
import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;
import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.testing.BackendIntegrationTest;
import com.restaurant.viettable.testing.UiWaiter;
import com.restaurant.viettable.ui.auth.SplashFragment;
import java.lang.reflect.Field;
import java.util.concurrent.atomic.AtomicReference;
import org.junit.After;
import org.junit.Before;
import org.junit.Test;
import org.junit.runner.RunWith;
import static org.junit.Assert.*;

@RunWith(AndroidJUnit4.class)
@BackendIntegrationTest
public class LifecycleRecoveryTest {
    private IsolatedUiSession session;
    private ActivityScenario<MainActivity> scenario;

    @Before public void setUp() throws Exception {
        session = new IsolatedUiSession();
        session.loginThroughGateway();
    }

    @After public void tearDown() throws Exception {
        if (scenario != null) scenario.close();
        session.close();
    }

    @Test public void splash_staleCallbackAfterStateSaved_isIgnored_thenResumesOnce() throws Exception {
        scenario = ActivityScenario.launch(MainActivity.class);
        UiTestActions.waitForHomeCatalog(scenario);
        AtomicReference<SplashFragment> splash = new AtomicReference<>();
        scenario.onActivity(activity -> {
            splash.set(SplashFragment.newInstance());
            activity.getSupportFragmentManager().beginTransaction()
                    .replace(R.id.fragment_container, splash.get()).commitNow();
        });
        Field callbackField = SplashFragment.class.getDeclaredField("navigateRunnable");
        callbackField.setAccessible(true);
        Runnable staleCallback = (Runnable) callbackField.get(splash.get());
        scenario.moveToState(Lifecycle.State.CREATED);
        InstrumentationRegistry.getInstrumentation().runOnMainSync(() -> {
            assertTrue(splash.get().getParentFragmentManager().isStateSaved());
            staleCallback.run(); // Exact previously crashing callback, without a timing sleep.
            assertSame(splash.get(), splash.get().getParentFragmentManager()
                    .findFragmentById(R.id.fragment_container));
        });
        scenario.moveToState(Lifecycle.State.RESUMED);
        UiTestActions.waitForHomeCatalog(scenario);
        scenario.onActivity(activity -> {
            Object home = activity.getSupportFragmentManager().findFragmentById(R.id.fragment_container);
            staleCallback.run(); // A detached Splash cannot navigate a second time.
            activity.getSupportFragmentManager().executePendingTransactions();
            assertSame(home, activity.getSupportFragmentManager().findFragmentById(R.id.fragment_container));
            assertEquals(0, activity.getSupportFragmentManager().getBackStackEntryCount());
        });
    }

    @Test public void signalR_stopWhileStarting_andRepeatedStarts_recoverWithOneHub() throws Exception {
        scenario = ActivityScenario.launch(MainActivity.class);
        UiTestActions.waitForHomeCatalog(scenario);
        SignalRManager manager = SignalRManager.getInstance();
        awaitConnected();
        java.util.concurrent.atomic.AtomicInteger removedNotifications = new java.util.concurrent.atomic.AtomicInteger();
        scenario.onActivity(activity -> {
            SignalRManager.Listener removed = state -> removedNotifications.incrementAndGet();
            manager.addListener(removed);
            manager.removeListener(removed);
        });
        scenario.onActivity(activity -> manager.pause());
        UiWaiter.until("Hub stopped", UiTestActions.activity(scenario),
                ignored -> manager.getState() == SignalRManager.ConnectionState.STOPPED);
        scenario.onActivity(activity -> {
            manager.resume();
            manager.pause(); // Same main turn: start completion cannot dispatch before pause.
            manager.resume();
            manager.startIfAuthenticated();
            manager.startIfAuthenticated();
        });
        awaitConnected();
        assertEquals("Removed UI observer must not receive queued callbacks", 0, removedNotifications.get());
        Field hubField = SignalRManager.class.getDeclaredField("hub");
        hubField.setAccessible(true);
        Object connectedHub = hubField.get(manager);
        scenario.onActivity(activity -> {
            manager.resume();
            manager.startIfAuthenticated();
            manager.startIfAuthenticated();
        });
        assertSame("Repeated start must retain exactly the current Hub", connectedHub, hubField.get(manager));
        scenario.recreate();
        UiTestActions.waitForHomeCatalog(scenario);
        awaitConnected();
    }

    private void awaitConnected() {
        UiWaiter.until("SignalR connected", UiTestActions.activity(scenario),
                ignored -> SignalRManager.getInstance().isConnected());
    }

    @Test public void disconnectedScreen_keepsRestFallback_thenCancelsPollingAfterReconnect() throws Exception {
        scenario = ActivityScenario.launch(MainActivity.class);
        UiTestActions.waitForHomeCatalog(scenario);
        awaitConnected();
        com.restaurant.viettable.ui.order.CurrentOrdersFragment orders =
                new com.restaurant.viettable.ui.order.CurrentOrdersFragment();
        scenario.onActivity(activity -> {
            activity.navigateTo(orders, true);
            SignalRManager.getInstance().pause();
        });
        UiTestActions.waitFragment(scenario, orders.getClass());
        Field handlerField = orders.getClass().getDeclaredField("handler");
        Field refreshField = orders.getClass().getDeclaredField("fallbackRefresh");
        handlerField.setAccessible(true);
        refreshField.setAccessible(true);
        android.os.Handler handler = (android.os.Handler) handlerField.get(orders);
        Runnable refresh = (Runnable) refreshField.get(orders);
        UiWaiter.until("REST fallback scheduled while disconnected", UiTestActions.activity(scenario),
                ignored -> handler.hasCallbacks(refresh));
        // Execute the scheduled operation now rather than waiting an arbitrary ten seconds.
        scenario.onActivity(activity -> refresh.run());
        UiWaiter.until("REST fallback rescheduled after canonical response", UiTestActions.activity(scenario),
                ignored -> handler.hasCallbacks(refresh));
        scenario.onActivity(activity -> SignalRManager.getInstance().resume());
        awaitConnected();
        UiWaiter.until("fallback cancelled when realtime recovers", UiTestActions.activity(scenario),
                ignored -> !handler.hasCallbacks(refresh));
    }
}
