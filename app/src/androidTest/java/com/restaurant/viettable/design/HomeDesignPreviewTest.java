package com.restaurant.viettable.design;

import android.os.Bundle;
import android.os.SystemClock;

import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;

import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.Category;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.ui.home.HomeFragment;

import org.junit.Test;
import org.junit.runner.RunWith;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;

import static org.junit.Assume.assumeTrue;

/**
 * Manual design check, not part of the regular suite: shows Trang chủ with made-up, offline data (no network,
 * no account) and holds it on screen so screenshots can be taken next to the Figma design.
 * Run only with -e designPreview true (optionally -e seated true, -e holdMs 60000).
 */
@RunWith(AndroidJUnit4.class)
public class HomeDesignPreviewTest {

    @Test
    public void showHomeWithSampleData() throws Exception {
        Bundle args = InstrumentationRegistry.getArguments();
        assumeTrue("design preview only", "true".equals(args.getString("designPreview")));
        boolean seated = "true".equals(args.getString("seated"));
        long holdMs = Long.parseLong(args.getString("holdMs", "60000"));

        try (ActivityScenario<MainActivity> scenario = ActivityScenario.launch(MainActivity.class)) {
            SystemClock.sleep(1500); // let the splash finish
            scenario.onActivity(activity -> {
                AppStateManager state = AppStateManager.getInstance();
                Branch branch = new Branch("1", "Chi nhánh Quận 1", "", "", "", 0, true, "",
                        new ArrayList<>(), 0, 0, 0, 0);
                state.setSelectedBranch(branch);
                state.setCurrentUser(new User("1", "Nguyễn Vinh Quang", "", "", "MEMBER", 0, "1"));
                List<Category> categories = Arrays.asList(new Category("1", "Bún & phở", 0, 3),
                        new Category("2", "Cơm", 0, 2), new Category("3", "Ăn vặt", 0, 3), new Category("4", "Đồ uống", 0, 2));
                List<FoodItem> foods = new ArrayList<>(Arrays.asList(
                        dish("1", "Bún chả Hà Nội", 65_000, R.drawable.food_bun_cha, true, false, "1"),
                        dish("2", "Phở bò tái", 70_000, R.drawable.food_pho_bo, true, false, "1"),
                        dish("3", "Chả cá Lã Vọng", 149_000, R.drawable.food_cha_ca, true, true, "2"),
                        dish("4", "Cơm tấm sườn bì", 55_000, R.drawable.food_com_tam, true, false, "2"),
                        dish("5", "Gỏi cuốn tôm thịt", 45_000, R.drawable.food_goi_cuon, true, false, "3"),
                        dish("6", "Nem rán", 50_000, R.drawable.food_nem_ran, false, false, "3"),
                        dish("7", "Chè khúc bạch", 35_000, R.drawable.food_che_khuc_bach, true, false, "4"),
                        dish("8", "Cà phê trứng", 40_000, R.drawable.food_cafe_trung, true, false, "4"),
                        dish("9", "Lẩu riêu cua", 259_000, R.drawable.food_lau_rieu, true, false, "1"),
                        dish("10", "Trà sen", 30_000, R.drawable.food_tra_sen, true, false, "4")));
                state.replaceBranchCatalog("1", categories, foods);
                if (seated) {
                    DiningSession session = new DiningSession("20", "1", "Chi nhánh Quận 1", "37",
                            DiningSession.Status.ACTIVE, null, "Nguyễn Vinh Quang", "2026-10-26T12:02:00Z",
                            new ArrayList<>(), new ArrayList<>());
                    session.setTableLabel("B4");
                    state.setActiveDiningSession(session);
                    Reservation reservation = new Reservation("21", "1", "Chi nhánh Quận 1", "", "2026-10-28", "19:00",
                            4, "", "5", "", new ArrayList<>(), 0, Reservation.Status.CONFIRMED, false,
                            "Nguyễn Vinh Quang", "");
                    reservation.setTableLabel("Bàn 5");
                    state.replaceReservations(Collections.singletonList(reservation));
                }
                activity.navigateTo(HomeFragment.newInstance(), false);
            });
            SystemClock.sleep(holdMs);
        }
    }

    private static FoodItem dish(String id, String name, long price, int image, boolean available, boolean special,
                                 String categoryId) {
        FoodItem food = new FoodItem(id, categoryId, "1", name, price, 0, 0,
                special ? "Cá lăng nướng nghệ, bún, lạc rang · 2–3 người" : "", "", "", "", 0, available, false,
                Collections.singletonList("Tiêu chuẩn"), new ArrayList<>(), null, image);
        food.setDailySpecial(special);
        return food;
    }
}
