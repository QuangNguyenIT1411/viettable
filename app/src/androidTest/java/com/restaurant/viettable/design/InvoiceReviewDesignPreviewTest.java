package com.restaurant.viettable.design;

import static org.junit.Assume.assumeTrue;

import android.os.Bundle;
import android.os.SystemClock;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.widget.TextViewCompat;
import androidx.fragment.app.Fragment;
import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;

import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.adapter.ReceiptHistoryAdapter;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.model.Receipt;
import com.restaurant.viettable.model.ReviewDraft;
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.ui.food.FoodDetailFragment;
import com.restaurant.viettable.ui.payment.InvoiceHistoryDetailFragment;
import com.restaurant.viettable.ui.payment.InvoiceHistoryFragment;
import com.restaurant.viettable.ui.profile.ReviewFragment;

import org.json.JSONObject;
import org.junit.Test;
import org.junit.runner.RunWith;

import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

/**
 * Manual design check (owner 05/11), not part of the regular suite: "Hoá đơn của tôi", "Chi tiết hoá đơn", "Đánh giá"
 * and the dish rating on "Chi tiết món" with made-up, offline data (the screens are filled exactly as the server's
 * answer would fill them). Run only with -e designPreview true -e screen invoices|invoice|review|dish.
 */
@RunWith(AndroidJUnit4.class)
public class InvoiceReviewDesignPreviewTest {
    private static final String[] RECEIPTS = {
        "{\"invoiceId\":46,\"snapshotVersion\":1,\"branchId\":1,\"branchName\":\"Chi nhánh Quận 1\",\"branchAddress\":\"123 Nguyễn Huệ, Quận 1, TP.HCM\","
            + "\"tableName\":\"Bàn 6\",\"sessionStartedAtUtc\":\"2026-11-05T12:02:00Z\",\"paidAtUtc\":\"2026-11-05T13:15:00Z\",\"issuedAtUtc\":\"2026-11-05T13:15:00Z\","
            + "\"cashierName\":\"Lan\",\"items\":[{\"dishId\":1,\"dishName\":\"Bún chả Hà Nội\",\"quantity\":2,\"unitPrice\":65000,\"lineTotal\":130000},"
            + "{\"dishId\":2,\"dishName\":\"Phở bò tái\",\"quantity\":1,\"unitPrice\":75000,\"lineTotal\":75000},"
            + "{\"dishId\":5,\"dishName\":\"Chè khúc bạch\",\"quantity\":2,\"unitPrice\":35000,\"lineTotal\":70000}],\"itemCount\":5,"
            + "\"subtotal\":275000,\"promotionName\":\"Khai trương\",\"promotionDiscount\":27500,\"pointsUsed\":1000,\"pointsDiscount\":10000,"
            + "\"serviceFeePercent\":5,\"serviceFeeAmount\":11875,\"vatPercent\":8,\"vatAmount\":19950,\"depositApplied\":0,\"grossTotal\":269325,"
            + "\"totalAmount\":269325,\"amountPaid\":269325,\"paymentMethod\":\"TRANSFER\",\"paymentStatus\":\"SUCCEEDED\",\"bankTransactionRef\":\"FT26110512345\",\"pointsEarned\":269}",
        "{\"invoiceId\":44,\"snapshotVersion\":1,\"branchId\":2,\"branchName\":\"Chi nhánh Bình Thạnh\",\"tableName\":\"Bàn 2\","
            + "\"paidAtUtc\":\"2026-11-02T11:40:00Z\",\"issuedAtUtc\":\"2026-11-02T11:40:00Z\",\"items\":[{\"dishId\":4,\"dishName\":\"Cơm tấm sườn\",\"quantity\":1,\"unitPrice\":55000,\"lineTotal\":55000}],"
            + "\"itemCount\":1,\"subtotal\":55000,\"serviceFeePercent\":0,\"vatPercent\":0,\"totalAmount\":55000,\"paymentMethod\":\"CASH\",\"paymentStatus\":\"SUCCEEDED\"}",
        "{\"invoiceId\":40,\"snapshotVersion\":0,\"branchId\":1,\"branchName\":\"Chi nhánh Quận 1\",\"tableName\":\"Bàn 11\","
            + "\"paidAtUtc\":\"2026-10-30T09:05:00Z\",\"issuedAtUtc\":\"2026-10-30T09:05:00Z\",\"items\":[{\"dishId\":1,\"dishName\":\"Bún chả Hà Nội\",\"quantity\":1,\"unitPrice\":65000,\"lineTotal\":65000}],"
            + "\"itemCount\":1,\"subtotal\":65000,\"serviceFeePercent\":null,\"vatPercent\":null,\"totalAmount\":65000,\"paymentMethod\":\"CASH\",\"paymentStatus\":\"SUCCEEDED\"}"
    };

    @Test
    public void showScreenWithSampleData() throws Exception {
        Bundle args = InstrumentationRegistry.getArguments();
        assumeTrue("design preview only", "true".equals(args.getString("designPreview")));
        String screen = args.getString("screen", "invoices");
        long holdMs = Long.parseLong(args.getString("holdMs", "15000"));
        try (ActivityScenario<MainActivity> scenario = ActivityScenario.launch(MainActivity.class)) {
            SystemClock.sleep(8000);
            scenario.onActivity(activity -> {
                AppStateManager state = AppStateManager.getInstance();
                Branch q1 = new Branch("1", "Chi nhánh Quận 1", "123 Nguyễn Huệ, Quận 1", "", "", 0, true, "", new ArrayList<>(), 0, 0, 0, 0);
                Branch bt = new Branch("2", "Chi nhánh Bình Thạnh", "789 Điện Biên Phủ", "", "", 0, true, "", new ArrayList<>(), 0, 0, 0, 0);
                state.replaceBranches(new ArrayList<>(Arrays.asList(q1, bt)));
                state.setSelectedBranch(q1);
                state.setCurrentUser(new User("1", "Nguyễn Vinh Quang", "", "0901234567", "GOLD", 3250, "1"));
                Fragment fragment;
                switch (screen) {
                    case "invoice": fragment = InvoiceHistoryDetailFragment.newInstance(46); break;
                    case "review": fragment = ReviewFragment.newInstanceForInvoice(46); break;
                    case "dish": fragment = FoodDetailFragment.newInstance(new FoodItem("1", "1", "1", "Bún chả Hà Nội", 65_000, 0, 0,
                            "Chả nướng than hoa, nước mắm chua ngọt pha tại bàn, ăn kèm rau sống và bún tươi trong ngày.", "", "", "", 1, true, false,
                            Arrays.asList("Tiêu chuẩn", "Lớn"), new ArrayList<>(), null, R.drawable.food_bun_cha)); break;
                    default: fragment = InvoiceHistoryFragment.newInstance();
                }
                activity.navigateTo(fragment, true);
            });
            SystemClock.sleep(7000); // the server is not reachable from the preview: wait for the error, then fill
            scenario.onActivity(activity -> {
                try { fill(screen, activity); } catch (Exception e) { throw new RuntimeException(e); }
            });
            SystemClock.sleep(holdMs);
        }
    }

    private static void fill(String screen, MainActivity a) throws Exception {
        Fragment f = a.getSupportFragmentManager().findFragmentById(R.id.fragment_container);
        List<Receipt> receipts = new ArrayList<>();
        for (String json : RECEIPTS) receipts.add(Receipt.fromJson(new JSONObject(json)));
        switch (screen) {
            case "invoice": {
                set(f, "receipt", receipts.get(0));
                call(f, "render", new Class<?>[]{Receipt.class}, receipts.get(0));
                TextView button = a.findViewById(R.id.btnInvoiceReview);
                button.setVisibility(View.VISIBLE);
                button.setText("Xem đánh giá");
                TextView note = a.findViewById(R.id.tvInvoiceReviewNote);
                note.setVisibility(View.VISIBLE);
                note.setText("Nhà hàng đã trả lời đánh giá của bạn.");
                break;
            }
            case "review": {
                set(f, "receipt", receipts.get(0));
                ReviewDraft draft = new ReviewDraft();
                draft.overall = 5; draft.food = 5; draft.service = 4; draft.space = 4;
                draft.tags.add("Ngon"); draft.tags.add("Đáng tiền");
                draft.dishRatings.put(1, 5); draft.dishRatings.put(2, 4);
                draft.comment = "Bún chả nướng thơm, nước chấm vừa miệng. Phục vụ nhanh.";
                set(f, "draft", draft);
                call(f, "render", new Class<?>[]{List.class},
                        Arrays.asList("Ngon", "Nhanh", "Đáng tiền", "Phục vụ tốt", "Không gian đẹp", "Sạch sẽ", "Cần cải thiện", "Chờ lâu"));
                break;
            }
            case "dish": {
                a.findViewById(R.id.tvDishReviewHeading).setVisibility(View.VISIBLE);
                a.findViewById(R.id.layoutDishReviews).setVisibility(View.VISIBLE);
                ((TextView) a.findViewById(R.id.tvDishReviewSummary)).setText("★ 4,6 / 5 · 12 lượt đánh giá");
                ((TextView) a.findViewById(R.id.tvFoodRating)).setText("4,6 (12)");
                a.findViewById(R.id.layoutFoodRating).setVisibility(View.VISIBLE);
                LinearLayout list = a.findViewById(R.id.layoutDishReviewList);
                list.removeAllViews();
                for (String text : new String[]{"★5 · Vinh Q.\nChả nướng thơm, nước chấm vừa miệng.\nNhà hàng: Cảm ơn bạn đã ghé!",
                        "★4 · Khách ẩn danh\nNgon, hơi đợi lâu giờ cao điểm."}) {
                    TextView row = new TextView(a);
                    TextViewCompat.setTextAppearance(row, R.style.TextAppearance_VietTable_Body);
                    row.setText(text);
                    row.setPadding(0, 24, 0, 0);
                    list.addView(row);
                }
                break;
            }
            default: {
                set(f, "loadToken", ((Integer) get(f, "loadToken")) + 1);
                @SuppressWarnings("unchecked") List<Receipt> shown = (List<Receipt>) get(f, "receipts");
                shown.clear();
                shown.addAll(receipts);
                ((ReceiptHistoryAdapter) get(f, "adapter")).submit(shown, false);
                a.findViewById(R.id.layoutInvoiceState).setVisibility(View.GONE);
            }
        }
    }

    private static Object get(Object target, String name) throws Exception {
        Field field = target.getClass().getDeclaredField(name);
        field.setAccessible(true);
        return field.get(target);
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
