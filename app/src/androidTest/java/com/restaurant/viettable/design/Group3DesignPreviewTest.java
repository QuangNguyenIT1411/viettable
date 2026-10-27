package com.restaurant.viettable.design;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.os.Bundle;
import android.os.SystemClock;
import android.view.View;
import android.widget.ImageView;

import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;

import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.NotificationItem;
import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.model.OrderItem;
import com.restaurant.viettable.model.PaymentInfo;
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.ui.food.FoodDetailFragment;
import com.restaurant.viettable.ui.order.OrderTrackingFragment;
import com.restaurant.viettable.ui.payment.OnsiteStatusFragment;
import com.restaurant.viettable.ui.payment.PaymentSuccessFragment;

import org.junit.Test;
import org.junit.runner.RunWith;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.Random;

import static org.junit.Assume.assumeTrue;

/**
 * Manual design check, not part of the regular suite: shows one of the four "nhóm 3" screens (Chi tiết món, Tiến độ
 * món, VietQR, Thanh toán thành công) with made-up, offline data and holds it on screen for screenshots.
 * Run only with -e designPreview true -e screen detail|tracking|qr|success (optionally -e holdMs 60000).
 */
@RunWith(AndroidJUnit4.class)
public class Group3DesignPreviewTest {

    @Test
    public void showScreenWithSampleData() throws Exception {
        Bundle args = InstrumentationRegistry.getArguments();
        assumeTrue("design preview only", "true".equals(args.getString("designPreview")));
        String screen = args.getString("screen", "detail");
        long holdMs = Long.parseLong(args.getString("holdMs", "60000"));

        try (ActivityScenario<MainActivity> scenario = ActivityScenario.launch(MainActivity.class)) {
            SystemClock.sleep(5000); // let the splash finish (it moves on to the welcome screen by itself)
            scenario.onActivity(activity -> {
                AppStateManager state = AppStateManager.getInstance();
                Branch branch = new Branch("1", "Chi nhánh Quận 1", "", "", "", 0, true, "",
                        new ArrayList<>(), 0, 0, 0, 0);
                state.setSelectedBranch(branch);
                state.setCurrentUser(new User("1", "Nguyễn Vinh Quang", "", "", "MEMBER", 0, "1"));
                List<FoodItem> foods = new ArrayList<>(Arrays.asList(
                        dish("1", "Bún chả Hà Nội", 65_000, R.drawable.food_bun_cha),
                        dish("2", "Canh chua cá lóc", 89_000, R.drawable.food_pho_bo),
                        dish("3", "Gỏi cuốn tôm thịt", 45_000, R.drawable.food_goi_cuon),
                        dish("4", "Bánh mì chảo", 55_000, R.drawable.food_com_tam),
                        dish("5", "Chè ba màu", 35_000, R.drawable.food_che_khuc_bach)));
                state.replaceBranchCatalog("1", new ArrayList<>(), foods);
                DiningSession session = new DiningSession("20", "1", "Chi nhánh Quận 1", "37",
                        DiningSession.Status.ACTIVE, null, "Nguyễn Vinh Quang", "2026-10-26T12:02:00Z",
                        new ArrayList<>(), new ArrayList<>());
                session.setTableLabel("B4");
                switch (screen) {
                    case "tracking":
                        session.getOrders().add(order(session));
                        state.setActiveDiningSession(session);
                        activity.navigateTo(OrderTrackingFragment.newInstance(), true);
                        break;
                    case "qr":
                        state.setActiveDiningSession(session);
                        activity.navigateTo(OnsiteStatusFragment.newInstance(invoice(), "Tại bàn", payment()), true);
                        break;
                    case "success":
                        state.setActiveDiningSession(session);
                        NotificationItem points = new NotificationItem("9", NotificationItem.Type.PAYMENT, "Điểm thưởng",
                                "Bạn được cộng 245 điểm", "2026-10-26T14:52:00Z", false);
                        points.setEventName("loyalty.points-earned.v1");
                        points.setRefId(77);
                        state.replaceNotifications(Collections.singletonList(points));
                        activity.navigateTo(PaymentSuccessFragment.newInstance(invoice()), true);
                        break;
                    default:
                        activity.navigateTo(FoodDetailFragment.newInstance(foods.get(0)), true);
                }
            });
            SystemClock.sleep(1200);
            if ("qr".equals(screen)) scenario.onActivity(Group3DesignPreviewTest::fillQr);
            SystemClock.sleep(holdMs);
        }
    }

    /** The QR needs the network; show the same views the real code fills, with a made-up pattern instead of a real code. */
    private static void fillQr(MainActivity activity) {
        View root = activity.findViewById(R.id.cardTransferQr);
        if (root == null) return;
        ((ImageView) activity.findViewById(R.id.ivTransferQr)).setImageBitmap(fakeQr());
        activity.findViewById(R.id.tvTransferState).setVisibility(View.GONE);
        ((android.widget.TextView) activity.findViewById(R.id.tvTransferBankAccount)).setText("BIDV · 8830150124");
        ((android.widget.TextView) activity.findViewById(R.id.tvTransferAccountName)).setText("NGUYEN VINH QUANG");
        activity.findViewById(R.id.layoutTransferAccount).setVisibility(View.VISIBLE);
        ((android.widget.TextView) activity.findViewById(R.id.tvTransferContent)).setText("VT0412 BAN B4");
        activity.findViewById(R.id.layoutTransferTear).setVisibility(View.VISIBLE);
        activity.findViewById(R.id.layoutTransferContent).setVisibility(View.VISIBLE);
        activity.findViewById(R.id.btnCopyAmount).setVisibility(View.VISIBLE);
        ((android.widget.TextView) activity.findViewById(R.id.tvTransferHint)).setText("Hệ thống sẽ tự xác nhận khi nhận được tiền.");
        activity.findViewById(R.id.btnSaveTransferQr).setEnabled(true);
        View scan = activity.findViewById(R.id.viewScanLine);
        scan.setVisibility(View.VISIBLE);
        float travel = (220 - 56) * activity.getResources().getDisplayMetrics().density;
        android.animation.ObjectAnimator animator = android.animation.ObjectAnimator.ofFloat(scan, View.TRANSLATION_Y, 0f, travel);
        animator.setDuration(1800);
        animator.setRepeatMode(android.animation.ValueAnimator.REVERSE);
        animator.setRepeatCount(android.animation.ValueAnimator.INFINITE);
        animator.start();
    }

    private static Bitmap fakeQr() {
        int cells = 29, px = 12;
        Bitmap bitmap = Bitmap.createBitmap(cells * px, cells * px, Bitmap.Config.ARGB_8888);
        Canvas canvas = new Canvas(bitmap);
        canvas.drawColor(Color.WHITE);
        Paint ink = new Paint();
        ink.setColor(0xFF1A120C);
        Random random = new Random(3);
        for (int y = 0; y < cells; y++) for (int x = 0; x < cells; x++) {
            boolean finder = (x < 8 && y < 8) || (x >= cells - 8 && y < 8) || (x < 8 && y >= cells - 8);
            if (!finder && random.nextInt(100) < 48) canvas.drawRect(x * px, y * px, (x + 1) * px - 1, (y + 1) * px - 1, ink);
        }
        for (int[] corner : new int[][]{{0, 0}, {cells - 7, 0}, {0, cells - 7}}) {
            canvas.drawRect(corner[0] * px, corner[1] * px, (corner[0] + 7) * px, (corner[1] + 7) * px, ink);
            Paint white = new Paint();
            white.setColor(Color.WHITE);
            canvas.drawRect((corner[0] + 1) * px, (corner[1] + 1) * px, (corner[0] + 6) * px, (corner[1] + 6) * px, white);
            canvas.drawRect((corner[0] + 2) * px, (corner[1] + 2) * px, (corner[0] + 5) * px, (corner[1] + 5) * px, ink);
        }
        return bitmap;
    }

    private static Order order(DiningSession session) {
        List<OrderItem> items = new ArrayList<>(Arrays.asList(
                item("1", "Canh chua cá lóc", 1, OrderItem.Status.SERVED),
                item("2", "Bún chả Hà Nội", 2, OrderItem.Status.READY),
                item("3", "Gỏi cuốn tôm thịt", 1, OrderItem.Status.PREPARING),
                item("4", "Bánh mì chảo", 1, OrderItem.Status.PREPARING),
                item("5", "Chè ba màu", 1, OrderItem.Status.PENDING)));
        Order order = new Order("38", "20", "Chi nhánh Quận 1", "37", Order.Type.DINE_IN, Order.Status.IN_PROGRESS,
                "2026-10-26T14:30:00Z", items, 330_000, "");
        order.setTableLabel("B4");
        return order;
    }

    private static OrderItem item(String id, String name, int quantity, OrderItem.Status status) {
        return new OrderItem(id, name, 65_000, quantity, "", status, "2026-10-26T14:30:00Z");
    }

    private static Invoice invoice() {
        Invoice invoice = new Invoice("412", "20", "Chi nhánh Quận 1", "37", 245_000, 0, 0, 0, 0, 245_000,
                "Chuyển khoản", "Tại bàn", "2026-10-19T14:52:00Z");
        invoice.setTableLabel("Bàn B4");
        invoice.setBackendInvoiceId(412);
        invoice.setBackendPaymentId(77);
        return invoice;
    }

    private static PaymentInfo payment() {
        return new PaymentInfo(77, 5, 20, 245_000, "ONSITE", "TRANSFER", "TABLE", "PENDING", "VND",
                "", "", "", "", "");
    }

    private static FoodItem dish(String id, String name, long price, int image) {
        FoodItem food = new FoodItem(id, "1", "1", name, price, 4.8, 126,
                "Chả nướng than hoa, nước mắm chua ngọt pha tại bàn, ăn kèm rau sống và bún tươi trong ngày.",
                "", "", "", 1, true, false,
                Arrays.asList("Tiêu chuẩn", "Lớn"), new ArrayList<>(Arrays.asList("Thêm chả", "Thêm bún")), null, image);
        return food;
    }
}
