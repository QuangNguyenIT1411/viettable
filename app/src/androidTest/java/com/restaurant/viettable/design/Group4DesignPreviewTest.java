package com.restaurant.viettable.design;

import android.os.Bundle;
import android.os.SystemClock;

import androidx.fragment.app.Fragment;
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
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.ui.food.MenuFragment;
import com.restaurant.viettable.ui.session.DineinCartFragment;
import com.restaurant.viettable.ui.session.DineinMenuFragment;

import org.junit.Test;
import org.junit.runner.RunWith;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

import static org.junit.Assume.assumeTrue;

/**
 * Manual design check, not part of the regular suite: shows one of the "nhóm 4" screens (the rest of the customer app
 * redone in "Phố đêm") with made-up, offline data and holds it on screen for screenshots.
 * Run only with -e designPreview true -e screen &lt;name&gt; (optionally -e holdMs 60000).
 */
@RunWith(AndroidJUnit4.class)
public class Group4DesignPreviewTest {

    @Test
    public void showScreenWithSampleData() throws Exception {
        Bundle args = InstrumentationRegistry.getArguments();
        assumeTrue("design preview only", "true".equals(args.getString("designPreview")));
        String screen = args.getString("screen", "menu");
        long holdMs = Long.parseLong(args.getString("holdMs", "60000"));

        try (ActivityScenario<MainActivity> scenario = ActivityScenario.launch(MainActivity.class)) {
            SystemClock.sleep(8000); // let the splash finish (it moves on to the welcome screen by itself)
            scenario.onActivity(activity -> {
                AppStateManager state = AppStateManager.getInstance();
                Branch branch = new Branch("1", "Chi nhánh Quận 1", "", "", "", 0, true, "",
                        new ArrayList<>(), 0, 0, 0, 0);
                state.setSelectedBranch(branch);
                state.setCurrentUser(new User("1", "Nguyễn Vinh Quang", "", "", "MEMBER", 0, "1"));
                List<FoodItem> foods = foods();
                state.replaceBranchCatalog("1", categories(), foods);
                DiningSession session = new DiningSession("20", "1", "Chi nhánh Quận 1", "37",
                        DiningSession.Status.ACTIVE, null, "Nguyễn Vinh Quang", "2026-10-26T12:02:00Z",
                        new ArrayList<>(), new ArrayList<>());
                session.setTableLabel("B4");
                state.setActiveDiningSession(session);
                activity.navigateTo(screen(screen, state, foods), true);
            });
            SystemClock.sleep(holdMs);
        }
    }

    private static Fragment screen(String screen, AppStateManager state, List<FoodItem> foods) {
        switch (screen) {
            case "dinein":
                state.addToCart(foods.get(0), 2, "Tiêu chuẩn", new ArrayList<>(), 0, "");
                return DineinMenuFragment.newInstance();
            case "cart":
                state.addToCart(foods.get(0), 2, "Tiêu chuẩn", new ArrayList<>(Arrays.asList("Thêm chả")), 0, "");
                state.addToCart(foods.get(2), 1, "Tiêu chuẩn", new ArrayList<>(), 0, "");
                return DineinCartFragment.newInstance();
            default:
                state.addToCart(foods.get(1), 1, "Tiêu chuẩn", new ArrayList<>(), 0, "");
                return new MenuFragment();
        }
    }

    private static List<Category> categories() {
        return new ArrayList<>(Arrays.asList(
                new Category("1", "Món chính", 0, 3),
                new Category("2", "Khai vị", 0, 2),
                new Category("3", "Tráng miệng", 0, 1),
                new Category("4", "Đồ uống", 0, 1)));
    }

    private static List<FoodItem> foods() {
        List<FoodItem> foods = new ArrayList<>(Arrays.asList(
                dish("1", "1", "Bún chả Hà Nội", 65_000, R.drawable.food_bun_cha),
                dish("2", "1", "Phở bò tái", 75_000, R.drawable.food_pho_bo),
                dish("3", "2", "Gỏi cuốn tôm thịt", 45_000, R.drawable.food_goi_cuon),
                dish("4", "1", "Cơm tấm sườn", 55_000, R.drawable.food_com_tam),
                dish("5", "3", "Chè khúc bạch", 35_000, R.drawable.food_che_khuc_bach),
                soldOut(dish("6", "2", "Nem rán", 49_000, R.drawable.food_nem_ran)),
                dish("7", "4", "Cà phê trứng", 39_000, R.drawable.food_cafe_trung),
                dish("8", "1", "Chả cá Lã Vọng", 129_000, R.drawable.food_cha_ca)));
        return foods;
    }

    private static FoodItem soldOut(FoodItem food) {
        return new FoodItem(food.getId(), food.getCategoryId(), "1", food.getName(), food.getPrice(), 4.6, 80,
                "", "", "", "", 0, false, false, food.getAvailableSizes(), food.getAvailableToppings(),
                null, R.drawable.food_nem_ran);
    }

    private static FoodItem dish(String id, String category, String name, long price, int image) {
        return new FoodItem(id, category, "1", name, price, 4.8, 126,
                "Chả nướng than hoa, nước mắm chua ngọt pha tại bàn, ăn kèm rau sống và bún tươi trong ngày.",
                "", "", "", 1, true, false,
                Arrays.asList("Tiêu chuẩn", "Lớn"), new ArrayList<>(Arrays.asList("Thêm chả", "Thêm bún")), null, image);
    }
}
