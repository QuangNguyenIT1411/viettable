package com.restaurant.viettable.design;

import android.os.Bundle;
import android.os.SystemClock;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.content.ContextCompat;
import androidx.core.widget.TextViewCompat;
import androidx.recyclerview.widget.RecyclerView;

import androidx.fragment.app.Fragment;
import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;

import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.adapter.VoucherSelectAdapter;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.Category;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.model.OrderItem;
import com.restaurant.viettable.model.Voucher;
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.ui.food.MenuFragment;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.model.ReservationDraft;
import com.restaurant.viettable.model.TableMapItem;
import com.restaurant.viettable.model.NotificationItem;
import com.restaurant.viettable.ui.home.NotificationFragment;
import com.restaurant.viettable.ui.order.OrderHistoryFragment;
import com.restaurant.viettable.ui.payment.InvoiceFragment;
import com.restaurant.viettable.ui.profile.LoyaltyFragment;
import com.restaurant.viettable.ui.profile.ProfileFragment;
import com.restaurant.viettable.ui.reservation.ResConfirmationFragment;
import com.restaurant.viettable.ui.reservation.ResDateFragment;
import com.restaurant.viettable.ui.reservation.ResDetailFragment;
import com.restaurant.viettable.ui.reservation.ResGuestCountFragment;
import com.restaurant.viettable.ui.reservation.ResHistoryFragment;
import com.restaurant.viettable.ui.reservation.ResNoteFragment;
import com.restaurant.viettable.ui.reservation.ResTableLayoutFragment;
import com.restaurant.viettable.ui.reservation.ResTimeFragment;
import com.restaurant.viettable.ui.payment.LoyaltySelectFragment;
import com.restaurant.viettable.ui.payment.PaymentTypeFragment;
import com.restaurant.viettable.ui.payment.VoucherSelectFragment;
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
                state.setCurrentUser(new User("1", "Nguyễn Vinh Quang", "", "0901234567", "GOLD", 3250, "1"));
                List<FoodItem> foods = foods();
                state.replaceBranchCatalog("1", categories(), foods);
                DiningSession session = new DiningSession("20", "1", "Chi nhánh Quận 1", "37",
                        DiningSession.Status.ACTIVE, null, "Nguyễn Vinh Quang", "2026-10-26T12:02:00Z",
                        new ArrayList<>(), new ArrayList<>());
                session.setTableLabel("B4");
                state.setActiveDiningSession(session);
                activity.navigateTo(screen(screen, state, foods), true);
            });
            // Screens that need the server get the same views the real code fills, with made-up values.
            SystemClock.sleep(14000);
            scenario.onActivity(activity -> fill(screen, activity));
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
            case "paytype":
                return PaymentTypeFragment.newInstance(invoice());
            case "invoice":
                state.getActiveDiningSession().getOrders().add(order(state.getActiveDiningSession()));
                return InvoiceFragment.newInstance();
            case "loyalty":
                return LoyaltySelectFragment.newInstance(null);
            case "voucher":
                return VoucherSelectFragment.newInstance(0);
            case "resdate":
                return ResDateFragment.newInstance();
            case "restime":
                return ResTimeFragment.newInstance(draft(state));
            case "resguest":
                return ResGuestCountFragment.newInstance(draft(state));
            case "restable":
                return ResTableLayoutFragment.newInstance(draft(state));
            case "resnote":
                return ResNoteFragment.newInstance(draft(state));
            case "resconfirm": {
                Reservation pending = reservation("8899", Reservation.Status.PENDING);
                return ResConfirmationFragment.newInstance(pending);
            }
            case "reshistory":
                state.replaceReservations(new ArrayList<>(Arrays.asList(
                        reservation("8899", Reservation.Status.PENDING),
                        reservation("8870", Reservation.Status.CONFIRMED),
                        reservation("8812", Reservation.Status.REJECTED))));
                return ResHistoryFragment.newInstance();
            case "orders":
                state.getActiveDiningSession().getOrders().add(order(state.getActiveDiningSession()));
                return new OrderHistoryFragment();
            case "notifications": {
                NotificationItem a = new NotificationItem("1", NotificationItem.Type.RESERVATION, "Đặt bàn đã được xác nhận",
                        "Bàn B4 lúc 19:00 ngày 29/10 đã sẵn sàng cho 4 khách.", "2026-10-27T09:12:00Z", false);
                NotificationItem b = new NotificationItem("2", NotificationItem.Type.ORDER, "Bếp đang nấu món của bạn",
                        "Bún chả Hà Nội, Phở bò tái đang được chế biến.", "2026-10-26T12:20:00Z", false);
                NotificationItem c = new NotificationItem("3", NotificationItem.Type.PAYMENT, "Thanh toán thành công",
                        "Hóa đơn #412: 272.500đ. Bạn được cộng 272 điểm.", "2026-10-26T14:52:00Z", true);
                NotificationItem d = new NotificationItem("4", NotificationItem.Type.PROMOTION, "Ưu đãi cuối tuần",
                        "Giảm 50.000đ cho hóa đơn từ 400.000đ.", "2026-10-25T08:00:00Z", true);
                state.replaceNotifications(new ArrayList<>(Arrays.asList(a, b, c, d)));
                return new NotificationFragment();
            }
            case "profile":
                state.setLoggedIn(true);
                return new ProfileFragment();
            case "loyalty-card":
                return new LoyaltyFragment();
            case "resdetail": {
                Reservation rejected = reservation("8812", Reservation.Status.REJECTED);
                rejected.setStatusReason("Nhà hàng đã kín chỗ khung giờ này");
                return ResDetailFragment.newInstance(rejected);
            }
            default:
                state.addToCart(foods.get(1), 1, "Tiêu chuẩn", new ArrayList<>(), 0, "");
                return new MenuFragment();
        }
    }

    private static void fill(String screen, MainActivity a) {
        switch (screen) {
            case "paytype":
                show(a, R.id.cardPayTransfer);
                break;
            case "invoice": {
                text(a, R.id.tvInvoiceBranch, "Chi nhánh Quận 1");
                text(a, R.id.tvInvoiceSessionInfo, "B4 • Phiên #20 • 26/10/2026 19:02");
                LinearLayout rows = a.findViewById(R.id.layoutInvoiceItems);
                if (rows != null && rows.getChildCount() == 0) {
                    String[][] lines = {{"2x Bún chả Hà Nội", "130.000đ"}, {"1x Phở bò tái", "75.000đ"}, {"1x Gỏi cuốn tôm thịt", "45.000đ"}};
                    for (String[] line : lines) rows.addView(invoiceRow(a, line[0], line[1]));
                }
                text(a, R.id.tvInvoiceSubtotal, "250.000đ");
                text(a, R.id.tvInvoiceServiceFee, "12.500đ");
                text(a, R.id.tvInvoiceVat, "20.000đ");
                show(a, R.id.layoutLoyaltyDiscount);
                text(a, R.id.tvLoyaltyDiscountAmount, "-10.000đ");
                text(a, R.id.tvInvoiceGrandTotal, "272.500đ");
                a.findViewById(R.id.btnProceedToPaymentType).setEnabled(true);
                a.findViewById(R.id.btnSplitBill).setEnabled(true);
                break;
            }
            case "loyalty":
                text(a, R.id.tvAvailablePoints, "3.250 điểm");
                show(a, R.id.tvAvailableMoneyEquivalent);
                text(a, R.id.tvAvailableMoneyEquivalent, "~27.250 đ");
                text(a, R.id.btnMinusPoints, "-1.000");
                text(a, R.id.btnPlusPoints, "+1.000");
                text(a, R.id.tvSelectedPoints, "2.000 điểm");
                show(a, R.id.tvPointsCap);
                text(a, R.id.tvPointsCap, "Giảm bằng điểm tối đa 10% hóa đơn");
                show(a, R.id.tvCalculatedDiscount);
                text(a, R.id.tvCalculatedDiscount, "Giảm tương ứng: -20.000 đ");
                show(a, R.id.btnQuick1000);
                show(a, R.id.btnQuick2000);
                text(a, R.id.btnQuick1000, "1.000 điểm");
                text(a, R.id.btnQuick2000, "2.000 điểm");
                a.findViewById(R.id.btnConfirmPoints).setEnabled(true);
                break;
            case "voucher": {
                RecyclerView list = a.findViewById(R.id.rvVouchers);
                if (list != null) {
                    VoucherSelectAdapter adapter = new VoucherSelectAdapter(new ArrayList<>(Arrays.asList(
                            new Voucher("PHODEM50", "Giảm 50.000đ", 50_000, 0, 400_000, "2026-11-30", true, "",
                                    "Áp dụng cho hóa đơn từ 400.000đ", false),
                            new Voucher("BANMOI10", "Giảm 10% cho khách mới", 0, 10, 0, "2026-11-15", true, "",
                                    "Tối đa 30.000đ, mỗi khách một lần", false))), v -> { });
                    list.setAdapter(adapter);
                    list.postDelayed(() -> {
                        View first = list.getChildAt(0);
                        if (first != null) first.performClick();
                    }, 500);
                }
                break;
            }
            case "restable":
                fillTables(a);
                break;
            case "loyalty-card": {
                show(a, R.id.cardTierProgress);
                text(a, R.id.tvCardNumber, "VT 0001 2345");
                text(a, R.id.tvTierProgressTitle, "Tiến độ lên hạng Kim cương");
                text(a, R.id.tvTierProgressPoints, "3.250 / 5.000");
                text(a, R.id.tvTierProgressHint, "Tích lũy thêm 1.750 điểm để lên hạng Kim cương.");
                View bar = a.findViewById(R.id.progressTier);
                if (bar instanceof com.restaurant.viettable.ui.common.PdProgressBar)
                    ((com.restaurant.viettable.ui.common.PdProgressBar) bar).setProgressCompat(65, false);
                show(a, R.id.tvBenefitsTitle);
                text(a, R.id.tvBenefitsTitle, "Đặc quyền hạng Vàng");
                show(a, R.id.cardBenefits);
                LinearLayout benefits = a.findViewById(R.id.layoutBenefits);
                if (benefits != null && benefits.getChildCount() == 0) {
                    for (String line : new String[]{"Tích 1 điểm cho mỗi 1.000đ thanh toán", "Ưu tiên giữ bàn giờ cao điểm"}) {
                        TextView t = new TextView(a);
                        TextViewCompat.setTextAppearance(t, R.style.TextAppearance_VietTable_Body);
                        t.setText("✓  " + line);
                        t.setPadding(0, 8, 0, 8);
                        benefits.addView(t);
                    }
                }
                break;
            }
            default:
                break;
        }
    }

    /** The map needs the server; put sample tables into the fragment's own list and let its code draw them. */
    private static void fillTables(MainActivity a) {
        for (Fragment f : a.getSupportFragmentManager().getFragments()) {
            if (!(f instanceof ResTableLayoutFragment)) continue;
            try {
                java.lang.reflect.Field tables = ResTableLayoutFragment.class.getDeclaredField("tables");
                tables.setAccessible(true);
                @SuppressWarnings("unchecked") List<TableMapItem> list = (List<TableMapItem>) tables.get(f);
                list.clear();
                list.add(new TableMapItem("1", 1, 2, TableMapItem.TOO_SMALL, "Không đủ ghế"));
                list.add(new TableMapItem("2", 2, 2, TableMapItem.TOO_SMALL, "Không đủ ghế"));
                list.add(new TableMapItem("3", 3, 4, TableMapItem.AVAILABLE, ""));
                list.add(new TableMapItem("4", 4, 4, TableMapItem.AVAILABLE, ""));
                list.add(new TableMapItem("5", 5, 4, TableMapItem.BOOKED, "Đã được đặt"));
                list.add(new TableMapItem("6", 6, 4, TableMapItem.IN_USE, "Đang có khách"));
                list.add(new TableMapItem("7", 7, 6, TableMapItem.AVAILABLE, ""));
                list.add(new TableMapItem("8", 8, 6, TableMapItem.INACTIVE, "Tạm ngưng"));
                java.lang.reflect.Field selected = ResTableLayoutFragment.class.getDeclaredField("selectedTableId");
                selected.setAccessible(true);
                selected.set(f, "4");
                java.lang.reflect.Method render = ResTableLayoutFragment.class.getDeclaredMethod("render");
                render.setAccessible(true);
                render.invoke(f);
            } catch (ReflectiveOperationException e) {
                throw new RuntimeException(e);
            }
            show(a, R.id.tvSelectedTableBanner);
            text(a, R.id.tvSelectedTableBanner, "Đã chọn Bàn 4 (4 người)");
            View hide = a.findViewById(R.id.progressTableMap);
            if (hide != null) hide.setVisibility(View.GONE);
            View cont = a.findViewById(R.id.btnContinue);
            if (cont != null) cont.setEnabled(true);
        }
    }

    private static ReservationDraft draft(AppStateManager state) {
        ReservationDraft draft = new ReservationDraft();
        draft.setBranch(state.getSelectedBranch());
        draft.setDate(java.time.LocalDate.now().plusDays(2).toString());
        draft.setTimeSlot("19:00");
        draft.setGuestCount(4);
        draft.setTableId("4");
        draft.setAreaName("Khu cửa sổ");
        return draft;
    }

    private static Reservation reservation(String id, Reservation.Status status) {
        Reservation r = new Reservation(id, "1", "Chi nhánh Quận 1", "12 Lê Lợi, Quận 1", "2026-10-29", "19:00", 4,
                "Khu cửa sổ", "4", "Chuẩn bị thêm 1 ghế trẻ em.", new ArrayList<>(), 0, status, false,
                "Nguyễn Vinh Quang", "0901234567");
        r.setTableLabel("Bàn B4");
        return r;
    }

    private static View invoiceRow(MainActivity a, String name, String price) {
        LinearLayout row = new LinearLayout(a);
        int pad = Math.round(6 * a.getResources().getDisplayMetrics().density);
        row.setPadding(0, pad, 0, pad);
        TextView tvName = new TextView(a);
        tvName.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        TextViewCompat.setTextAppearance(tvName, R.style.TextAppearance_VietTable_Body);
        tvName.setText(name);
        TextView tvPrice = new TextView(a);
        TextViewCompat.setTextAppearance(tvPrice, R.style.TextAppearance_VietTable_Body);
        tvPrice.setTextColor(ContextCompat.getColor(a, R.color.pd_muted));
        tvPrice.setText(price);
        row.addView(tvName);
        row.addView(tvPrice);
        return row;
    }

    private static void show(MainActivity a, int id) {
        View v = a.findViewById(id);
        if (v != null) v.setVisibility(View.VISIBLE);
    }

    private static void text(MainActivity a, int id, CharSequence value) {
        View v = a.findViewById(id);
        if (v instanceof TextView) ((TextView) v).setText(value);
    }

    private static Order order(DiningSession session) {
        List<OrderItem> items = new ArrayList<>(Arrays.asList(
                new OrderItem("1", "Bún chả Hà Nội", 65_000, 2, "", OrderItem.Status.SERVED, "2026-10-26T14:30:00Z"),
                new OrderItem("2", "Phở bò tái", 75_000, 1, "", OrderItem.Status.SERVED, "2026-10-26T14:30:00Z"),
                new OrderItem("3", "Gỏi cuốn tôm thịt", 45_000, 1, "", OrderItem.Status.READY, "2026-10-26T14:30:00Z")));
        Order order = new Order("38", session.getId(), "Chi nhánh Quận 1", "37", Order.Type.DINE_IN,
                Order.Status.IN_PROGRESS, "2026-10-26T14:30:00Z", items, 250_000, "");
        order.setTableLabel("B4");
        return order;
    }

    private static Invoice invoice() {
        Invoice invoice = new Invoice("412", "20", "Chi nhánh Quận 1", "37", 272_500, 0, 0, 0, 0, 272_500,
                "Chuyển khoản", "Tại bàn", "2026-10-26T14:52:00Z");
        invoice.setTableLabel("Bàn B4");
        return invoice;
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
