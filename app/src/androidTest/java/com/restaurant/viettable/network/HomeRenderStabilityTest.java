package com.restaurant.viettable.network;

import androidx.recyclerview.widget.RecyclerView;
import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.ui.home.HomeFragment;
import org.junit.Test;
import org.junit.runner.RunWith;
import java.util.concurrent.atomic.AtomicReference;
import static org.junit.Assert.assertSame;
import static org.junit.Assume.assumeTrue;

/** A session/profile state update must not recreate the three Catalog lists. */
@RunWith(AndroidJUnit4.class)
public class HomeRenderStabilityTest {
    @Test public void unrelatedStateUpdateKeepsCatalogAdapters() throws Exception {
        try (ActivityScenario<MainActivity> scenario = ActivityScenario.launch(MainActivity.class)) {
            AtomicReference<Boolean> signedIn = new AtomicReference<>(false);
            scenario.onActivity(activity -> signedIn.set(AppStateManager.getInstance().isLoggedIn()));
            assumeTrue("No isolated QA session installed", signedIn.get());
            AtomicReference<RecyclerView.Adapter<?>> featured = new AtomicReference<>();
            AtomicReference<RecyclerView.Adapter<?>> best = new AtomicReference<>();
            AtomicReference<RecyclerView.Adapter<?>> recommendations = new AtomicReference<>();
            long deadline = android.os.SystemClock.elapsedRealtime() + 15000;
            while (featured.get() == null && android.os.SystemClock.elapsedRealtime() < deadline) {
                scenario.onActivity(activity -> {
                    if (!(activity.getSupportFragmentManager().findFragmentById(R.id.fragment_container)
                            instanceof HomeFragment)) return;
                    RecyclerView list = activity.findViewById(R.id.rvFeaturedFoods);
                    if (list == null || list.getAdapter() == null || list.getAdapter().getItemCount() == 0) return;
                    featured.set(list.getAdapter());
                    best.set(((RecyclerView) activity.findViewById(R.id.rvBestSellers)).getAdapter());
                    recommendations.set(((RecyclerView) activity.findViewById(R.id.rvRecommendations)).getAdapter());
                });
                Thread.sleep(50);
            }
            assumeTrue("QA Catalog is not populated", featured.get() != null);
            scenario.onActivity(activity -> {
                AppStateManager.getInstance().notifyStateChanged();
                assertSame(featured.get(), ((RecyclerView) activity.findViewById(R.id.rvFeaturedFoods)).getAdapter());
                assertSame(best.get(), ((RecyclerView) activity.findViewById(R.id.rvBestSellers)).getAdapter());
                assertSame(recommendations.get(), ((RecyclerView) activity.findViewById(R.id.rvRecommendations)).getAdapter());
            });
        }
    }
}
