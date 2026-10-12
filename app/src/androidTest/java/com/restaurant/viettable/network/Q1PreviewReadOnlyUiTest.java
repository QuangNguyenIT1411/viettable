package com.restaurant.viettable.network;

import android.os.SystemClock;
import android.util.Log;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;
import androidx.fragment.app.Fragment;
import androidx.recyclerview.widget.RecyclerView;
import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import com.google.android.material.tabs.TabLayout;
import com.restaurant.viettable.BuildConfig;
import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.data.AppStateManager;
import org.json.JSONObject;
import org.junit.Test;
import org.junit.runner.RunWith;
import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.Predicate;
import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertTrue;
import static org.junit.Assume.assumeTrue;

/** Uses only the completed Q1 customer fixture. Never creates an order or payment. */
@RunWith(AndroidJUnit4.class)
public class Q1PreviewReadOnlyUiTest {
    @Test public void loginCatalogAndCompletedHistories() throws Exception {
        assumeTrue(BuildConfig.DEBUG && BuildConfig.API_BASE_URL.equals("http://127.0.0.1:48127"));
        JSONObject credential = receiveOneTimeCredential();
        String username = credential.getString("username");
        String password = credential.getString("password");
        assertEquals("q1uat_20261012", username);
        long started = SystemClock.elapsedRealtime();
        try (ActivityScenario<MainActivity> scenario = ActivityScenario.launch(MainActivity.class)) {
            waitFor(scenario, "welcome", activity -> currentIs(activity,
                    com.restaurant.viettable.ui.auth.WelcomeFragment.class));
            scenario.onActivity(activity -> activity.findViewById(R.id.btnLogin).performClick());
            waitFor(scenario, "login", activity -> currentIs(activity,
                    com.restaurant.viettable.ui.auth.LoginFragment.class));
            final String passwordForForm = password;
            scenario.onActivity(activity -> {
                ((EditText) activity.findViewById(R.id.etEmail)).setText(username);
                ((EditText) activity.findViewById(R.id.etPassword)).setText(passwordForForm);
                activity.findViewById(R.id.btnSubmitLogin).performClick();
            });
            credential = null;
            password = null;
            waitFor(scenario, "authenticated branch screen", activity -> currentIs(activity,
                    com.restaurant.viettable.ui.branch.BranchListFragment.class));
            Log.i("Q1UiPerf", "loginToBranchMs=" + (SystemClock.elapsedRealtime() - started));

            waitFor(scenario, "real Q1 branch list", activity ->
                    AppStateManager.getInstance().getBranches().stream()
                            .anyMatch(branch -> "1".equals(branch.getId())));
            scenario.onActivity(activity -> ((EditText) activity.findViewById(R.id.etSearchBranch)).setText("Quận 1"));
            waitFor(scenario, "filtered Q1 branch", activity -> {
                RecyclerView rows = activity.findViewById(R.id.rvBranches);
                return rows != null && rows.getAdapter() != null && rows.getAdapter().getItemCount() == 1;
            });
            scenario.onActivity(a -> {
                RecyclerView rows = a.findViewById(R.id.rvBranches);
                rows.scrollToPosition(0);
                RecyclerView.ViewHolder holder = rows.findViewHolderForAdapterPosition(0);
                assertNotNull("Q1 branch row", holder);
                holder.itemView.findViewById(R.id.btnSelectBranch).performClick();
            });
            waitFor(scenario, "real Home catalog", activity -> {
                if (!currentIs(activity, com.restaurant.viettable.ui.home.HomeFragment.class)) return false;
                LinearLayout categories = activity.findViewById(R.id.layoutCategoryChips);
                RecyclerView foods = activity.findViewById(R.id.rvFeaturedFoods);
                TextView branch = activity.findViewById(R.id.tvHeaderBranchName);
                return branch != null && branch.isShown() && categories != null && categories.getChildCount() > 0
                        && foods != null && foods.getAdapter() != null && foods.getAdapter().getItemCount() > 0;
            });
            assertEquals("1", AppStateManager.getInstance().getSelectedBranch().getId());
            assertTrue(AppStateManager.getInstance().getFoodItems().size() > 0);
            Log.i("Q1UiPerf", "catalogUsableMs=" + (SystemClock.elapsedRealtime() - started));

            scenario.onActivity(activity -> activity.findViewById(R.id.btnQuickMenu).performClick());
            waitFor(scenario, "Menu screen", activity -> currentIs(activity,
                    com.restaurant.viettable.ui.food.MenuFragment.class));
            scenario.onActivity(MainActivity::navigateBack);
            waitFor(scenario, "Home return", activity -> currentIs(activity,
                    com.restaurant.viettable.ui.home.HomeFragment.class));
            scenario.onActivity(activity -> activity.switchTab(R.id.nav_orders));
            waitFor(scenario, "Order screen", activity -> currentIs(activity,
                    com.restaurant.viettable.ui.order.OrderHistoryFragment.class));
            selectTab(scenario, 1);
            waitFor(scenario, "Q1 completed session visible", activity -> {
                TextView title = activity.findViewById(R.id.tvHistorySessionTitle);
                return title != null && title.isShown() && title.getText().toString().contains("Phiên #24");
            });
            Log.i("Q1UiPerf", "sessionHistoryUsableMs=" + (SystemClock.elapsedRealtime() - started));
            selectTab(scenario, 2);
            waitFor(scenario, "Q1 invoice visible", activity -> {
                TextView title = activity.findViewById(R.id.tvHistoryInvoiceCode);
                return title != null && title.isShown()
                        && title.getText().toString().contains("INV-17aeec74d2ca432d847b7c01e606172e");
            });
            Log.i("Q1UiPerf", "invoiceHistoryUsableMs=" + (SystemClock.elapsedRealtime() - started));
        }
    }

    private static void selectTab(ActivityScenario<MainActivity> scenario, int index) {
        scenario.onActivity(activity -> {
            TabLayout tabs = activity.findViewById(R.id.tabLayoutOrderHistory);
            assertNotNull(tabs);
            TabLayout.Tab tab = tabs.getTabAt(index);
            assertNotNull(tab);
            tab.select();
        });
    }

    private static boolean currentIs(MainActivity activity, Class<? extends Fragment> type) {
        return type.isInstance(activity.getSupportFragmentManager().findFragmentById(R.id.fragment_container));
    }

    private static void waitFor(ActivityScenario<MainActivity> scenario, String stage,
                                Predicate<MainActivity> condition) throws Exception {
        long deadline = SystemClock.elapsedRealtime() + 65000;
        while (SystemClock.elapsedRealtime() < deadline) {
            AtomicReference<Boolean> matched = new AtomicReference<>(false);
            scenario.onActivity(activity -> matched.set(condition.test(activity)));
            if (matched.get()) return;
            Thread.sleep(100); // Test thread only; UI callbacks continue on the main looper.
        }
        throw new AssertionError("Q1 UI stage timed out: " + stage);
    }

    private static JSONObject receiveOneTimeCredential() throws Exception {
        HttpURLConnection connection = (HttpURLConnection) new URL("http://127.0.0.1:48128/qa-customer-credential").openConnection();
        connection.setConnectTimeout(5000);
        connection.setReadTimeout(5000);
        try {
            assertEquals(200, connection.getResponseCode());
            ByteArrayOutputStream bytes = new ByteArrayOutputStream();
            try (InputStream stream = connection.getInputStream()) {
                byte[] chunk = new byte[1024];
                int n;
                while ((n = stream.read(chunk)) != -1) bytes.write(chunk, 0, n);
            }
            JSONObject credential = new JSONObject(bytes.toString(StandardCharsets.UTF_8.name()));
            byte[] data = bytes.toByteArray();
            java.util.Arrays.fill(data, (byte) 0);
            return credential;
        } finally {
            connection.disconnect();
        }
    }
}
