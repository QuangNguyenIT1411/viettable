package com.restaurant.viettable.design;

import static org.junit.Assume.assumeTrue;

import android.os.Bundle;
import android.os.SystemClock;
import android.view.View;

import androidx.fragment.app.Fragment;
import androidx.test.core.app.ActivityScenario;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;

import com.restaurant.viettable.MainActivity;
import com.restaurant.viettable.R;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.data.ThemeStore;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.SupportChat;
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.ui.profile.SupportChatFragment;
import com.restaurant.viettable.ui.profile.SupportListFragment;
import com.restaurant.viettable.ui.profile.SupportStartFragment;
import com.restaurant.viettable.util.SupportText;
import com.restaurant.viettable.util.ThemeChoice;

import org.json.JSONObject;
import org.junit.Test;
import org.junit.runner.RunWith;

import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.time.Instant;
import java.time.ZoneId;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

/**
 * Manual design check (owner 06/11), not part of the regular suite: the support chat screens filled offline with
 * made-up data shaped exactly like the server's answer. Run only with
 * -e designPreview true -e screen start|confirmed|chat|list [-e theme light|dark].
 */
@RunWith(AndroidJUnit4.class)
public class SupportChatDesignPreviewTest {
    private static final String[][] TOPICS = {
            {"DEPOSIT_TRANSFER", "Đã chuyển khoản đặt cọc nhưng chưa được xác nhận", "DEPOSIT", "1"},
            {"BILL_TRANSFER", "Đã chuyển khoản thanh toán nhưng chưa được xác nhận", "PAYMENT", "1"},
            {"BOOKING_CHANGE", "Đổi / huỷ đặt bàn", "RESERVATION", "0"},
            {"DEPOSIT_REFUND", "Hoàn tiền cọc", "DEPOSIT", "0"},
            {"BILL_WRONG", "Hoá đơn sai món / sai tiền", "INVOICE", "0"},
            {"LOYALTY_PROMO", "Điểm thưởng / voucher / khuyến mãi không áp được", "INVOICE", "0"},
            {"OTHER", "Khác", null, "0"}};

    @Test
    public void showScreenWithSampleData() throws Exception {
        Bundle args = InstrumentationRegistry.getArguments();
        assumeTrue("design preview only", "true".equals(args.getString("designPreview")));
        String screen = args.getString("screen", "start");
        long holdMs = Long.parseLong(args.getString("holdMs", "15000"));
        ThemeStore.getInstance(InstrumentationRegistry.getInstrumentation().getTargetContext())
                .save("light".equals(args.getString("theme")) ? ThemeChoice.LIGHT : ThemeChoice.DARK);
        InstrumentationRegistry.getInstrumentation().runOnMainSync(() ->
                ThemeStore.getInstance(InstrumentationRegistry.getInstrumentation().getTargetContext()).apply());
        Branch q1 = new Branch("1", "Chi nhánh Quận 1", "123 Nguyễn Huệ, Quận 1", "", "", 0, true, "", new ArrayList<>(), 0, 0, 0, 0);
        Branch bt = new Branch("2", "Chi nhánh Bình Thạnh", "789 Điện Biên Phủ", "", "", 0, true, "", new ArrayList<>(), 0, 0, 0, 0);
        try (ActivityScenario<MainActivity> scenario = ActivityScenario.launch(MainActivity.class)) {
            SystemClock.sleep(8000);
            for (int round = 0; round < 3; round++) {
                final boolean[] onScreen = {false};
                scenario.onActivity(activity -> {
                    Fragment top = activity.getSupportFragmentManager().findFragmentById(R.id.fragment_container);
                    onScreen[0] = top != null && top.getClass() == targetClass(screen);
                });
                if (round > 0 && onScreen[0]) break;
                scenario.onActivity(activity -> {
                    AppStateManager state = AppStateManager.getInstance();
                    state.replaceBranches(new ArrayList<>(Arrays.asList(q1, bt)));
                    state.setSelectedBranch(q1);
                    state.setCurrentUser(new User("1", "Nguyễn Vinh Quang", "", "0901234567", "GOLD", 3250, "1"));
                    activity.navigateTo(fragmentFor(screen), true);
                });
                SystemClock.sleep(9000); // the server is not reachable from the preview: let the error land, then fill
            }
            scenario.onActivity(activity -> {
                try { fill(screen, activity, Arrays.asList(q1, bt)); } catch (Exception e) { throw new RuntimeException(e); }
            });
            // A late "no server" error from the screen's own load may land after the fill: hide it again.
            SystemClock.sleep(6000);
            scenario.onActivity(activity -> {
                hide(activity, R.id.tvState); hide(activity, R.id.btnRetry); hide(activity, R.id.progress);
                if (!"chat".equals(screen) && !"list".equals(screen)) {
                    android.widget.ScrollView scroll = activity.findViewById(R.id.scroll);
                    scroll.scrollTo(0, scroll.getChildAt(0).getHeight());
                }
            });
            SystemClock.sleep(holdMs);
        }
    }

    private static Class<?> targetClass(String screen) {
        switch (screen) {
            case "chat": return SupportChatFragment.class;
            case "list": return SupportListFragment.class;
            default: return SupportStartFragment.class;
        }
    }

    private static Fragment fragmentFor(String screen) {
        switch (screen) {
            case "chat": return SupportChatFragment.newInstance(7);
            case "list": return SupportListFragment.newInstance();
            default: return SupportStartFragment.newInstance();
        }
    }

    private static void fill(String screen, MainActivity a, List<Branch> branches) throws Exception {
        Fragment f = a.getSupportFragmentManager().findFragmentById(R.id.fragment_container);
        hide(a, R.id.tvState); hide(a, R.id.btnRetry); hide(a, R.id.progress);
        switch (screen) {
            case "chat": {
                SupportChat.Detail d = SupportChat.Detail.parse(new JSONObject(chatJson()));
                set(f, "conversation", d.conversation);
                set(f, "messages", new ArrayList<>(d.messages));
                set(f, "hasOlder", true);
                call(f, "render", new Class<?>[]{});
                break;
            }
            case "list": {
                android.widget.LinearLayout rows = a.findViewById(R.id.layoutRows);
                rows.removeAllViews();
                for (String json : new String[]{conv(9, "Waiting", 0, "BILL_TRANSFER", "Đã chuyển khoản thanh toán nhưng chưa được xác nhận",
                        "Chuyển khoản 1.250.000đ · 19:42 06/11", "Tôi đã chuyển 1.250.000đ với nội dung \"VT B1 TT58\" nhưng chưa được xác nhận.", 600),
                        conv(7, "InProgress", 1, "DEPOSIT_TRANSFER", "Đã chuyển khoản đặt cọc nhưng chưa được xác nhận",
                                "Cọc đặt bàn #37 · 300.000đ", "Mình đã kiểm tra, cọc của anh/chị đã được ghi nhận ạ.", 3600),
                        conv(3, "Resolved", 0, "OTHER", "Khác", null, "Cảm ơn bạn đã góp ý.", 3 * 86400)}) {
                    rows.addView((View) call(f, "row", new Class<?>[]{SupportChat.Conversation.class, ZoneId.class},
                            SupportChat.Conversation.parse(new JSONObject(json)), ZoneId.systemDefault()));
                }
                break;
            }
            default: {
                List<SupportChat.Topic> topics = new ArrayList<>();
                for (String[] t : TOPICS) topics.add(new SupportChat.Topic(t[0], t[1], t[2], "1".equals(t[3])));
        @SuppressWarnings("unchecked") List<SupportChat.Topic> ft = (List<SupportChat.Topic>) get(f, "topics");
                ft.clear(); ft.addAll(topics);
                @SuppressWarnings("unchecked") List<Branch> fb = (List<Branch>) get(f, "branches");
                fb.clear(); fb.addAll(branches);
                set(f, "branchId", 1);
                a.findViewById(R.id.layoutForm).setVisibility(View.VISIBLE);
                call(f, "renderBranches", new Class<?>[]{});
                set(f, "topic", topics.get(0));
                call(f, "renderTopics", new Class<?>[]{});
                List<SupportChat.Option> options = Arrays.asList(
                        new SupportChat.Option("DEPOSIT", 37, 1, "Cọc đặt bàn #37 · 300.000đ", "Chưa nhận được tiền cọc · 19:00 07/11"),
                        new SupportChat.Option("DEPOSIT", 35, 1, "Cọc đặt bàn #35 · 350.000đ", "Đã nhận tiền cọc · 18:30 05/11"));
                set(f, "option", options.get("confirmed".equals(screen) ? 1 : 0));
                android.widget.TextView title = a.findViewById(R.id.tvOptionsTitle);
                title.setText("CHỌN KHOẢN ĐẶT CỌC");
                title.setVisibility(View.VISIBLE);
                call(f, "renderOptions", new Class<?>[]{List.class}, options);
                a.findViewById(R.id.cardCheck).setVisibility(View.VISIBLE);
                android.widget.TextView check = a.findViewById(R.id.tvCheck);
                if ("confirmed".equals(screen)) {
                    SupportChat.Check ok = new SupportChat.Check(true, Instant.now().minusSeconds(26 * 3600).toString(), 350000, "VT DC35", "Đã nhận tiền cọc", 1);
                    check.setText(SupportText.confirmedText(ok, ZoneId.systemDefault()));
                    a.findViewById(R.id.layoutCheckActions).setVisibility(View.VISIBLE);
                } else {
                    SupportChat.Check wait = new SupportChat.Check(false, null, 300000, "VT DC37", "Chưa nhận được tiền cọc", 1);
                    check.setText(SupportText.notConfirmedText(wait));
                    call(f, "showMessageBox", new Class<?>[]{String.class}, SupportText.suggestedMessage(topics.get(0), options.get(0), wait));
                }
            }
        }
    }

    private static String chatJson() {
        Instant now = Instant.now();
        return "{\"conversation\":{\"supportConversationId\":7,\"branchId\":1,\"topic\":\"DEPOSIT_TRANSFER\","
                + "\"topicLabel\":\"Đã chuyển khoản đặt cọc nhưng chưa được xác nhận\",\"contextType\":\"DEPOSIT\",\"contextId\":37,"
                + "\"contextLabel\":\"Cọc đặt bàn #37 · 300.000đ\",\"status\":\"InProgress\",\"unread\":0,"
                + "\"customerLastReadMessageId\":5,\"staffLastReadMessageId\":3},"
                + "\"messages\":["
                + msg(3, "CUSTOMER", null, "Tôi đã chuyển 300.000đ với nội dung \\\"VT DC37\\\" nhưng chưa được xác nhận.", now.minusSeconds(1500))
                + "," + msg(4, "AUTO", null, "Tin nhắn tự động: Nhân viên sẽ trả lời bạn sau ít phút.", now.minusSeconds(1500))
                + "," + msg(5, "STAFF", "Lan", "Chào anh/chị, mình đang kiểm tra sao kê. Anh/chị chuyển từ ngân hàng nào ạ?", now.minusSeconds(900))
                + "," + msg(6, "CUSTOMER", null, "Mình chuyển từ Vietcombank lúc 19:05.", now.minusSeconds(600))
                + "],\"hasOlder\":true}";
    }

    private static String msg(long id, String sender, String name, String body, Instant at) {
        return "{\"supportMessageId\":" + id + ",\"senderType\":\"" + sender + "\"," + (name == null ? "" : "\"senderName\":\"" + name + "\",")
                + "\"body\":\"" + body + "\",\"createdAtUtc\":\"" + at + "\"}";
    }

    private static String conv(int id, String status, int unread, String topic, String label, String context, String last, long agoSeconds) {
        return "{\"supportConversationId\":" + id + ",\"branchId\":1,\"topic\":\"" + topic + "\",\"topicLabel\":\"" + label + "\","
                + (context == null ? "" : "\"contextLabel\":\"" + context + "\",") + "\"status\":\"" + status + "\",\"unread\":" + unread + ","
                + "\"lastMessageAtUtc\":\"" + Instant.now().minusSeconds(agoSeconds) + "\","
                + "\"lastMessage\":{\"body\":\"" + last.replace("\"", "\\\"") + "\"}}";
    }

    private static void hide(MainActivity a, int id) { View v = a.findViewById(id); if (v != null) v.setVisibility(View.GONE); }

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

    private static Object call(Object target, String name, Class<?>[] types, Object... args) throws Exception {
        Method method = target.getClass().getDeclaredMethod(name, types);
        method.setAccessible(true);
        return method.invoke(target, args);
    }
}
