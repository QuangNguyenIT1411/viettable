package com.restaurant.viettable.network;

import android.os.Bundle;
import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;
import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.ui.payment.*;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import org.junit.Test;
import org.junit.Assume;
import org.junit.runner.RunWith;
import static org.junit.Assert.*;
import static androidx.test.espresso.Espresso.onView;
import static androidx.test.espresso.action.ViewActions.*;
import static androidx.test.espresso.matcher.ViewMatchers.*;
import static androidx.test.espresso.assertion.ViewAssertions.matches;
import static org.hamcrest.Matchers.*;
import com.restaurant.viettable.testing.UiWaiter;

/** Opt-in coordinated acceptance: a real cashier clicks the browser while Android polls.
 * No test endpoint, fake payment, or production account store is used.
 */
@RunWith(AndroidJUnit4.class)
public class OnsiteCashierAcceptanceTest {
    private static class Reply<T> implements MobileRepository.Callback<T> {
        final CountDownLatch ready = new CountDownLatch(1);
        T value; ApiException error;
        public void onSuccess(T result) { value = result; ready.countDown(); }
        public void onError(ApiException failure) { error = failure; ready.countDown(); }
        T await() throws Exception {
            assertTrue("Repository callback timeout", ready.await(40, TimeUnit.SECONDS));
            assertNull("Repository error: " + (error == null ? "" : error.getCode()), error);
            return value;
        }
    }
    @Test public void pendingToCashierSuccessAndInvoice() throws Exception {
        Assume.assumeTrue("Requires coordinated cashier browser acceptance",
            "true".equals(InstrumentationRegistry.getArguments().getString("onsiteAcceptance")));
        try (IsolatedUiSession session = new IsolatedUiSession()) {
            session.loginThroughGateway();
            try (ActivityScenario<MainActivity> scenario = ActivityScenario.launch(MainActivity.class)) {
                UiTestActions.waitForHomeCatalog(scenario);
                MobileRepository repository = MobileRepository.getInstance();
                Reply<DiningSession> active = new Reply<>(); repository.loadActiveSession(active);
                DiningSession dining = active.await(); assertNotNull("Real fixture session required", dining);
                Reply<Invoice> quoted = new Reply<>(); repository.beginCheckout(quoted); quoted.await();
                Reply<Invoice> requested = new Reply<>(); repository.requestCheckoutPayment(requested);
                Invoice invoice = requested.await();
                boolean split = "true".equals(InstrumentationRegistry.getArguments().getString("onsiteSplit"));
                if (split) {
                    scenario.onActivity(a -> a.navigateTo(SplitBillFragment.newInstance(invoice), true));
                    UiTestActions.waitFragment(scenario, SplitBillFragment.class);
                    UiWaiter.until("split editor", UiTestActions.activity(scenario), a -> UiWaiter.shown(a.findViewById(R.id.btnDoneSplit)) && a.findViewById(R.id.btnDoneSplit).isEnabled());
                    onView(withId(R.id.btnMinusSplit)).perform(scrollTo(), click());
                    onView(withId(R.id.btnDoneSplit)).perform(click());
                } else scenario.onActivity(a -> a.navigateTo(PaymentTypeFragment.newInstance(invoice), true));
                for (int part = 1; part <= (split ? 2 : 1); part++) {
                if (split) {
                    UiWaiter.until("split parts", UiTestActions.activity(scenario), a -> ((android.widget.LinearLayout)a.findViewById(R.id.layoutSplitParts)).getChildCount() == 2);
                    onView(allOf(withText("Thanh toán"), hasSibling(withText(startsWith("Phần " + part + "\n"))))).perform(scrollTo(), click());
                }
                UiTestActions.waitFragment(scenario, PaymentTypeFragment.class);
                onView(withId(R.id.cardPayOnsite)).perform(scrollTo(), click());
                UiTestActions.waitFragment(scenario, OnsitePaymentMethodFragment.class);
                onView(withId(R.id.btnProceedToLocation)).perform(click());
                UiTestActions.waitFragment(scenario, PaymentLocationFragment.class);
                onView(withId(R.id.cardPayAtCounter)).perform(scrollTo(), click());
                onView(withId(R.id.btnSubmitLocation)).perform(click());
                UiTestActions.waitFragment(scenario, OnsiteStatusFragment.class);
                onView(withId(R.id.tvOnsiteTotal)).check(matches(isDisplayed()));
                Bundle progress = new Bundle(); progress.putString("stream", "ONSITE_PENDING session=" + dining.getId() + " part=" + part + "; confirm in cashier browser now\n");
                InstrumentationRegistry.getInstrumentation().sendStatus(2, progress);
                long deadline = System.nanoTime() + TimeUnit.MINUTES.toNanos(5);
                AtomicBoolean success = new AtomicBoolean();
                boolean intermediate = split && part == 1;
                while (!success.get() && System.nanoTime() < deadline) {
                    scenario.onActivity(a -> success.set(intermediate
                        ? a.getSupportFragmentManager().findFragmentById(R.id.fragment_container) instanceof SplitBillFragment
                        : a.getSupportFragmentManager().findFragmentById(R.id.fragment_container) instanceof PaymentSuccessFragment));
                    if (!success.get()) Thread.sleep(250);
                }
                assertTrue("Cashier must confirm within 5 minutes; Android must navigate from backend success", success.get());
                if (intermediate) assertEquals("PAYMENT_REQUESTED", session.getObject("/api/mobile/v1/dining-sessions/" + dining.getId(), true).getString("status"));
                }
                onView(withId(R.id.tvSuccessInvoiceId)).check(matches(isDisplayed()));
                assertEquals("CLOSED", session.getObject("/api/mobile/v1/dining-sessions/" + dining.getId(), true).getString("status"));
                assertTrue(session.getObject("/api/mobile/v1/dining-sessions/" + dining.getId() + "/invoice", true).getInt("invoiceId") > 0);
                onView(withId(R.id.btnReviewMeal)).perform(click());
                UiTestActions.waitFragment(scenario, com.restaurant.viettable.ui.profile.ReviewFragment.class);
                onView(withId(R.id.etReviewComment)).perform(replaceText("ONSITE acceptance review"), closeSoftKeyboard());
                onView(withId(R.id.btnSubmitReview)).perform(click());
                UiTestActions.waitFragment(scenario, com.restaurant.viettable.ui.profile.ReviewHistoryFragment.class);
                UiWaiter.until("review loaded", UiTestActions.activity(scenario), a -> UiWaiter.shown(a.findViewById(R.id.btnEditReview)));
                onView(withId(R.id.btnEditReview)).perform(click());
                UiTestActions.waitFragment(scenario, com.restaurant.viettable.ui.profile.ReviewFragment.class);
                onView(withId(R.id.etReviewComment)).perform(replaceText("Updated onsite acceptance review"), closeSoftKeyboard());
                onView(withId(R.id.btnSubmitReview)).perform(click());
                UiTestActions.waitFragment(scenario, com.restaurant.viettable.ui.profile.ReviewHistoryFragment.class);
                UiWaiter.until("updated review loaded", UiTestActions.activity(scenario), a -> UiWaiter.shown(a.findViewById(R.id.btnDeleteReview)));
                onView(withId(R.id.btnDeleteReview)).perform(click());
                onView(withText("Xóa")).perform(click());
                UiWaiter.until("review deleted", UiTestActions.activity(scenario), a -> UiWaiter.shown(a.findViewById(R.id.tvReviewState)));
                onView(withId(R.id.tvReviewState)).check(matches(withText("Bạn chưa gửi đánh giá nào.")));
            }
        }
    }
}
