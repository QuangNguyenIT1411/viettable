package com.restaurant.viettable.network;

import android.Manifest;
import android.content.Context;
import android.content.pm.PackageManager;
import android.os.Bundle;
import androidx.core.content.ContextCompat;
import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;
import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.testing.BackendIntegrationTest;
import org.junit.After;
import org.junit.Assume;
import org.junit.Before;
import org.junit.Test;
import org.junit.runner.RunWith;
import java.util.UUID;
import static androidx.test.espresso.Espresso.onView;
import static androidx.test.espresso.action.ViewActions.*;
import static androidx.test.espresso.assertion.ViewAssertions.matches;
import static androidx.test.espresso.matcher.ViewMatchers.*;
import static org.hamcrest.Matchers.*;
import static org.junit.Assert.*;

@RunWith(AndroidJUnit4.class)
@BackendIntegrationTest
public class QrManualUiIntegrationTest {
    private static final String CAMERA_PREFS = "qr_camera_permission";
    private static final String REQUESTED = "requested";
    private IsolatedUiSession session;
    private ActivityScenario<MainActivity> scenario;
    private boolean requestedWasPresent;
    private boolean requestedWasTrue;

    @Before public void isolateSessionAndDenyCamera() throws Exception {
        session = new IsolatedUiSession();
        session.requireCredentials();
        Bundle arguments = InstrumentationRegistry.getArguments();
        Assume.assumeTrue("Pass -e testQrCode with a current backend QR fixture",
                !arguments.getString("testQrCode", "").trim().isEmpty());
        session.loginThroughGateway();
        Context context = session.context;
        Assume.assumeTrue("Runner must start this test with camera permission denied",
                ContextCompat.checkSelfPermission(context, Manifest.permission.CAMERA)
                        != PackageManager.PERMISSION_GRANTED);
        android.content.SharedPreferences prefs = context.getSharedPreferences(CAMERA_PREFS, 0);
        requestedWasPresent = prefs.contains(REQUESTED);
        requestedWasTrue = prefs.getBoolean(REQUESTED, false);
        prefs.edit().putBoolean(REQUESTED, true).commit();
    }

    @After public void restoreDeviceAndSession() throws Exception {
        if (scenario != null) scenario.close();
        android.content.SharedPreferences prefs = session.context.getSharedPreferences(CAMERA_PREFS, 0);
        if (requestedWasPresent) prefs.edit().putBoolean(REQUESTED, requestedWasTrue).commit();
        else prefs.edit().remove(REQUESTED).commit();
        session.close();
    }

    @Test public void qrPermissionDenied_manualInvalidThenValid_recreatesAndUsesGatewayValidation() {
        String validQr = InstrumentationRegistry.getArguments().getString("testQrCode").trim();
        scenario = ActivityScenario.launch(MainActivity.class);
        UiTestActions.waitForHomeCatalog(scenario);
        onView(withId(R.id.nav_qr)).perform(click());
        UiTestActions.waitFragment(scenario, com.restaurant.viettable.ui.session.QrScanFragment.class);
        onView(withId(R.id.tvCameraStatus)).check(matches(isDisplayed()));
        onView(withId(R.id.etTableCode)).check(matches(isDisplayed()));

        String invalid = "missing-" + UUID.randomUUID().toString().substring(0, 12);
        onView(withId(R.id.etTableCode)).perform(replaceText(invalid), closeSoftKeyboard());
        onView(withId(R.id.btnSubmitTableCode)).perform(click());
        UiTestActions.waitFragment(scenario, com.restaurant.viettable.ui.session.QrScanFragment.class);
        onView(withId(R.id.btnSubmitTableCode)).check(matches(isEnabled()));

        onView(withId(R.id.etTableCode)).perform(replaceText(validQr), closeSoftKeyboard());
        scenario.recreate();
        UiTestActions.waitFragment(scenario, com.restaurant.viettable.ui.session.QrScanFragment.class);
        onView(withId(R.id.etTableCode)).check(matches(withText(validQr)));
        onView(withId(R.id.btnSubmitTableCode)).perform(click());
        UiTestActions.waitFragment(scenario, com.restaurant.viettable.ui.session.WalkinConfirmFragment.class);
        onView(withId(R.id.tvWalkinTable)).check(matches(allOf(isDisplayed(), not(withText("")))));
        assertNull("Validation must not create a fake/local session",
                com.restaurant.viettable.data.AppStateManager.getInstance().getActiveDiningSession());
    }
}
