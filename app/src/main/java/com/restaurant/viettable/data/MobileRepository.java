package com.restaurant.viettable.data;

import com.restaurant.viettable.network.ApiClient;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.network.SignalRManager;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.Category;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.model.FaqItem;
import com.restaurant.viettable.model.NotificationItem;
import com.restaurant.viettable.model.CartItem;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.DiningSessionSummary;
import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.model.OrderItem;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.model.ReservationDraft;
import com.restaurant.viettable.model.Review;
import com.restaurant.viettable.model.ServiceRequest;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.InvoiceHistoryItem;
import com.restaurant.viettable.model.PaymentInfo;
import com.restaurant.viettable.model.SplitBill;
import com.restaurant.viettable.model.SplitPaymentResult;
import com.restaurant.viettable.model.Voucher;
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.util.DisplayFormatters;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.UUID;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.OffsetDateTime;
import java.time.ZoneId;
import java.time.format.DateTimeFormatter;
import java.util.Locale;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;

public final class MobileRepository {
    public interface Callback<T> {
        void onSuccess(T value);
        void onError(ApiException error);
    }

    private static final MobileRepository INSTANCE = new MobileRepository();
    private final AppStateManager state = AppStateManager.getInstance();
    private JSONObject currentQuote;
    private int currentPaymentId;
    private SplitBill currentSplitBill;
    private final java.util.Map<String, List<Callback<Void>>> menuRequests = new java.util.HashMap<>();
    private final List<Callback<List<Branch>>> branchRequests = new ArrayList<>();
    private boolean branchesLoading;
    private final TableNumberDirectory tableNumbers = new TableNumberDirectory();
    private final Set<String> tableBranchesLoaded = new HashSet<>();
    private final java.util.Map<String, List<Runnable>> tableRequests = new java.util.HashMap<>();
    private final java.util.Map<String, String> qrTableLabels = new java.util.HashMap<>();
    private final java.util.Map<String, String> qrBranches = new java.util.HashMap<>();

    public String tableLabel(String branchId, String tableId) { return tableNumbers.label(branchId, tableId); }
    public String qrTableLabel(String code) { return qrTableLabels.getOrDefault(code, "Chưa có thông tin bàn"); }
    public String qrBranchName(String code) { return qrBranches.getOrDefault(code, "Chưa có thông tin chi nhánh"); }

    // Enrich display metadata via the existing Gateway Catalog endpoint. A failed metadata GET must
    // never hide a successful business mutation or cause a POST replay; labels then remain unknown.
    private ApiClient.Callback tableAware(ApiClient.Callback callback) {
        return new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                Set<String> branches = new HashSet<>();
                collectTableBranches(body, branches);
                if (branches.isEmpty()) { callback.onSuccess(body); return; }
                int[] remaining = {branches.size()};
                for (String branch : branches) loadTableNumbers(branch, () -> {
                    if (--remaining[0] == 0) callback.onSuccess(body);
                });
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        };
    }

    private void collectTableBranches(Object body, Set<String> branches) {
        if (body instanceof JSONArray) {
            JSONArray rows = (JSONArray) body;
            for (int i = 0; i < rows.length(); i++) collectTableBranches(rows.opt(i), branches);
        } else if (body instanceof JSONObject) {
            JSONObject row = (JSONObject) body;
            if (row.optInt("branchId") > 0) branches.add(String.valueOf(row.optInt("branchId")));
            java.util.Iterator<String> keys = row.keys();
            while (keys.hasNext()) collectTableBranches(row.opt(keys.next()), branches);
        }
    }

    private void loadTableNumbers(String branchId, Runnable done) {
        if (tableBranchesLoaded.contains(branchId)) { done.run(); return; }
        List<Runnable> pending = tableRequests.get(branchId);
        if (pending != null) { pending.add(done); return; }
        pending = new ArrayList<>(); pending.add(done); tableRequests.put(branchId, pending);
        api().get("/api/mobile/v1/branches/" + branchId + "/table-layout", false, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray tables = ((JSONObject) body).optJSONArray("tables");
                if (tables != null) {
                    for (int i = 0; i < tables.length(); i++) {
                        JSONObject table = tables.optJSONObject(i);
                        if (table != null) tableNumbers.put(branchId, String.valueOf(table.optInt("tableId")), table.optInt("tableNumber"));
                    }
                    tableBranchesLoaded.add(branchId);
                }
                finish();
            }
            @Override public void onError(ApiException error) { finish(); }
            private void finish() {
                List<Runnable> callbacks = tableRequests.remove(branchId);
                if (callbacks != null) for (Runnable callback : callbacks) callback.run();
            }
        });
    }
    private ApiClient api() { return ApiClient.getInstance(); }

    public static MobileRepository getInstance() { return INSTANCE; }
    private MobileRepository() {}

    public boolean hasSession() { return api().hasSession(); }

    public void login(String username, String password, Callback<User> callback) {
        try {
            JSONObject body = new JSONObject().put("username", username).put("password", password);
            api().post("/api/mobile/v1/auth/login", body, false, null, tokenCallback(callback));
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    public void loginWithGoogle(String idToken, Callback<User> callback) {
        try {
            JSONObject body = new JSONObject().put("idToken", idToken);
            api().post("/api/mobile/v1/auth/google", body, false, null, tokenCallback(callback));
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    public void register(String name, String email, String phone, String password, Callback<User> callback) {
        try {
            JSONObject body = new JSONObject().put("name", name).put("username", email)
                    .put("password", password).put("phoneNumber", phone).put("email", email);
            api().post("/api/mobile/v1/auth/register", body, false, null, tokenCallback(callback));
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    public void forgotPassword(String identity, Callback<String> callback) {
        try {
            JSONObject body = new JSONObject().put("usernameOrEmailOrPhone", identity);
            api().post("/api/mobile/v1/auth/forgot-password", body, false, null, new ApiClient.Callback() {
                @Override public void onSuccess(Object value) {
                    String token = ((JSONObject) value).optString("resetToken", "");
                    if (token.isEmpty()) callback.onError(new ApiException(0, "reset_token_delivery_required", "Hãy kiểm tra kênh nhận mã đặt lại mật khẩu.", null));
                    else callback.onSuccess(token);
                }
                @Override public void onError(ApiException error) { callback.onError(error); }
            });
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    public void resetPassword(String token, String password, Callback<Void> callback) {
        try {
            JSONObject body = new JSONObject().put("token", token).put("newPassword", password).put("confirmPassword", password);
            api().post("/api/mobile/v1/auth/reset-password", body, false, null, new ApiClient.Callback() {
                @Override public void onSuccess(Object body) { callback.onSuccess(null); }
                @Override public void onError(ApiException error) { callback.onError(error); }
            });
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    private ApiClient.Callback tokenCallback(Callback<User> callback) {
        return new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONObject response = (JSONObject) body;
                if (!api().saveTokenResponse(response)) {
                    callback.onError(new ApiException(0, "secure_storage_failed",
                            "Không thể lưu phiên đăng nhập an toàn. Vui lòng thử lại.", null));
                    return;
                }
                User user = parseTokenUser(response.optJSONObject("user"));
                state.setCurrentUser(user);
                state.setLoggedIn(true);
                SignalRManager.getInstance().startIfAuthenticated();
                callback.onSuccess(user);
                refreshBootstrap(null);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        };
    }

    public void refreshBootstrap(Callback<Void> callback) {
        loadBranches(new Callback<List<Branch>>() {
            @Override public void onSuccess(List<Branch> value) {
                loadProfile(new Callback<User>() {
                    @Override public void onSuccess(User value) {
                        loadSelectedBranchMenu(callback);
                    }
                    @Override public void onError(ApiException error) {
                        if (error.getStatusCode() == 401) state.setLoggedIn(false);
                        finishError(callback, error);
                    }
                });
            }
            @Override public void onError(ApiException error) { finishError(callback, error); }
        });
    }

    private void loadSelectedBranchMenu(Callback<Void> callback) {
        Branch selected = state.getSelectedBranch();
        if (selected == null) { finishSuccess(callback, null); return; }
        loadMenu(selected.getId(), new Callback<Void>() {
            @Override public void onSuccess(Void value) { loadFavorites(new Callback<Void>() {
                @Override public void onSuccess(Void ignored) {
                    loadReservations(new Callback<List<Reservation>>() {
                        @Override public void onSuccess(List<Reservation> value) {
                            loadActiveSession(new Callback<DiningSession>() {
                                @Override public void onSuccess(DiningSession session) { finishSuccess(callback, null); }
                                @Override public void onError(ApiException error) { finishError(callback, error); }
                            });
                        }
                        @Override public void onError(ApiException error) { finishError(callback, error); }
                    });
                }
                @Override public void onError(ApiException error) { finishError(callback, error); }
            }); }
            @Override public void onError(ApiException error) { finishError(callback, error); }
        });
    }

    public void loadBranches(Callback<List<Branch>> callback) {
        branchRequests.add(callback);
        if (branchesLoading) return;
        branchesLoading = true;
        api().get("/api/mobile/v1/branches", false, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray rows = (JSONArray) body;
                List<Branch> branches = new ArrayList<>();
                for (int i = 0; i < rows.length(); i++) {
                    JSONObject row = rows.optJSONObject(i);
                    if (row == null) continue;
                    Branch branch = new Branch(String.valueOf(row.optInt("branchId")), row.optString("name"),
                            row.optString("location"), "", "", 0, true, "", new ArrayList<>(),
                            0, 0, 0, 0);
                    branch.setImageUrl(api().absoluteUrl(row.optString("image", "")));
                    branches.add(branch);
                }
                state.replaceBranches(branches);
                finishBranchRequests(branches, null);
            }
            @Override public void onError(ApiException error) { finishBranchRequests(null, error); }
        });
    }

    private void finishBranchRequests(List<Branch> branches, ApiException error) {
        List<Callback<List<Branch>>> callbacks = new ArrayList<>(branchRequests);
        branchRequests.clear();
        branchesLoading = false;
        for (Callback<List<Branch>> callback : callbacks) {
            if (error == null) finishSuccess(callback, branches);
            else finishError(callback, error);
        }
    }

    public void loadMenu(String branchId, Callback<Void> callback) {
        if (menuRequests.containsKey(branchId)) {
            menuRequests.get(branchId).add(callback);
            if (!state.isCatalogLoading()) state.beginCatalogLoad(branchId);
            return;
        }
        List<Callback<Void>> callbacks = new ArrayList<>();
        callbacks.add(callback);
        menuRequests.put(branchId, callbacks);
        final long startedAt = android.os.SystemClock.elapsedRealtime();
        if (com.restaurant.viettable.BuildConfig.DEBUG)
            android.util.Log.d("CatalogLoad", "menu start branch=" + branchId);
        state.beginCatalogLoad(branchId);
        api().get("/api/mobile/v1/branches/" + branchId + "/menu", false, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONObject response = (JSONObject) body;
                JSONArray categoryRows = response.optJSONArray("categories");
                if (categoryRows == null) { onError(jsonError()); return; }
                List<Category> categories = new ArrayList<>();
                List<FoodItem> foods = new ArrayList<>();
                if (categoryRows != null) for (int i = 0; i < categoryRows.length(); i++) {
                    JSONObject category = categoryRows.optJSONObject(i);
                    if (category == null) continue;
                    String categoryId = String.valueOf(category.optInt("categoryId"));
                    JSONArray dishes = category.optJSONArray("dishes");
                    categories.add(new Category(categoryId, category.optString("categoryName"), 0,
                            dishes == null ? 0 : dishes.length()));
                    if (dishes == null) continue;
                    for (int j = 0; j < dishes.length(); j++) {
                        JSONObject dish = dishes.optJSONObject(j);
                        if (dish == null) continue;
                        foods.add(parseDish(dish, categoryId, branchId));
                    }
                }
                boolean applied = state.replaceBranchCatalog(branchId, categories, foods);
                if (com.restaurant.viettable.BuildConfig.DEBUG)
                    android.util.Log.d("CatalogLoad", "menu success branch=" + branchId
                            + " categories=" + categories.size() + " dishes=" + foods.size()
                            + " applied=" + applied + " elapsedMs=" + (android.os.SystemClock.elapsedRealtime() - startedAt));
                finishMenuRequest(branchId, null);
            }
            @Override public void onError(ApiException error) {
                if (com.restaurant.viettable.BuildConfig.DEBUG)
                    android.util.Log.d("CatalogLoad", "menu error branch=" + branchId
                            + " http=" + error.getStatusCode() + " elapsedMs="
                            + (android.os.SystemClock.elapsedRealtime() - startedAt));
                state.failCatalogLoad(branchId, error.getMessage());
                finishMenuRequest(branchId, error);
            }
        });
    }

    private void finishMenuRequest(String branchId, ApiException error) {
        List<Callback<Void>> callbacks = menuRequests.remove(branchId);
        if (callbacks == null) return;
        for (Callback<Void> callback : callbacks) {
            if (error == null) finishSuccess(callback, null);
            else finishError(callback, error);
        }
    }

    public void loadProfile(Callback<User> callback) {
        api().get("/api/mobile/v1/me", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                User user = parseProfile((JSONObject) body);
                state.setCurrentUser(user);
                state.setLoggedIn(true);
                callback.onSuccess(user);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void updateProfile(String name, String phone, String email, Callback<User> callback) {
        try {
            JSONObject body = new JSONObject().put("name", name).put("phoneNumber", phone).put("email", email);
            api().put("/api/mobile/v1/me", body, true, null, new ApiClient.Callback() {
                @Override public void onSuccess(Object value) {
                    User user = parseProfile((JSONObject) value);
                    state.setCurrentUser(user);
                    callback.onSuccess(user);
                }
                @Override public void onError(ApiException error) { callback.onError(error); }
            });
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    public void loadFavorites(Callback<Void> callback) {
        api().get("/api/mobile/v1/me/favorites", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray rows = ((JSONObject) body).optJSONArray("items");
                Set<String> ids = new HashSet<>();
                if (rows != null) for (int i = 0; i < rows.length(); i++)
                    ids.add(String.valueOf(rows.optJSONObject(i).optInt("dishId")));
                state.applyFavoriteIds(ids);
                callback.onSuccess(null);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void setFavorite(FoodItem food, boolean favorite, Callback<Void> callback) {
        ApiClient.Callback apiCallback = new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                food.setFavorite(favorite);
                state.notifyStateChanged();
                callback.onSuccess(null);
            }
            @Override public void onError(ApiException error) {
                food.setFavorite(!favorite);
                state.notifyStateChanged();
                callback.onError(error);
            }
        };
        String path = "/api/mobile/v1/me/favorites/dishes/" + food.getId();
        if (favorite) api().post(path, new JSONObject(), true, null, apiCallback);
        else api().delete(path, true, apiCallback);
    }

    public void loadNotifications(Callback<List<NotificationItem>> callback) {
        api().get("/api/mobile/v1/notifications?limit=100", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray rows = ((JSONObject) body).optJSONArray("items");
                List<NotificationItem> values = new ArrayList<>();
                if (rows != null) for (int i = 0; i < rows.length(); i++) {
                    JSONObject row = rows.optJSONObject(i);
                    values.add(new NotificationItem(String.valueOf(row.optLong("notificationId")),
                            NotificationItem.Type.ORDER, "Món ăn đã sẵn sàng", row.optString("message"),
                            row.optString("createdAtUtc"), row.optBoolean("isRead")));
                }
                state.replaceNotifications(values);
                callback.onSuccess(values);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void loadReservations(Callback<List<Reservation>> callback) {
        api().get("/api/mobile/v1/reservations?limit=100", true, tableAware(new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray rows = ((JSONObject) body).optJSONArray("items");
                List<Reservation> values = new ArrayList<>();
                if (rows != null) for (int i = 0; i < rows.length(); i++) values.add(parseReservation(rows.optJSONObject(i)));
                state.replaceReservations(values);
                callback.onSuccess(values);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        }));
    }

    public void createReservation(ReservationDraft draft, Callback<Reservation> callback) {
        try {
            if (draft.getBranch() == null) throw new JSONException("missing branch");
            JSONObject body = new JSONObject().put("branchId", Integer.parseInt(draft.getBranch().getId()))
                    .put("partySize", draft.getGuestCount()).put("reservedAt", reservationInstant(draft))
                    .put("note", draft.getNote());
            int selectedTable = Integer.parseInt(draft.getTableId());
            body.put("tableId", selectedTable > 0 ? selectedTable : JSONObject.NULL);
            api().post("/api/mobile/v1/reservations", body, true, key("reservation"), tableAware(new ApiClient.Callback() {
                @Override public void onSuccess(Object value) {
                    Reservation reservation = parseReservation((JSONObject) value);
                    state.upsertReservation(reservation);
                    callback.onSuccess(reservation);
                }
                @Override public void onError(ApiException error) { callback.onError(error); }
            }));
        } catch (Exception e) { callback.onError(new ApiException(0, "invalid_reservation_time", "Ngày giờ đặt bàn không hợp lệ.", null)); }
    }

    public static final class AvailableTable {
        public final String tableId;
        public final int tableNumber;
        public final int capacity;
        public AvailableTable(String tableId, int tableNumber, int capacity) {
            this.tableId = tableId; this.tableNumber = tableNumber; this.capacity = capacity;
        }
    }

    public void loadAvailability(ReservationDraft draft, Callback<List<AvailableTable>> callback) {
        try {
            if (draft.getBranch() == null) throw new IllegalArgumentException("missing branch");
            String instant = URLEncoder.encode(reservationInstant(draft), StandardCharsets.UTF_8.name());
            String path = "/api/mobile/v1/branches/" + draft.getBranch().getId() + "/availability?reservedAt="
                    + instant + "&partySize=" + draft.getGuestCount();
            api().get(path, false, new ApiClient.Callback() {
                @Override public void onSuccess(Object body) {
                    JSONArray rows = ((JSONObject) body).optJSONArray("availableTables");
                    List<AvailableTable> values = new ArrayList<>();
                    if (rows != null) for (int i = 0; i < rows.length(); i++) {
                        JSONObject row = rows.optJSONObject(i);
                        if (row != null) {
                            tableNumbers.put(draft.getBranch().getId(), String.valueOf(row.optInt("tableId")), row.optInt("tableNumber"));
                            values.add(new AvailableTable(String.valueOf(row.optInt("tableId")),
                                row.optInt("tableNumber"), row.optInt("numberOfSeats")));
                        }
                    }
                    callback.onSuccess(values);
                }
                @Override public void onError(ApiException error) { callback.onError(error); }
            });
        } catch (Exception e) {
            callback.onError(new ApiException(0, "invalid_reservation_time", "Không thể kiểm tra bàn cho ngày giờ đã chọn.", null));
        }
    }

    public void cancelReservation(Reservation reservation, Callback<Reservation> callback) {
        api().post("/api/mobile/v1/reservations/" + reservation.getId() + "/cancel", new JSONObject(), true, null,
                tableAware(new ApiClient.Callback() {
                    @Override public void onSuccess(Object body) {
                        Reservation updated = parseReservation((JSONObject) body); state.upsertReservation(updated); callback.onSuccess(updated);
                    }
                    @Override public void onError(ApiException error) { callback.onError(error); }
                }));
    }

    public void updateReservation(Reservation reservation, String date, String time, int partySize,
                                  String note, Callback<Reservation> callback) {
        try {
            JSONObject body = new JSONObject().put("partySize", partySize)
                    .put("reservedAt", reservationInstant(date, time)).put("note", note);
            int tableId = Integer.parseInt(reservation.getTableId());
            body.put("tableId", tableId > 0 ? tableId : JSONObject.NULL);
            api().put("/api/mobile/v1/reservations/" + reservation.getId(), body, true,
                    key("reservation-update"), tableAware(new ApiClient.Callback() {
                        @Override public void onSuccess(Object value) {
                            Reservation updated = parseReservation((JSONObject) value);
                            state.upsertReservation(updated);
                            callback.onSuccess(updated);
                        }
                        @Override public void onError(ApiException error) { callback.onError(error); }
                    }));
        } catch (Exception e) {
            callback.onError(new ApiException(0, "invalid_reservation_time", "Ngày giờ đặt bàn không hợp lệ.", null));
        }
    }

    public void submitPreOrder(Reservation reservation, Callback<JSONObject> callback) {
        updatePreOrder(reservation, false, new Callback<JSONObject>() {
            @Override public void onSuccess(JSONObject draft) {
                api().post("/api/mobile/v1/reservations/" + reservation.getId() + "/preorder/submit",
                        new JSONObject(), true, key("preorder"), new ApiClient.Callback() {
                            @Override public void onSuccess(Object value) {
                                reservation.setHasPreOrder(true);
                                state.notifyStateChanged();
                                callback.onSuccess((JSONObject) value);
                            }
                            @Override public void onError(ApiException error) { callback.onError(error); }
                        });
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void loadPreOrder(Reservation reservation, Callback<JSONObject> callback) {
        api().get("/api/mobile/v1/reservations/" + reservation.getId() + "/preorder", true,
                new ApiClient.Callback() {
                    @Override public void onSuccess(Object body) {
                        JSONObject value = (JSONObject) body;
                        JSONArray rows = value.optJSONArray("items");
                        List<CartItem> cart = new ArrayList<>();
                        if (rows != null) for (int i = 0; i < rows.length(); i++) {
                            JSONObject row = rows.optJSONObject(i);
                            if (row == null) continue;
                            String dishId = String.valueOf(row.optInt("dishId"));
                            FoodItem food = state.findFoodItem(dishId);
                            if (food == null) food = new FoodItem(dishId, "", reservation.getBranchId(),
                                    row.optString("dishNameSnapshot"), Math.round(row.optDouble("unitPriceSnapshot")),
                                    0, 0, "", "", "", "", 0, true, false,
                                    Collections.singletonList("Tiêu chuẩn"), new ArrayList<>(), "", 0);
                            cart.add(new CartItem(food, row.optInt("quantity"), "Tiêu chuẩn",
                                    new ArrayList<>(), 0, row.optString("note")));
                        }
                        state.replaceCart(cart);
                        reservation.setHasPreOrder(rows != null && rows.length() > 0);
                        callback.onSuccess(value);
                    }
                    @Override public void onError(ApiException error) { callback.onError(error); }
                });
    }

    public void clearPreOrder(Reservation reservation, Callback<JSONObject> callback) {
        updatePreOrder(reservation, true, new Callback<JSONObject>() {
            @Override public void onSuccess(JSONObject value) {
                reservation.setHasPreOrder(false);
                state.notifyStateChanged();
                callback.onSuccess(value);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    private void updatePreOrder(Reservation reservation, boolean empty, Callback<JSONObject> callback) {
        try {
            JSONArray items = new JSONArray();
            if (!empty) for (CartItem cart : state.getCartItems()) {
                items.put(new JSONObject().put("dishId", Integer.parseInt(cart.getFoodItem().getId()))
                        .put("quantity", cart.getQuantity()).put("note", cart.getOptionsSummary()));
            }
            JSONObject body = new JSONObject().put("items", items);
            api().put("/api/mobile/v1/reservations/" + reservation.getId() + "/preorder", body,
                    true, null, objectCallback(callback));
        } catch (Exception e) { callback.onError(jsonError()); }
    }

    public void validateQr(String qrCode, Callback<JSONObject> callback) {
        try {
            JSONObject body = new JSONObject().put("qrCode", qrCode);
            Reservation upcoming = state.getUpcomingReservation();
            if (upcoming != null) body.put("reservationId", Integer.parseInt(upcoming.getId()));
            api().post("/api/mobile/v1/check-ins/qr/validate", body, true, null, tableAware(new ApiClient.Callback() {
                @Override public void onSuccess(Object body) {
                    JSONObject row = (JSONObject) body;
                    String branch = String.valueOf(row.optInt("branchId"));
                    String table = String.valueOf(row.optInt("tableId"));
                    tableNumbers.put(branch, table, row.optInt("tableNumber"));
                    qrTableLabels.put(qrCode, tableLabel(branch, table));
                    Branch selected = state.findBranch(branch);
                    qrBranches.put(qrCode, selected == null ? "Chưa có thông tin chi nhánh" : selected.getName());
                    callback.onSuccess(row);
                }
                @Override public void onError(ApiException error) { callback.onError(error); }
            }));
        } catch (Exception e) { callback.onError(jsonError()); }
    }

    public void checkInWalkIn(String qrCode, Callback<DiningSession> callback) {
        try {
            JSONObject body = new JSONObject().put("qrCode", qrCode);
            api().post("/api/mobile/v1/check-ins/walk-in", body, true, key("walkin"), sessionCallback(callback));
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    public void checkInReservation(Reservation reservation, String qrCode, boolean confirmMismatch,
                                   Callback<DiningSession> callback) {
        try {
            JSONObject body = new JSONObject().put("reservationId", Integer.parseInt(reservation.getId()))
                    .put("qrCode", qrCode).put("confirmTableMismatch", confirmMismatch);
            api().post("/api/mobile/v1/check-ins/reservation", body, true, key("reservation-checkin"), sessionCallback(callback));
        } catch (Exception e) { callback.onError(jsonError()); }
    }

    public void loadActiveSession(Callback<DiningSession> callback) {
        api().get("/api/mobile/v1/dining-sessions/active", true, tableAware(new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                DiningSession session = parseSession((JSONObject) body); state.setActiveDiningSession(session);
                loadOrders("active", new Callback<List<Order>>() {
                    @Override public void onSuccess(List<Order> value) { callback.onSuccess(state.getActiveDiningSession()); }
                    @Override public void onError(ApiException error) { callback.onSuccess(session); }
                });
            }
            @Override public void onError(ApiException error) {
                if (error.getStatusCode() == 404) { state.setActiveDiningSession(null); callback.onSuccess(null); }
                else callback.onError(error);
            }
        }));
    }

    public void submitCurrentCart(Callback<Order> callback) {
        DiningSession session = state.getActiveDiningSession();
        if (session == null || state.getCartItems().isEmpty()) {
            callback.onError(new ApiException(0, "empty_cart", "Giỏ món đang trống hoặc chưa có phiên phục vụ.", null)); return;
        }
        try {
            JSONArray items = new JSONArray();
            for (CartItem cart : state.getCartItems()) items.put(new JSONObject()
                    .put("dishId", Integer.parseInt(cart.getFoodItem().getId())).put("quantity", cart.getQuantity())
                    .put("note", cart.getOptionsSummary()));
            JSONObject body = new JSONObject().put("items", items);
            api().post("/api/mobile/v1/dining-sessions/" + session.getId() + "/orders", body, true, key("order"),
                    tableAware(new ApiClient.Callback() {
                        @Override public void onSuccess(Object value) {
                            Order order = parseOrder((JSONObject) value); state.addServerOrder(order); state.clearCart(); callback.onSuccess(order);
                        }
                        @Override public void onError(ApiException error) { callback.onError(error); }
                    }));
        } catch (Exception e) { callback.onError(jsonError()); }
    }

    public void cancelPaymentRequest(Callback<DiningSession> callback) {
        DiningSession session = state.getActiveDiningSession();
        if (session == null) { callback.onError(new ApiException(0, "session_required", "Chưa có phiên phục vụ.", null)); return; }
        api().post("/api/mobile/v1/dining-sessions/" + session.getId() + "/cancel-payment-request", new JSONObject(), true, null, sessionCallback(callback));
    }

    public void loadOrders(String scope, Callback<List<Order>> callback) {
        api().get("/api/mobile/v1/orders?scope=" + scope, true, tableAware(new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray rows = (JSONArray) body; List<Order> values = new ArrayList<>();
                for (int i = 0; i < rows.length(); i++) values.add(parseOrder(rows.optJSONObject(i)));
                state.replaceOrders(scope, values); callback.onSuccess(values);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        }));
    }

    public void confirmOrderItemReceived(int orderId, int itemId, Callback<Boolean> callback) {
        api().post("/api/mobile/v1/orders/" + orderId + "/items/" + itemId + "/confirm-received", new JSONObject(), true, null,
                new ApiClient.Callback() {
                    @Override public void onSuccess(Object body) { callback.onSuccess(true); }
                    @Override public void onError(ApiException error) { callback.onError(error); }
                });
    }

    public void createServiceRequest(String type, String note, Callback<ServiceRequest> callback) {
        DiningSession session = state.getActiveDiningSession();
        if (session == null) { callback.onError(new ApiException(0, "session_required", "Chưa có phiên phục vụ.", null)); return; }
        try {
            JSONObject body = new JSONObject().put("requestType", normalizeServiceType(type)).put("note", note);
            api().post("/api/mobile/v1/dining-sessions/" + session.getId() + "/service-requests", body, true, key("service"),
                    new ApiClient.Callback() {
                        @Override public void onSuccess(Object value) {
                            ServiceRequest request = parseServiceRequest((JSONObject) value);
                            state.upsertServiceRequest(request); callback.onSuccess(request);
                        }
                        @Override public void onError(ApiException error) { callback.onError(error); }
                    });
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    public void loadServiceRequests(Callback<List<ServiceRequest>> callback) {
        DiningSession session = state.getActiveDiningSession();
        if (session == null) { callback.onError(new ApiException(0, "session_required", "Chưa có phiên phục vụ.", null)); return; }
        api().get("/api/mobile/v1/dining-sessions/" + session.getId() + "/service-requests", true,
                new ApiClient.Callback() {
                    @Override public void onSuccess(Object body) {
                        JSONArray rows = (JSONArray) body;
                        List<ServiceRequest> values = new ArrayList<>();
                        for (int i = 0; i < rows.length(); i++) values.add(parseServiceRequest(rows.optJSONObject(i)));
                        session.getServiceRequests().clear();
                        session.getServiceRequests().addAll(values);
                        state.notifyStateChanged();
                        callback.onSuccess(values);
                    }
                    @Override public void onError(ApiException error) { callback.onError(error); }
                });
    }

    public void cancelServiceRequest(ServiceRequest request, Callback<ServiceRequest> callback) {
        api().post("/api/mobile/v1/service-requests/" + request.getId() + "/cancel", new JSONObject(),
                true, null, new ApiClient.Callback() {
                    @Override public void onSuccess(Object body) {
                        ServiceRequest updated = parseServiceRequest((JSONObject) body);
                        request.setStatus(updated.getStatus());
                        state.notifyStateChanged();
                        callback.onSuccess(updated);
                    }
                    @Override public void onError(ApiException error) { callback.onError(error); }
                });
    }

    public void beginCheckout(Callback<Invoice> callback) {
        DiningSession session = state.getActiveDiningSession();
        if (session == null) { callback.onError(new ApiException(0, "session_required", "Chưa có phiên phục vụ.", null)); return; }
        api().post("/api/mobile/v1/dining-sessions/" + session.getId() + "/checkout/quote", new JSONObject(), true, key("quote"),
                new ApiClient.Callback() {
                    @Override public void onSuccess(Object body) {
                        currentQuote = (JSONObject) body;
                        callback.onSuccess(invoiceFromQuote(currentQuote));
                    }
                    @Override public void onError(ApiException error) { callback.onError(error); }
                });
    }

    public void requestCheckoutPayment(Callback<Invoice> callback) {
        DiningSession session = state.getActiveDiningSession();
        int quoteId = currentCheckoutQuoteId();
        if (session == null || quoteId <= 0) {
            callback.onError(new ApiException(0, "quote_required", "Không tìm thấy báo giá thanh toán từ máy chủ.", null)); return;
        }
        try {
            JSONObject request = new JSONObject().put("checkoutQuoteId", quoteId);
            api().post("/api/mobile/v1/dining-sessions/" + session.getId() + "/payment-request", request, true, null,
                    new ApiClient.Callback() {
                        @Override public void onSuccess(Object body) {
                            currentQuote = (JSONObject) body;
                            session.setStatus(DiningSession.Status.PAYMENT_REQUESTED); state.notifyStateChanged();
                            callback.onSuccess(invoiceFromQuote(currentQuote));
                        }
                        @Override public void onError(ApiException error) { callback.onError(error); }
                    });
        } catch (JSONException error) { callback.onError(jsonError()); }
    }

    public Invoice currentCheckoutInvoice() { return currentQuote == null ? null : invoiceFromQuote(currentQuote); }
    public int currentCheckoutQuoteId() { return currentQuote == null ? 0 : currentQuote.optInt("checkoutQuoteId"); }
    public SplitBill currentSplitBill() { return currentSplitBill; }

    public void loadSessionSplitBill(Callback<SplitBill> callback) {
        DiningSession session = state.getActiveDiningSession();
        if (session == null) { callback.onError(new ApiException(0, "session_required", "Chưa có phiên phục vụ.", null)); return; }
        api().get("/api/mobile/v1/dining-sessions/" + session.getId() + "/split-bill", true, splitCallback(callback));
    }

    public void loadSplitBill(int splitBillId, Callback<SplitBill> callback) {
        api().get("/api/mobile/v1/split-bills/" + splitBillId, true, splitCallback(callback));
    }

    public void createSplitBill(long[] amounts, Callback<SplitBill> callback) {
        DiningSession session = state.getActiveDiningSession();
        int quoteId = currentCheckoutQuoteId();
        if (session == null || quoteId <= 0) {
            callback.onError(new ApiException(0, "quote_required", "Không tìm thấy báo giá thanh toán từ máy chủ.", null)); return;
        }
        try {
            JSONArray values = new JSONArray();
            StringBuilder identity = new StringBuilder(String.valueOf(quoteId));
            for (long amount : amounts) { values.put(amount); identity.append('-').append(amount); }
            JSONObject body = new JSONObject().put("checkoutQuoteId", quoteId).put("amounts", values);
            api().post("/api/mobile/v1/dining-sessions/" + session.getId() + "/split-bill", body, true,
                    stableKey("split", identity.toString()), splitCallback(callback));
        } catch (JSONException error) { callback.onError(jsonError()); }
    }

    public void createSplitPayment(SplitBill splitBill, SplitBill.Part part, String channel,
                                   String method, String location, Callback<SplitPaymentResult> callback) {
        if (splitBill == null || part == null || !part.canStartPayment()) {
            callback.onError(new ApiException(0, "split_part_not_payable", "Phần thanh toán này không còn khả dụng.", null)); return;
        }
        try {
            JSONObject body = new JSONObject().put("splitBillPartId", part.getSplitBillPartId())
                    .put("channel", channel).put("method", method)
                    .put("location", location == null ? JSONObject.NULL : location);
            String identity = part.getSplitBillPartId() + "-" + channel + "-" + method + "-" + location;
            api().post("/api/mobile/v1/split-bills/" + splitBill.getSplitBillId() + "/payments", body, true,
                    stableKey("splitpay", identity), new ApiClient.Callback() {
                        @Override public void onSuccess(Object value) {
                            JSONObject response = (JSONObject) value;
                            PaymentInfo payment = parsePayment(response.optJSONObject("payment"));
                            SplitBill updated = parseSplitBill(response.optJSONObject("splitBill"));
                            currentPaymentId = payment.getPaymentId(); currentSplitBill = updated;
                            callback.onSuccess(new SplitPaymentResult(payment, updated));
                        }
                        @Override public void onError(ApiException error) { callback.onError(error); }
                    });
        } catch (JSONException error) { callback.onError(jsonError()); }
    }

    public void createPayment(String channel, String method, String location, Callback<PaymentInfo> callback) {
        if (currentQuote == null) { callback.onError(new ApiException(0, "quote_required", "Chưa có báo giá thanh toán.", null)); return; }
        try {
            JSONObject body = new JSONObject().put("channel", channel).put("method", method)
                    .put("location", location == null ? JSONObject.NULL : location);
            api().post("/api/mobile/v1/checkout/quotes/" + currentQuote.optInt("checkoutQuoteId") + "/payments",
                    body, true, key("payment"), new ApiClient.Callback() {
                        @Override public void onSuccess(Object value) { PaymentInfo payment = parsePayment((JSONObject) value); currentPaymentId = payment.getPaymentId(); callback.onSuccess(payment); }
                        @Override public void onError(ApiException error) { callback.onError(error); }
                    });
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    public void refreshPayment(Callback<PaymentInfo> callback) { refreshPayment(currentPaymentId, callback); }

    public void refreshPayment(int paymentId, Callback<PaymentInfo> callback) {
        if (paymentId <= 0) { callback.onError(new ApiException(0, "payment_required", "Chưa có giao dịch thanh toán.", null)); return; }
        api().get("/api/mobile/v1/payments/" + paymentId, true, new ApiClient.Callback() {
            @Override public void onSuccess(Object value) { PaymentInfo payment = parsePayment((JSONObject) value); currentPaymentId = payment.getPaymentId(); callback.onSuccess(payment); }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void retryPayment(int paymentId, Callback<PaymentInfo> callback) {
        if (paymentId <= 0) {
            callback.onError(new ApiException(0, "payment_required", "Không tìm thấy giao dịch cần thử lại.", null)); return;
        }
        api().post("/api/mobile/v1/payments/" + paymentId + "/retry", new JSONObject(), true,
                stableKey("paymentretry", String.valueOf(paymentId)), new ApiClient.Callback() {
                    @Override public void onSuccess(Object value) {
                        PaymentInfo payment = parsePayment((JSONObject) value);
                        currentPaymentId = payment.getPaymentId(); callback.onSuccess(payment);
                    }
                    @Override public void onError(ApiException error) { callback.onError(error); }
                });
    }

    public void loadIssuedInvoice(Callback<Invoice> callback) {
        DiningSession session = state.getActiveDiningSession();
        if (session == null && currentQuote == null) { callback.onError(new ApiException(0, "session_required", "Không tìm thấy phiên thanh toán.", null)); return; }
        int sessionId = session != null ? Integer.parseInt(session.getId()) : currentQuote.optInt("diningSessionId");
        api().get("/api/mobile/v1/dining-sessions/" + sessionId + "/invoice", true, tableAware(new ApiClient.Callback() {
            @Override public void onSuccess(Object body) { callback.onSuccess(invoiceFromIssued((JSONObject) body)); }
            @Override public void onError(ApiException error) { callback.onError(error); }
        }));
    }

    public void loadInvoiceHistory(Callback<List<InvoiceHistoryItem>> callback) {
        api().get("/api/mobile/v1/invoices?limit=100", true, tableAware(new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray rows = ((JSONObject) body).optJSONArray("items");
                List<InvoiceHistoryItem> invoices = new ArrayList<>();
                if (rows != null) for (int i = 0; i < rows.length(); i++) {
                    JSONObject row = rows.optJSONObject(i);
                    if (row != null) invoices.add(parseInvoiceHistoryItem(row));
                }
                callback.onSuccess(invoices);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        }));
    }

    public void loadInvoiceHistoryItem(int invoiceId, Callback<InvoiceHistoryItem> callback) {
        api().get("/api/mobile/v1/invoices/" + invoiceId, true, tableAware(new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                callback.onSuccess(parseInvoiceHistoryItem((JSONObject) body));
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        }));
    }

    private InvoiceHistoryItem parseInvoiceHistoryItem(JSONObject row) {
        int branchId = row.optInt("branchId");
        Branch branch = state.findBranch(String.valueOf(branchId));
        int tableId = row.optInt("tableId");
        JSONArray lineRows = row.optJSONArray("items");
        List<InvoiceHistoryItem.Line> lines = new ArrayList<>();
        if (lineRows != null) for (int i = 0; i < lineRows.length(); i++) {
            JSONObject line = lineRows.optJSONObject(i);
            if (line == null) continue;
            String name = jsonString(line, "dishName", "DishName", "name", "Name");
            int quantity = jsonInt(line, "quantity", "Quantity");
            long total = jsonLong(line, "lineTotal", "LineTotal");
            if (total == 0) total = jsonLong(line, "totalAmount", "TotalAmount");
            if (total == 0) total = jsonLong(line, "unitPrice", "UnitPrice") * quantity;
            lines.add(new InvoiceHistoryItem.Line(name, quantity, total));
        }
        InvoiceHistoryItem invoice = new InvoiceHistoryItem(row.optInt("invoiceId"), row.optString("invoiceCode"),
                row.optInt("checkoutQuoteId"), row.optInt("paymentId"), row.optInt("diningSessionId"),
                row.optString("diningSessionCode"), branch == null ? "Chi nhánh " + branchId : branch.getName(),
                tableId > 0 ? String.valueOf(tableId) : "", Math.round(row.optDouble("subtotal")),
                Math.round(row.optDouble("discount")), Math.round(row.optDouble("totalAmount")),
                row.optString("issuedAtUtc"), row.optString("paymentCode"), row.optString("paymentChannel"),
                row.optString("paymentMethod"), row.optString("paymentLocation"), row.optString("paymentStatus"), lines);
        invoice.setTableLabel(tableLabel(String.valueOf(branchId), invoice.getTableId()));
        return invoice;
    }

    private static String jsonString(JSONObject row, String... keys) {
        for (String key : keys) if (row.has(key) && !row.isNull(key)) return row.optString(key);
        return "Món ăn";
    }

    private static int jsonInt(JSONObject row, String... keys) {
        for (String key : keys) if (row.has(key)) return row.optInt(key);
        return 0;
    }

    private static long jsonLong(JSONObject row, String... keys) {
        for (String key : keys) if (row.has(key)) return Math.round(row.optDouble(key));
        return 0;
    }

    public void loadFaqs(Callback<List<FaqItem>> callback) {
        api().get("/api/mobile/v1/support/faqs", false, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray rows = ((JSONObject) body).optJSONArray("items"); List<FaqItem> result = new ArrayList<>();
                if (rows != null) for (int i = 0; i < rows.length(); i++) {
                    JSONObject row = rows.optJSONObject(i);
                    if (row != null) result.add(new FaqItem(row.optString("faqId"), row.optString("question"), row.optString("answer")));
                }
                callback.onSuccess(result);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void loadReviews(Callback<List<Review>> callback) {
        api().get("/api/mobile/v1/me/reviews", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray rows = ((JSONObject) body).optJSONArray("items"); List<Review> result = new ArrayList<>();
                if (rows != null) for (int i = 0; i < rows.length(); i++) {
                    JSONObject row = rows.optJSONObject(i); if (row != null) result.add(parseReview(row));
                }
                callback.onSuccess(result);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void createReview(int invoiceId, int rating, String comment, Callback<Review> callback) {
        if (invoiceId <= 0) { callback.onError(new ApiException(0, "invoice_required", "Chưa có hóa đơn hoàn tất để đánh giá.", null)); return; }
        try {
            JSONObject body = new JSONObject().put("invoiceId", invoiceId).put("overallRating", rating)
                    .put("foodRating", rating).put("serviceRating", rating).put("spaceRating", rating)
                    .put("comment", comment).put("imageUrls", new JSONArray());
            api().post("/api/mobile/v1/reviews", body, true, stableKey("reviewcreate", String.valueOf(invoiceId)), new ApiClient.Callback() {
                @Override public void onSuccess(Object body) { callback.onSuccess(parseReview((JSONObject) body)); }
                @Override public void onError(ApiException error) { callback.onError(error); }
            });
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    public void updateReview(Review review, int rating, String comment, Callback<Review> callback) {
        if (review == null || review.getReviewId() <= 0) { callback.onError(new ApiException(0, "review_required", "Không tìm thấy đánh giá cần sửa.", null)); return; }
        try {
            JSONObject body = new JSONObject().put("overallRating", rating)
                    .put("foodRating", review.getFoodRating()).put("serviceRating", review.getServiceRating())
                    .put("spaceRating", review.getSpaceRating()).put("comment", comment)
                    .put("imageUrls", new JSONArray(review.getImageUrls()));
            api().put("/api/mobile/v1/reviews/" + review.getReviewId(), body, true, null, new ApiClient.Callback() {
                @Override public void onSuccess(Object body) { callback.onSuccess(parseReview((JSONObject) body)); }
                @Override public void onError(ApiException error) { callback.onError(error); }
            });
        } catch (JSONException error) { callback.onError(jsonError()); }
    }

    public void deleteReview(int reviewId, Callback<Void> callback) {
        if (reviewId <= 0) { callback.onError(new ApiException(0, "review_required", "Không tìm thấy đánh giá cần xóa.", null)); return; }
        api().delete("/api/mobile/v1/reviews/" + reviewId, true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) { callback.onSuccess(null); }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void submitSupport(String message, Callback<Void> callback) {
        try {
            JSONObject body = new JSONObject().put("category", "GENERAL").put("subject", "Góp ý từ ứng dụng").put("message", message);
            api().post("/api/mobile/v1/support/requests", body, true, key("support"), new ApiClient.Callback() {
                @Override public void onSuccess(Object body) { callback.onSuccess(null); }
                @Override public void onError(ApiException error) { callback.onError(error); }
            });
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    public void loadSettings(Callback<JSONObject> callback) {
        api().get("/api/mobile/v1/me/settings", true, objectCallback(callback));
    }

    public void loadVouchers(Callback<List<Voucher>> callback) {
        api().get("/api/mobile/v1/me/vouchers", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray rows = (JSONArray) body; List<Voucher> values = new ArrayList<>();
                for (int i = 0; i < rows.length(); i++) {
                    JSONObject row = rows.optJSONObject(i); String type = row.optString("discountType");
                    Voucher voucher = new Voucher(row.optString("code"), row.optString("name"),
                            "FIXED".equals(type) ? Math.round(row.optDouble("discountValue")) : 0,
                            "PERCENT".equals(type) ? row.optInt("discountValue") : 0,
                            Math.round(row.optDouble("minSubtotal")), row.optString("endsAtUtc"), true, null,
                            row.optString("name"), false);
                    voucher.setBackendId(row.optInt("voucherId")); values.add(voucher);
                }
                state.replaceVouchers(values); callback.onSuccess(values);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void applyVoucher(Voucher voucher, Callback<Invoice> callback) {
        if (currentQuote == null || voucher == null || voucher.getBackendId() <= 0) {
            callback.onError(new ApiException(0, "voucher_invalid", "Voucher không hợp lệ.", null)); return;
        }
        try {
            JSONObject body = new JSONObject().put("voucherId", voucher.getBackendId());
            api().post("/api/mobile/v1/checkout/quotes/" + currentQuote.optInt("checkoutQuoteId") + "/voucher", body, true, null,
                    quoteCallback(callback));
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    public void applyLoyalty(int points, Callback<Invoice> callback) {
        if (currentQuote == null) { callback.onError(new ApiException(0, "quote_required", "Chưa có báo giá thanh toán.", null)); return; }
        try {
            JSONObject body = new JSONObject().put("points", points);
            api().post("/api/mobile/v1/checkout/quotes/" + currentQuote.optInt("checkoutQuoteId") + "/loyalty", body, true, null,
                    quoteCallback(callback));
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    private ApiClient.Callback quoteCallback(Callback<Invoice> callback) {
        return new ApiClient.Callback() {
            @Override public void onSuccess(Object body) { currentQuote = (JSONObject) body; callback.onSuccess(invoiceFromQuote(currentQuote)); }
            @Override public void onError(ApiException error) { callback.onError(error); }
        };
    }

    public void updateSettings(boolean push, boolean sms, boolean biometric, String language, Callback<JSONObject> callback) {
        try {
            JSONObject body = new JSONObject().put("pushNotificationsEnabled", push)
                    .put("smsReservationRemindersEnabled", sms).put("biometricLoginEnabled", biometric)
                    .put("languageCode", language);
            Branch branch = state.getSelectedBranch();
            body.put("defaultBranchId", branch == null ? JSONObject.NULL : Integer.parseInt(branch.getId()));
            api().put("/api/mobile/v1/me/settings", body, true, null, objectCallback(callback));
        } catch (Exception e) { callback.onError(jsonError()); }
    }

    public void markNotificationRead(NotificationItem item, Callback<Void> callback) {
        api().post("/api/mobile/v1/notifications/" + item.getId() + "/read", new JSONObject(), true, null,
                new ApiClient.Callback() {
                    @Override public void onSuccess(Object body) { item.setRead(true); state.notifyStateChanged(); callback.onSuccess(null); }
                    @Override public void onError(ApiException error) { callback.onError(error); }
                });
    }

    public void logout(Callback<Void> callback) {
        api().post("/api/mobile/v1/auth/logout", tokenBody(), false, null, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) { clearLocalSession(); callback.onSuccess(null); }
            @Override public void onError(ApiException error) { clearLocalSession(); callback.onSuccess(null); }
        });
    }

    public void clearLocalSession() {
        SignalRManager.getInstance().stopAndClear();
        api().clearSession();
        state.clearAuthenticatedData();
        tableNumbers.clear(); tableBranchesLoaded.clear(); qrTableLabels.clear(); qrBranches.clear();
        currentQuote = null; currentSplitBill = null; currentPaymentId = 0;
    }

    private JSONObject tokenBody() {
        try { return new JSONObject().put("refreshToken", api().getRefreshToken()); }
        catch (JSONException ignored) { return new JSONObject(); }
    }

    private FoodItem parseDish(JSONObject dish, String categoryId, String branchId) {
        List<String> ingredients = new ArrayList<>();
        JSONArray rows = dish.optJSONArray("ingredients");
        if (rows != null) for (int i = 0; i < rows.length(); i++)
            ingredients.add(rows.optJSONObject(i).optString("name"));
        String image = api().absoluteUrl(dish.optString("image"));
        return new FoodItem(String.valueOf(dish.optInt("dishId")), categoryId, branchId,
                dish.optString("name"), Math.round(dish.optDouble("price")), 0, 0,
                dish.optString("description"), String.join(", ", ingredients), "", "", 0,
                dish.optBoolean("available", true), false, Collections.singletonList("Tiêu chuẩn"),
                new ArrayList<>(), image, 0);
    }

    private User parseTokenUser(JSONObject row) {
        if (row == null) return new User();
        return new User(String.valueOf(row.optInt("customerId")), row.optString("name"),
                row.optString("email"), row.optString("phoneNumber"), "Thành viên",
                row.optInt("loyaltyPoints"), null);
    }

    private User parseProfile(JSONObject row) {
        return new User(String.valueOf(row.optInt("customerId")), row.optString("name"),
                row.optString("email"), row.optString("phoneNumber"), row.optString("membershipTier", "Thành viên"),
                row.optInt("loyaltyPoints"), null);
    }

    private Reservation parseReservation(JSONObject row) {
        if (row == null) return new Reservation();
        String branchId = String.valueOf(row.optInt("branchId")); Branch branch = state.findBranch(branchId);
        String reserved = row.optString("reservedAt");
        String date = DisplayFormatters.date(reserved);
        String time = DisplayFormatters.time(reserved);
        String note = row.isNull("note") ? "" : row.optString("note", "");
        int tableId = row.isNull("tableId") ? 0 : row.optInt("tableId");
        JSONArray assignedTables = row.optJSONArray("assignedTables");
        if (tableId <= 0 && assignedTables != null && assignedTables.length() > 0) {
            tableId = assignedTables.optJSONObject(0) == null ? 0 : assignedTables.optJSONObject(0).optInt("tableId");
        }
        Reservation reservation = new Reservation(String.valueOf(row.optInt("reservationId")), branchId,
                branch == null ? "Chi nhánh " + branchId : branch.getName(), branch == null ? "" : branch.getAddress(),
                date, time, row.optInt("partySize"), "", tableId > 0 ? String.valueOf(tableId) : "", note,
                new ArrayList<>(), 0, reservationStatus(row.optString("status")), row.optJSONArray("preOrderItems") != null && row.optJSONArray("preOrderItems").length() > 0,
                row.optString("customerName"), row.optString("phoneNumber"));
        reservation.setTableLabel(tableLabel(branchId, reservation.getTableId()));
        return reservation;
    }

    private DiningSession parseSession(JSONObject row) {
        String branchId = String.valueOf(row.optInt("branchId")); Branch branch = state.findBranch(branchId);
        User user = state.getCurrentUser();
        DiningSession session = new DiningSession(String.valueOf(row.optInt("diningSessionId")), branchId,
                branch == null ? "Chi nhánh " + branchId : branch.getName(), String.valueOf(row.optInt("tableId")),
                sessionStatus(row.optString("status")), row.isNull("reservationId") ? null : String.valueOf(row.optInt("reservationId")),
                user == null ? "Khách hàng" : user.getFullName(), row.optString("startedAtUtc"), new ArrayList<>(), new ArrayList<>());
        session.setTableLabel(tableLabel(branchId, session.getTableId()));
        return session;
    }

    private ApiClient.Callback sessionCallback(Callback<DiningSession> callback) {
        return tableAware(new ApiClient.Callback() {
            @Override public void onSuccess(Object body) { DiningSession session = parseSession((JSONObject) body); state.setActiveDiningSession(session); callback.onSuccess(session); }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    private Order parseOrder(JSONObject row) {
        List<OrderItem> items = new ArrayList<>(); JSONArray itemRows = row.optJSONArray("items");
        if (itemRows != null) for (int i = 0; i < itemRows.length(); i++) {
            JSONObject item = itemRows.optJSONObject(i);
            items.add(new OrderItem(String.valueOf(item.optInt("itemId")), item.optString("dishName"),
                    Math.round(item.optDouble("unitPrice")), item.optInt("quantity"), item.optString("note"),
                    orderItemStatus(item.optString("status")), ""));
        }
        DiningSession activeSession = state.getActiveDiningSession();
        JSONObject sessionRow = row.optJSONObject("diningSession");
        String sessionId = row.isNull("diningSessionId") ? "" : String.valueOf(row.optInt("diningSessionId"));
        String branchName = activeSession == null ? "" : activeSession.getBranchName();
        String tableIdValue = row.isNull("tableId") ? "" : String.valueOf(row.optInt("tableId"));
        String tableDisplay = activeSession == null ? "Chưa có thông tin bàn" : activeSession.getTableLabel();
        DiningSessionSummary summary = null;
        if (sessionRow != null) {
            sessionId = String.valueOf(sessionRow.optInt("diningSessionId"));
            String branchId = String.valueOf(sessionRow.optInt("branchId"));
            Branch branch = state.findBranch(branchId);
            branchName = branch == null ? "Chi nhánh " + branchId : branch.getName();
            int tableId = sessionRow.optInt("tableId");
            tableIdValue = tableId > 0 ? String.valueOf(tableId) : "";
            tableDisplay = tableLabel(branchId, tableIdValue);
            summary = new DiningSessionSummary(sessionId, sessionRow.optString("sessionCode"), branchId,
                    tableId > 0 ? String.valueOf(tableId) : "", sessionRow.optString("status"),
                    sessionRow.optString("startedAtUtc"), sessionRow.isNull("endedAtUtc") ? null : sessionRow.optString("endedAtUtc"));
            summary.setTableLabel(tableDisplay);
        }
        Order order = new Order(String.valueOf(row.optInt("orderId")), sessionId,
                branchName, tableIdValue, Order.Type.DINE_IN,
                orderStatus(row.optString("status")), row.optString("orderTime"), items, Math.round(row.optDouble("subtotal")), "");
        order.setDiningSessionSummary(summary);
        order.setTableLabel(tableDisplay);
        return order;
    }

    private ServiceRequest parseServiceRequest(JSONObject row) {
        ServiceRequest.Status status = serviceRequestStatus(row.optString("status"));
        return new ServiceRequest(String.valueOf(row.optInt("serviceRequestId")), row.optString("requestType"), row.optString("note"), status, row.optString("createdAtUtc"));
    }

    static ServiceRequest.Status serviceRequestStatus(String value) {
        String rawStatus = value == null ? "" : value.toUpperCase(Locale.ROOT);
        try {
            if ("OPEN".equals(rawStatus)) return ServiceRequest.Status.REQUESTED;
            if ("ACKNOWLEDGED".equals(rawStatus)) return ServiceRequest.Status.ACCEPTED;
            return ServiceRequest.Status.valueOf(rawStatus);
        } catch (Exception e) { return ServiceRequest.Status.REQUESTED; }
    }

    private Invoice invoiceFromQuote(JSONObject quote) {
        DiningSession session = state.getActiveDiningSession(); long subtotal = Math.round(quote.optDouble("subtotal"));
        long voucher = Math.round(quote.optDouble("voucherDiscount")); long points = Math.round(quote.optDouble("pointsDiscount"));
        Invoice invoice = new Invoice("QUOTE-" + quote.optInt("checkoutQuoteId"), String.valueOf(quote.optInt("diningSessionId")),
                session == null ? "VietTable" : session.getBranchName(), session == null ? "" : session.getTableId(), subtotal,
                0, 0, voucher, points, Math.round(quote.optDouble("totalAmount")), "Chưa chọn", "", quote.optString("expiresAtUtc"));
        invoice.setTableLabel(session == null ? "Chưa có thông tin bàn" : session.getTableLabel());
        return invoice;
    }

    private Invoice invoiceFromIssued(JSONObject value) {
        DiningSession session = state.getActiveDiningSession();
        Invoice invoice = new Invoice(value.optString("invoiceCode", String.valueOf(value.optInt("invoiceId"))),
                String.valueOf(value.optInt("diningSessionId")), session == null ? "VietTable" : session.getBranchName(),
                session == null ? String.valueOf(value.optInt("tableId")) : session.getTableId(), Math.round(value.optDouble("subtotal")), 0, 0,
                Math.round(value.optDouble("discount")), 0, Math.round(value.optDouble("totalAmount")),
                "Đã thanh toán", "", value.optString("issuedAtUtc"));
        invoice.setBackendInvoiceId(value.optInt("invoiceId"));
        invoice.setTableLabel(session == null ? tableLabel(String.valueOf(value.optInt("branchId")), String.valueOf(value.optInt("tableId"))) : session.getTableLabel());
        return invoice;
    }

    private ApiClient.Callback splitCallback(Callback<SplitBill> callback) {
        return new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                currentSplitBill = parseSplitBill((JSONObject) body);
                callback.onSuccess(currentSplitBill);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        };
    }

    private SplitBill parseSplitBill(JSONObject row) {
        List<SplitBill.Part> parts = new ArrayList<>();
        JSONArray values = row == null ? null : row.optJSONArray("parts");
        if (values != null) for (int i = 0; i < values.length(); i++) {
            JSONObject part = values.optJSONObject(i); if (part == null) continue;
            Integer paymentId = part.isNull("paymentId") ? null : part.optInt("paymentId");
            parts.add(new SplitBill.Part(part.optInt("splitBillPartId"), part.optString("label"),
                    Math.round(part.optDouble("amount")), part.optString("status"), paymentId,
                    part.optString("paymentStatus", null)));
        }
        return new SplitBill(row.optInt("splitBillId"), row.optInt("checkoutQuoteId"),
                row.optInt("diningSessionId"), Math.round(row.optDouble("totalAmount")),
                row.optString("status"), parts);
    }

    private PaymentInfo parsePayment(JSONObject row) {
        return new PaymentInfo(row.optInt("paymentId"), row.optInt("checkoutQuoteId"),
                row.optInt("diningSessionId"), Math.round(row.optDouble("amount")),
                row.optString("channel"), row.optString("method"), row.optString("location", null),
                row.optString("status"), row.optString("currency", "VND"), row.optString("providerName", null),
                row.optString("providerReference", null), row.optString("providerTransactionId", null),
                row.optString("paymentUrl", null), row.optString("providerExpiresAtUtc", null));
    }

    private static Review parseReview(JSONObject row) {
        JSONArray images = row.optJSONArray("imageUrls"); List<String> imageUrls = new ArrayList<>();
        if (images != null) for (int i = 0; i < images.length(); i++) imageUrls.add(images.optString(i));
        return new Review(row.optInt("reviewId"), row.optInt("invoiceId"), row.optInt("diningSessionId"),
                row.optInt("overallRating"), row.optInt("foodRating"), row.optInt("serviceRating"),
                row.optInt("spaceRating"), row.optString("comment", null), imageUrls,
                row.optString("createdAtUtc", null), row.optString("updatedAtUtc", null));
    }

    private static Reservation.Status reservationStatus(String value) {
        String normalized = value.replace("CheckedIn", "CHECKED_IN").replace("CheckingIn", "CHECKING_IN")
                .replace("NoShow", "NO_SHOW").replace("PendingDeposit", "PENDING_DEPOSIT").toUpperCase(Locale.ROOT);
        try { return Reservation.Status.valueOf(normalized); } catch (Exception e) { return Reservation.Status.PENDING; }
    }
    private static DiningSession.Status sessionStatus(String value) {
        try { return DiningSession.Status.valueOf(value.toUpperCase(Locale.ROOT)); } catch (Exception e) { return DiningSession.Status.ACTIVE; }
    }
    static Order.Status orderStatus(String value) {
        String v = value == null ? "" : value.toUpperCase(Locale.ROOT);
        if (v.equals("PENDING")) return Order.Status.PLACED;
        if (v.equals("PREPARING") || v.equals("READY")) return Order.Status.IN_PROGRESS;
        if (v.equals("SERVING")) return Order.Status.COMPLETED;
        try { return Order.Status.valueOf(v); } catch (Exception e) { return Order.Status.PLACED; }
    }
    static OrderItem.Status orderItemStatus(String value) {
        String v = value == null ? "" : value.toUpperCase(Locale.ROOT);
        if (v.equals("SERVING")) return OrderItem.Status.SERVED;
        try { return OrderItem.Status.valueOf(v); } catch (Exception e) { return OrderItem.Status.PENDING; }
    }
    private static String normalizeServiceType(String value) {
        return ServiceRequestType.normalize(value);
    }
    private static String reservationInstant(ReservationDraft draft) {
        return reservationInstant(draft.getDate(), draft.getTimeSlot());
    }

    private static String reservationInstant(String dateText, String timeText) {
        Matcher matcher = Pattern.compile("(\\d{1,2})/(\\d{1,2})(?:/(\\d{4}))?").matcher(dateText);
        LocalDate date = LocalDate.now().plusDays(1);
        if (matcher.find()) {
            int day = Integer.parseInt(matcher.group(1)); int month = Integer.parseInt(matcher.group(2));
            int year = matcher.group(3) == null ? LocalDate.now().getYear() : Integer.parseInt(matcher.group(3));
            date = LocalDate.of(year, month, day); if (date.isBefore(LocalDate.now())) date = date.plusYears(1);
        }
        String[] hm = timeText.split(":");
        LocalDateTime local = date.atTime(Integer.parseInt(hm[0]), Integer.parseInt(hm[1]));
        return local.atZone(ZoneId.systemDefault()).toOffsetDateTime().toString();
    }
    private ApiClient.Callback objectCallback(Callback<JSONObject> callback) {
        return new ApiClient.Callback() {
            @Override public void onSuccess(Object body) { callback.onSuccess((JSONObject) body); }
            @Override public void onError(ApiException error) { callback.onError(error); }
        };
    }
    private static String key(String prefix) { return prefix + "-" + UUID.randomUUID(); }
    private static String stableKey(String prefix, String identity) {
        return prefix + "-" + UUID.nameUUIDFromBytes(identity.getBytes(StandardCharsets.UTF_8));
    }

    private ApiException jsonError() {
        return new ApiException(0, "client_json_error", "Không thể tạo dữ liệu gửi lên máy chủ.", null);
    }

    private static <T> void finishSuccess(Callback<T> callback, T value) { if (callback != null) callback.onSuccess(value); }
    private static void finishError(Callback<?> callback, ApiException error) { if (callback != null) callback.onError(error); }
}
