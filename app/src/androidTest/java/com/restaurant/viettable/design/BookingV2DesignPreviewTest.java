package com.restaurant.viettable.design;

import static org.junit.Assume.assumeTrue;

import android.os.Bundle;
import android.os.SystemClock;

import androidx.fragment.app.Fragment;
import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;

import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.model.AvailabilityAlternatives;
import com.restaurant.viettable.model.AvailabilityOption;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.MenuSuggestion;
import com.restaurant.viettable.model.ReservationDraft;
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.model.WaitlistEntry;
import com.restaurant.viettable.ui.food.MenuSuggestionFragment;
import com.restaurant.viettable.ui.reservation.BookingAssistantFragment;
import com.restaurant.viettable.ui.reservation.ResNoTableFragment;
import com.restaurant.viettable.ui.reservation.WaitlistFragment;

import org.json.JSONObject;
import org.junit.Test;
import org.junit.runner.RunWith;

import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.time.Instant;
import java.time.LocalDate;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

/**
 * Manual design check (owner 06/11), not part of the regular suite: "Gợi ý món", "Trợ lý đặt bàn", "Hết bàn" with
 * suggestions and "Danh sách chờ của tôi", filled offline with made-up data exactly as the server's answer would.
 * Run only with -e designPreview true -e screen suggest|assistant|notable|waitlist.
 */
@RunWith(AndroidJUnit4.class)
public class BookingV2DesignPreviewTest {
    private static final String SUGGESTION = "{\"feasible\":true,\"items\":["
            + "{\"dishId\":5,\"name\":\"Đậu hũ sốt cà\",\"price\":50000,\"quantity\":1,\"course\":\"MAIN\",\"reason\":\"Món chay · đủ cho 1 người\",\"forVegetarians\":true,"
            + "\"alternatives\":[{\"dishId\":6,\"name\":\"Rau xào chay\",\"price\":45000,\"isVegetarian\":true}]},"
            + "{\"dishId\":1,\"name\":\"Bún chả Hà Nội\",\"price\":65000,\"quantity\":1,\"course\":\"MAIN\",\"reason\":\"Món chính · đủ cho 3 người\",\"forVegetarians\":false,"
            + "\"alternatives\":[{\"dishId\":3,\"name\":\"Cơm tấm sườn\",\"price\":55000}]},"
            + "{\"dishId\":2,\"name\":\"Phở bò tái\",\"price\":75000,\"quantity\":2,\"course\":\"MAIN\",\"reason\":\"Món chính · đủ cho 3 người\",\"forVegetarians\":false,\"alternatives\":[]},"
            + "{\"dishId\":8,\"name\":\"Gỏi cuốn tôm thịt\",\"price\":40000,\"quantity\":1,\"course\":\"STARTER\",\"reason\":\"Khai vị cho cả bàn\",\"forVegetarians\":false,\"alternatives\":[]},"
            + "{\"dishId\":10,\"name\":\"Trà đá\",\"price\":5000,\"quantity\":4,\"course\":\"DRINK\",\"reason\":\"Đồ uống cho 4 người\",\"forVegetarians\":false,\"alternatives\":[]}],"
            + "\"subtotal\":325000,\"serviceFeePercent\":5,\"serviceFee\":16250,\"vatPercent\":8,\"vat\":27300,\"total\":368550,"
            + "\"budget\":800000,\"minimumTotal\":null,\"notes\":[\"2 món chưa được gắn loại món nên chưa được gợi ý.\"]}";

    @Test
    public void showScreenWithSampleData() throws Exception {
        Bundle args = InstrumentationRegistry.getArguments();
        assumeTrue("design preview only", "true".equals(args.getString("designPreview")));
        String screen = args.getString("screen", "suggest");
        long holdMs = Long.parseLong(args.getString("holdMs", "15000"));
        Branch q1 = new Branch("1", "Chi nhánh Quận 1", "123 Nguyễn Huệ, Quận 1", "", "", 0, true, "", new ArrayList<>(), 0, 0, 0, 0);
        Branch bt = new Branch("2", "Chi nhánh Bình Thạnh", "789 Điện Biên Phủ", "", "", 0, true, "", new ArrayList<>(), 0, 0, 0, 0);
        try (ActivityScenario<MainActivity> scenario = ActivityScenario.launch(MainActivity.class)) {
            SystemClock.sleep(8000);
            // Without a signed-in user the app may move to "Chào mừng" while starting: open the screen again until it stays.
            for (int round = 0; round < 3; round++) {
                final boolean[] onScreen = {false};
                scenario.onActivity(activity -> {
                    Fragment top = activity.getSupportFragmentManager().findFragmentById(R.id.fragment_container);
                    onScreen[0] = top != null && top.getClass() == targetClass(screen);
                });
                if (round > 0 && onScreen[0]) break;
                scenario.onActivity(activity -> open(activity, screen, q1, bt));
                SystemClock.sleep(9000); // the server is not reachable from the preview: let the error land, then fill
            }
            scenario.onActivity(activity -> {
                try { fill(screen, activity); } catch (Exception e) { throw new RuntimeException(e); }
            });
            SystemClock.sleep(holdMs);
        }
    }

    private static Class<?> targetClass(String screen) {
        switch (screen) {
            case "assistant": return BookingAssistantFragment.class;
            case "notable": return ResNoTableFragment.class;
            case "waitlist": return WaitlistFragment.class;
            default: return MenuSuggestionFragment.class;
        }
    }

    private static void open(MainActivity activity, String screen, Branch q1, Branch bt) {
        {
            {
                AppStateManager state = AppStateManager.getInstance();
                state.replaceBranches(new ArrayList<>(Arrays.asList(q1, bt)));
                state.setSelectedBranch(q1);
                state.setCurrentUser(new User("1", "Nguyễn Vinh Quang", "", "0901234567", "GOLD", 3250, "1"));
                Fragment fragment;
                switch (screen) {
                    case "assistant": fragment = new BookingAssistantFragment(); break;
                    case "notable": {
                        ReservationDraft draft = new ReservationDraft();
                        draft.setBranch(q1);
                        draft.setDate(LocalDate.now().plusDays(1).toString());
                        draft.setTimeSlot("19:00");
                        draft.setGuestCount(6);
                        fragment = ResNoTableFragment.newInstance(draft, null, null);
                        break;
                    }
                    case "waitlist": fragment = new WaitlistFragment(); break;
                    default: fragment = new MenuSuggestionFragment();
                }
                activity.navigateTo(fragment, true);
            }
        }
    }

    private static void fill(String screen, MainActivity a) throws Exception {
        Fragment f = a.getSupportFragmentManager().findFragmentById(R.id.fragment_container);
        String tomorrow = LocalDate.now().plusDays(1).toString();
        switch (screen) {
            case "assistant":
                call(f, "send", new Class<?>[]{String.class}, "tối mai 7 giờ 4 người");
                break;
            case "notable": {
                List<AvailabilityOption> times = Arrays.asList(
                        new AvailabilityOption(null, null, tomorrow, "18:30", 2, null),
                        new AvailabilityOption(null, null, tomorrow, "20:00", 3, null),
                        new AvailabilityOption(null, null, tomorrow, "20:30", 4, null));
                List<AvailabilityOption> branches = Arrays.asList(
                        new AvailabilityOption("2", "Chi nhánh Bình Thạnh", tomorrow, "19:00", 3, 3.4));
                call(f, "show", new Class<?>[]{AvailabilityAlternatives.class}, new AvailabilityAlternatives(false, times, branches));
                a.findViewById(R.id.tvSuggestionState).setVisibility(android.view.View.GONE); // the preview has no server
                break;
            }
            case "waitlist": {
                WaitlistEntry offered = entry("12", "Offered", 0, Instant.now().plusSeconds(3600).toString());
                offered.offeredReservationId = "88"; offered.offeredTableId = "5";
                offered.offeredReservedAtUtc = Instant.now().plusSeconds(3600).toString();
                offered.offerExpiresAtUtc = Instant.now().plusSeconds(8 * 60).toString();
                WaitlistEntry waiting = entry("13", "Waiting", 2, Instant.now().plusSeconds(26 * 3600).toString());
                WaitlistEntry expired = entry("9", "Expired", 0, Instant.now().minusSeconds(24 * 3600).toString());
                expired.closedReason = "Hết 10 phút giữ bàn.";
                call(f, "show", new Class<?>[]{List.class}, Arrays.asList(offered, waiting, expired));
                a.findViewById(R.id.tvWaitlistState).setVisibility(android.view.View.GONE); // the preview has no server
                break;
            }
            default: {
                set(f, "suggestion", MenuSuggestion.parse(new JSONObject(SUGGESTION)));
                ((com.google.android.material.textfield.TextInputEditText) a.findViewById(R.id.etPeople)).setText("4");
                ((com.google.android.material.textfield.TextInputEditText) a.findViewById(R.id.etVegetarians)).setText("1");
                ((com.google.android.material.textfield.TextInputEditText) a.findViewById(R.id.etBudget)).setText("800k");
                call(f, "render", new Class<?>[]{});
                if ("suggest_result".equals(InstrumentationRegistry.getArguments().getString("scroll")))
                    a.findViewById(R.id.scrollSuggestion).postDelayed(() -> {
                        android.widget.ScrollView scroll = a.findViewById(R.id.scrollSuggestion);
                        scroll.fullScroll(android.view.View.FOCUS_DOWN);
                    }, 1500);
            }
        }
    }

    private static WaitlistEntry entry(String id, String status, int position, String start) {
        WaitlistEntry e = new WaitlistEntry();
        e.id = id; e.branchId = "1"; e.branchName = "Chi nhánh Quận 1"; e.status = status; e.position = position; e.partySize = 6;
        e.windowStartUtc = start; e.windowEndUtc = Instant.parse(start).plusSeconds(3600).toString();
        return e;
    }

    private static void set(Object target, String name, Object value) throws Exception {
        Field field = target.getClass().getDeclaredField(name);
        field.setAccessible(true);
        field.set(target, value);
    }

    private static void call(Object target, String name, Class<?>[] types, Object... args) throws Exception {
        Method method = target.getClass().getDeclaredMethod(name, types);
        method.setAccessible(true);
        method.invoke(target, args);
    }
}
