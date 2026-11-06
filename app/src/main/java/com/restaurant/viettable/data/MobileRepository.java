package com.restaurant.viettable.data;

import androidx.annotation.Nullable;
import com.restaurant.viettable.BuildConfig;
import com.restaurant.viettable.network.ApiClient;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.network.BiometricTokenStore;
import com.restaurant.viettable.util.BiometricLoginPolicy;
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
import com.restaurant.viettable.model.ReviewInputPolicy;
import com.restaurant.viettable.model.ServiceRequest;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.InvoiceHistoryItem;
import com.restaurant.viettable.model.PaymentInfo;
import com.restaurant.viettable.model.SplitBill;
import com.restaurant.viettable.model.SplitPaymentResult;
import com.restaurant.viettable.model.Voucher;
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.util.DisplayFormatters;
import com.restaurant.viettable.util.ReservationSchedule;
import com.restaurant.viettable.util.StatusLabels;

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
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import com.restaurant.viettable.util.NotificationRules;
import com.restaurant.viettable.util.SafeText;

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
    private final List<Callback<DiningSession>> activeSessionRequests = new ArrayList<>();
    private final java.util.Map<String, List<Callback<List<Order>>>> orderRequests = new java.util.HashMap<>();
    private final List<Callback<Void>> bootstrapRequests = new ArrayList<>();
    private boolean bootstrapLoading;
    private final PublicCatalogCache publicCatalog = new PublicCatalogCache();

    public void initializePublicCatalog(android.content.Context context, Runnable ready) {
        publicCatalog.initialize(context, () -> {
            long restoredAt = android.os.SystemClock.elapsedRealtime();
            Object branches = publicCatalog.read("branches");
            if (branches instanceof JSONArray) applyBranches((JSONArray) branches);
            Branch selected = state.getSelectedBranch();
            if (selected != null) {
                Object menu = publicCatalog.read("menu_" + selected.getId());
                if (menu instanceof JSONObject) applyMenu(selected.getId(), (JSONObject) menu);
            }
            android.util.Log.i("CatalogPerf", "restore branch=" + (selected == null ? "none" : selected.getId())
                    + " categories=" + state.getCategories().size() + " dishes=" + state.getFoodItems().size()
                    + " mapMs=" + (android.os.SystemClock.elapsedRealtime() - restoredAt));
            ready.run();
        });
    }
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
                    String token = SafeText.json(((JSONObject) value), "resetToken", "");
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
                setSessionUser(user);
                state.setLoggedIn(true);
                SignalRManager.getInstance().startIfAuthenticated();
                callback.onSuccess(user);
                refreshBootstrap(null);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        };
    }

    public void refreshBootstrap(Callback<Void> callback) {
        bootstrapRequests.add(callback);
        if (bootstrapLoading) return;
        bootstrapLoading = true;
        // These reads are independent. A Catalog/favorites/reservations failure must never
        // prevent REST from restoring (or clearing) the actual dining session.
        int[] remaining = {4};
        ApiException[] firstError = {null};
        Callback<Void> completed = new Callback<Void>() {
            @Override public void onSuccess(Void value) { finish(null); }
            @Override public void onError(ApiException error) { finish(error); }
            private void finish(ApiException error) {
                if (error != null && firstError[0] == null) firstError[0] = error;
                if (--remaining[0] != 0) return;
                List<Callback<Void>> callbacks = new ArrayList<>(bootstrapRequests);
                bootstrapRequests.clear();
                bootstrapLoading = false;
                for (Callback<Void> item : callbacks) {
                    if (firstError[0] == null) finishSuccess(item, null);
                    else finishError(item, firstError[0]);
                }
            }
        };
        loadActiveSession(new Callback<DiningSession>() {
            @Override public void onSuccess(DiningSession value) { completed.onSuccess(null); }
            @Override public void onError(ApiException error) { completed.onError(error); }
        });
        loadReservations(new Callback<List<Reservation>>() {
            @Override public void onSuccess(List<Reservation> value) { completed.onSuccess(null); }
            @Override public void onError(ApiException error) { completed.onError(error); }
        });
        loadProfile(new Callback<User>() {
            @Override public void onSuccess(User value) { completed.onSuccess(null); }
            @Override public void onError(ApiException error) {
                if (error.getStatusCode() == 401) state.setLoggedIn(false);
                completed.onError(error);
            }
        });
        loadBranches(new Callback<List<Branch>>() {
            @Override public void onSuccess(List<Branch> value) {
                loadSelectedBranchMenu(completed);
            }
            @Override public void onError(ApiException error) { completed.onError(error); }
        });
    }

    private void loadSelectedBranchMenu(Callback<Void> callback) {
        Branch selected = state.getSelectedBranch();
        if (selected == null) { finishSuccess(callback, null); return; }
        loadMenu(selected.getId(), true, new Callback<Void>() {
            @Override public void onSuccess(Void value) { loadFavorites(new Callback<Void>() {
                // Favorites decorate menu instances, so only this dependency remains ordered.
                @Override public void onSuccess(Void ignored) { finishSuccess(callback, null); }
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
                publicCatalog.write("branches", body);
                List<Branch> branches = applyBranches((JSONArray) body);
                finishBranchRequests(branches, null);
            }
            @Override public void onError(ApiException error) { finishBranchRequests(null, error); }
        });
    }

    private List<Branch> applyBranches(JSONArray rows) {
                List<Branch> branches = new ArrayList<>();
                for (int i = 0; i < rows.length(); i++) {
                    JSONObject row = rows.optJSONObject(i);
                    if (row == null) continue;
                    // Coordinates only when the admin entered them (owner 06/11); 0/0 = none.
                    double latitude = row.isNull("latitude") ? 0 : row.optDouble("latitude", 0);
                    double longitude = row.isNull("longitude") ? 0 : row.optDouble("longitude", 0);
                    Branch branch = new Branch(String.valueOf(row.optInt("branchId")), SafeText.json(row, "name"),
                            SafeText.json(row, "location"), "", "", 0, true, "", new ArrayList<>(),
                            0, 0, latitude, longitude);
                    branch.setImageUrl(api().absoluteUrl(SafeText.json(row, "image", "")));
                    branches.add(branch);
                }
                state.replaceBranches(branches);
                return branches;
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
        loadMenu(branchId, false, callback);
    }

    public void loadMenu(String branchId, boolean forceRefresh, Callback<Void> callback) {
        Object cached = publicCatalog.read("menu_" + branchId);
        if (cached instanceof JSONObject && !branchId.equals(state.getCatalogBranchId()))
            applyMenu(branchId, (JSONObject) cached);
        if (!forceRefresh && publicCatalog.fresh("menu_" + branchId)) {
            finishSuccess(callback, null);
            return;
        }
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
                if (response.optJSONArray("categories") == null) { onError(jsonError()); return; }
                publicCatalog.write("menu_" + branchId, response);
                applyMenu(branchId, response);
                finishMenuRequest(branchId, null);
            }
            @Override public void onError(ApiException error) {
                state.failCatalogLoad(branchId, error.getMessage());
                finishMenuRequest(branchId, error);
            }
        });
    }

    private void applyMenu(String branchId, JSONObject response) {
                long startedAt = android.os.SystemClock.elapsedRealtime();
                JSONArray categoryRows = response.optJSONArray("categories");
                if (categoryRows == null) return;
                List<Category> categories = new ArrayList<>();
                List<FoodItem> foods = new ArrayList<>();
                if (categoryRows != null) for (int i = 0; i < categoryRows.length(); i++) {
                    JSONObject category = categoryRows.optJSONObject(i);
                    if (category == null) continue;
                    String categoryId = String.valueOf(category.optInt("categoryId"));
                    JSONArray dishes = category.optJSONArray("dishes");
                    categories.add(new Category(categoryId, SafeText.json(category, "categoryName"), 0,
                            dishes == null ? 0 : dishes.length()));
                    if (dishes == null) continue;
                    for (int j = 0; j < dishes.length(); j++) {
                        JSONObject dish = dishes.optJSONObject(j);
                        if (dish == null) continue;
                        foods.add(parseDish(dish, categoryId, branchId));
                    }
                }
                boolean applied = state.replaceBranchCatalog(branchId, categories, foods);
                // Owner 07/11: warm the menu photos now so "Xem tất cả" / Thực đơn open with them already there.
                List<String> photos = new ArrayList<>();
                for (FoodItem food : foods) if (food.getImageUrl() != null) photos.add(food.getImageUrl());
                com.restaurant.viettable.util.ImageLoader.getInstance().prefetch(photos);
                android.util.Log.i("CatalogPerf", "map branch=" + branchId + " categories=" + categories.size()
                        + " dishes=" + foods.size() + " applied=" + applied + " mapMs="
                        + (android.os.SystemClock.elapsedRealtime() - startedAt));
                if (com.restaurant.viettable.BuildConfig.DEBUG)
                    android.util.Log.d("CatalogLoad", "menu success branch=" + branchId
                            + " categories=" + categories.size() + " dishes=" + foods.size()
                            + " applied=" + applied + " elapsedMs=" + (android.os.SystemClock.elapsedRealtime() - startedAt));
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
                setSessionUser(user);
                state.setLoggedIn(true);
                callback.onSuccess(user);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    private static List<String> benefitList(JSONArray rows) {
        List<String> values = new ArrayList<>();
        if (rows != null) for (int i = 0; i < rows.length(); i++) {
            String text = SafeText.clean(SafeText.json(rows, i));
            if (!text.isEmpty()) values.add(text);
        }
        return values;
    }

    /** Real points, tier and next-tier threshold (GET /me/loyalty); also refreshes the points kept on the user. */
    public void loadLoyalty(Callback<com.restaurant.viettable.model.LoyaltyInfo> callback) {
        api().get("/api/mobile/v1/me/loyalty", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONObject row = (JSONObject) body;
                Integer nextAt = row.isNull("nextTierAtPoints") || !row.has("nextTierAtPoints") ? null : row.optInt("nextTierAtPoints");
                com.restaurant.viettable.model.LoyaltyInfo info = new com.restaurant.viettable.model.LoyaltyInfo(
                        row.optInt("availablePoints"), SafeText.json(row, "tierCode"), SafeText.json(row, "tierName"),
                        SafeText.json(row, "nextTierCode", null), nextAt, row.optInt("pointsToNextTier"), row.optInt("progressPercent"),
                        SafeText.json(row, "cardNumber", ""), benefitList(row.optJSONArray("benefits")));
                User user = state.getCurrentUser();
                if (user != null) user.setLoyaltyPoints(info.getAvailablePoints());
                callback.onSuccess(info);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    /**
     * The server replaces the whole profile with the request, so the birthday, gender and address the profile already
     * has are always sent back (a null birthday clears it on purpose only when the guest removed it).
     */
    public void updateProfile(String name, String phone, String email, @Nullable String dateOfBirth, Callback<User> callback) {
        try {
            User current = state.getCurrentUser();
            JSONObject body = new JSONObject().put("name", name).put("phoneNumber", phone).put("email", email)
                    .put("dateOfBirth", dateOfBirth == null ? JSONObject.NULL : dateOfBirth)
                    .put("gender", current == null || current.getGender() == null ? JSONObject.NULL : current.getGender())
                    .put("address", current == null || current.getAddress() == null ? JSONObject.NULL : current.getAddress());
            api().put("/api/mobile/v1/me", body, true, null, new ApiClient.Callback() {
                @Override public void onSuccess(Object value) {
                    User user = parseProfile((JSONObject) value);
                    setSessionUser(user);
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
                List<NotificationItem> values = parseNotificationRows(rows);
                NotificationInboxStore.getInstance().remember(BiometricTokenStore.getInstance().sessionAccount(), rows);
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
            if (!draft.getContactName().isEmpty()) body.put("contactName", draft.getContactName());
            if (!draft.getContactPhone().isEmpty()) body.put("contactPhone", draft.getContactPhone());
            int selectedTable = Integer.parseInt(draft.getTableId());
            body.put("tableId", selectedTable > 0 ? selectedTable : JSONObject.NULL);
            if (draft.getGroupTableIds().size() > 1) {
                JSONArray group = new JSONArray();
                for (String id : draft.getGroupTableIds()) group.put(Integer.parseInt(id));
                body.put("tableIds", group);
            }
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

    /**
     * Every table of the branch for that time and party, each with its state (reservation item 5). The server
     * releases the reservation being changed (excludeReservationId) so its own table shows as free.
     */
    private com.restaurant.viettable.model.TableCombination lastTableCombination;

    /** The group proposed by the last table-map read, or null. */
    public com.restaurant.viettable.model.TableCombination tableCombination() { return lastTableCombination; }

    // ---- Waitlist (owner 06/11) ----

    public void loadWaitlist(Callback<List<com.restaurant.viettable.model.WaitlistEntry>> callback) {
        api().get("/api/mobile/v1/reservations/waitlist", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray rows = ((JSONObject) body).optJSONArray("items");
                List<com.restaurant.viettable.model.WaitlistEntry> items = new ArrayList<>();
                if (rows != null) for (int i = 0; i < rows.length(); i++) {
                    JSONObject row = rows.optJSONObject(i);
                    if (row != null) items.add(parseWaitlist(row));
                }
                callback.onSuccess(items);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    /** windowStart/End: "HH:mm" on the draft's date. */
    public void joinWaitlist(ReservationDraft draft, String windowStart, String windowEnd, String note,
                             Callback<com.restaurant.viettable.model.WaitlistEntry> callback) {
        JSONObject body = new JSONObject();
        try {
            body.put("branchId", Integer.parseInt(draft.getBranch().getId()))
                    .put("windowStart", reservationInstant(draft.getDate(), windowStart))
                    .put("windowEnd", reservationInstant(draft.getDate(), windowEnd))
                    .put("partySize", draft.getGuestCount())
                    .put("note", note == null ? JSONObject.NULL : note)
                    .put("contactName", draft.getContactName().isEmpty() ? JSONObject.NULL : draft.getContactName())
                    .put("contactPhone", draft.getContactPhone().isEmpty() ? JSONObject.NULL : draft.getContactPhone());
        } catch (Exception e) { callback.onError(new ApiException(400, "validation_failed", "Thông tin chờ bàn không hợp lệ.", null)); return; }
        api().post("/api/mobile/v1/reservations/waitlist", body, true, null, new ApiClient.Callback() {
            @Override public void onSuccess(Object value) { callback.onSuccess(parseWaitlist((JSONObject) value)); }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void acceptWaitlist(String entryId, Callback<Reservation> callback) {
        api().post("/api/mobile/v1/reservations/waitlist/" + entryId + "/accept", new JSONObject(), true, null,
                tableAware(new ApiClient.Callback() {
                    @Override public void onSuccess(Object value) {
                        Reservation created = parseReservation((JSONObject) value); state.upsertReservation(created); callback.onSuccess(created);
                    }
                    @Override public void onError(ApiException error) { callback.onError(error); }
                }));
    }

    /** "Xếp lại hàng chờ" after an offer lapsed: a new place at the end of the queue, same window. */
    public void rejoinWaitlist(String entryId, Callback<com.restaurant.viettable.model.WaitlistEntry> callback) {
        api().post("/api/mobile/v1/reservations/waitlist/" + entryId + "/rejoin", new JSONObject(), true, null, new ApiClient.Callback() {
            @Override public void onSuccess(Object value) { callback.onSuccess(parseWaitlist((JSONObject) value)); }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void cancelWaitlist(String entryId, Callback<Void> callback) {
        api().post("/api/mobile/v1/reservations/waitlist/" + entryId + "/cancel", new JSONObject(), true, null, new ApiClient.Callback() {
            @Override public void onSuccess(Object value) { callback.onSuccess(null); }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    private com.restaurant.viettable.model.WaitlistEntry parseWaitlist(JSONObject row) {
        com.restaurant.viettable.model.WaitlistEntry e = new com.restaurant.viettable.model.WaitlistEntry();
        e.id = String.valueOf(row.optInt("waitlistEntryId"));
        e.branchId = String.valueOf(row.optInt("branchId"));
        Branch branch = state.findBranch(e.branchId);
        e.branchName = row.isNull("branchName") ? (branch == null ? "Chi nhánh " + e.branchId : branch.getName()) : SafeText.json(row, "branchName");
        e.windowStartUtc = SafeText.json(row, "windowStartUtc", null);
        e.windowEndUtc = SafeText.json(row, "windowEndUtc", null);
        e.partySize = row.optInt("partySize");
        e.note = row.isNull("note") ? null : SafeText.json(row, "note", null);
        e.status = SafeText.json(row, "status", "Waiting");
        e.position = row.optInt("position");
        e.offeredReservationId = row.isNull("offeredReservationId") ? null : String.valueOf(row.optInt("offeredReservationId"));
        e.offeredTableId = row.isNull("offeredTableId") ? null : String.valueOf(row.optInt("offeredTableId"));
        e.offeredReservedAtUtc = row.isNull("offeredReservedAtUtc") ? null : SafeText.json(row, "offeredReservedAtUtc", null);
        e.offerExpiresAtUtc = row.isNull("offerExpiresAtUtc") ? null : SafeText.json(row, "offerExpiresAtUtc", null);
        e.closedReason = row.isNull("closedReason") ? null : SafeText.json(row, "closedReason", null);
        return e;
    }

    /** "Hết bàn?" (owner 06/11): nearest other times and other branches with room, from the server. */
    public void loadAvailabilityAlternatives(ReservationDraft draft, String excludeReservationId,
                                             Callback<com.restaurant.viettable.model.AvailabilityAlternatives> callback) {
        if (draft == null || draft.getBranch() == null) { callback.onError(new ApiException(400, "validation_failed", "Chưa chọn chi nhánh.", null)); return; }
        String path = "/api/mobile/v1/reservations/availability?branchId=" + draft.getBranch().getId()
                + "&reservedAt=" + encode(reservationInstant(draft)) + "&partySize=" + draft.getGuestCount()
                + (excludeReservationId == null || excludeReservationId.isEmpty() ? "" : "&excludeReservationId=" + excludeReservationId);
        api().get(path, true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONObject row = (JSONObject) body;
                List<com.restaurant.viettable.model.AvailabilityOption> times = new ArrayList<>();
                JSONArray timeRows = row.optJSONArray("timeSuggestions");
                if (timeRows != null) for (int i = 0; i < timeRows.length(); i++) {
                    JSONObject x = timeRows.optJSONObject(i);
                    if (x == null || x.isNull("localTime")) continue;
                    times.add(new com.restaurant.viettable.model.AvailabilityOption(null, null,
                            SafeText.json(x, "localDate"), SafeText.json(x, "localTime"), x.optInt("availableTableCount"), null));
                }
                List<com.restaurant.viettable.model.AvailabilityOption> branches = new ArrayList<>();
                JSONArray branchRows = row.optJSONArray("branchSuggestions");
                if (branchRows != null) for (int i = 0; i < branchRows.length(); i++) {
                    JSONObject x = branchRows.optJSONObject(i);
                    if (x == null) continue;
                    branches.add(new com.restaurant.viettable.model.AvailabilityOption(String.valueOf(x.optInt("branchId")),
                            SafeText.json(x, "name"), SafeText.json(x, "localDate"), SafeText.json(x, "localTime"),
                            x.optInt("availableTableCount"), x.isNull("distanceKm") ? null : x.optDouble("distanceKm")));
                }
                callback.onSuccess(new com.restaurant.viettable.model.AvailabilityAlternatives(row.optBoolean("available"), times, branches));
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void loadTableMap(ReservationDraft draft, String excludeReservationId, Callback<List<com.restaurant.viettable.model.TableMapItem>> callback) {
        try {
            if (draft.getBranch() == null) throw new IllegalArgumentException("missing branch");
            String instant = URLEncoder.encode(reservationInstant(draft), StandardCharsets.UTF_8.name());
            String path = "/api/mobile/v1/branches/" + draft.getBranch().getId() + "/availability?reservedAt="
                    + instant + "&partySize=" + draft.getGuestCount()
                    + (excludeReservationId == null ? "" : "&excludeReservationId=" + excludeReservationId);
            String branchId = draft.getBranch().getId();
            api().get(path, true, new ApiClient.Callback() {
                @Override public void onSuccess(Object body) {
                    JSONObject response = (JSONObject) body;
                    List<com.restaurant.viettable.model.TableMapItem> values = new ArrayList<>();
                    JSONArray rows = response.optJSONArray("tables");
                    boolean fullMap = rows != null;
                    if (!fullMap) rows = response.optJSONArray("availableTables"); // older server: free tables only
                    if (rows != null) for (int i = 0; i < rows.length(); i++) {
                        JSONObject row = rows.optJSONObject(i);
                        if (row == null) continue;
                        tableNumbers.put(branchId, String.valueOf(row.optInt("tableId")), row.optInt("tableNumber"));
                        values.add(new com.restaurant.viettable.model.TableMapItem(String.valueOf(row.optInt("tableId")),
                                row.optInt("tableNumber"), row.optInt("numberOfSeats"),
                                fullMap ? SafeText.json(row, "status", "INACTIVE") : com.restaurant.viettable.model.TableMapItem.AVAILABLE,
                                fullMap ? SafeText.json(row, "statusLabel", "") : "Trống"));
                    }
                    // No single table seats the party: the server's pushed-together tables (owner 06/11).
                    JSONObject combo = response.optJSONObject("combination");
                    lastTableCombination = null;
                    if (combo != null && combo.optJSONArray("tableIds") != null) {
                        List<String> ids = new ArrayList<>();
                        JSONArray idRows = combo.optJSONArray("tableIds");
                        for (int i = 0; i < idRows.length(); i++) ids.add(String.valueOf(idRows.optInt(i)));
                        if (ids.size() > 1) lastTableCombination = new com.restaurant.viettable.model.TableCombination(ids,
                                SafeText.json(combo, "label", ""), combo.optInt("seats"));
                    }
                    callback.onSuccess(values);
                }
                @Override public void onError(ApiException error) { callback.onError(error); }
            });
        } catch (Exception e) {
            callback.onError(new ApiException(0, "invalid_reservation_time", "Không thể kiểm tra bàn cho ngày giờ đã chọn.", null));
        }
    }

    /** The reminder's buttons: response is "will_come" or "cancel" (owner 06/11). */
    /** Booking hours from the server: {openFrom, lastSlot} and the slot length (owner 06/11: nothing hard-coded). */
    public interface HoursCallback { void onHours(java.time.LocalTime openFrom, java.time.LocalTime lastSlot, int slotMinutes); }

    public void loadBookingHours(HoursCallback callback) {
        api().get("/api/mobile/v1/reservations/hours", false, new ApiClient.Callback() {
            @Override public void onSuccess(Object value) {
                try {
                    JSONObject row = (JSONObject) value;
                    callback.onHours(java.time.LocalTime.parse(row.getString("openFrom")), java.time.LocalTime.parse(row.getString("lastSlot")),
                            row.optInt("slotMinutes", 30));
                } catch (Exception ignored) { /* the server still checks the hours when booking */ }
            }
            @Override public void onError(ApiException error) { /* same: the server checks */ }
        });
    }

    /** "Gợi ý món" (06/11): dishes for a group and a budget, priced by the server like the bill. */
    public void suggestMenu(String branchId, int people, long budget, int vegetarians, boolean avoidSpicy,
                            Callback<com.restaurant.viettable.model.MenuSuggestion> callback) {
        JSONObject body = new JSONObject();
        try {
            body.put("branchId", Integer.parseInt(branchId)).put("people", people).put("budget", budget)
                    .put("vegetarians", vegetarians).put("avoidSpicy", avoidSpicy);
        } catch (Exception e) { callback.onError(new ApiException(400, "validation_failed", "Chưa chọn chi nhánh.", null)); return; }
        api().post("/api/mobile/v1/menu/suggestions", body, false, null, new ApiClient.Callback() {
            @Override public void onSuccess(Object value) { callback.onSuccess(com.restaurant.viettable.model.MenuSuggestion.parse((JSONObject) value)); }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    /** New server total after the guest swapped a dish. */
    public void estimateMenu(String branchId, com.restaurant.viettable.model.MenuSuggestion suggestion,
                             Callback<com.restaurant.viettable.model.MenuSuggestion.Totals> callback) {
        api().post("/api/mobile/v1/menu/estimate", suggestion.estimateBody(branchId), false, null, new ApiClient.Callback() {
            @Override public void onSuccess(Object value) { callback.onSuccess(com.restaurant.viettable.model.MenuSuggestion.Totals.parse((JSONObject) value)); }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void answerReminder(String reservationId, String response, Callback<Reservation> callback) {
        JSONObject body = new JSONObject();
        try { body.put("response", response); } catch (Exception ignored) { }
        api().post("/api/mobile/v1/reservations/" + reservationId + "/arrival-confirmation", body, true, null,
                new ApiClient.Callback() {
                    @Override public void onSuccess(Object value) {
                        Reservation updated = parseReservation((JSONObject) value); state.upsertReservation(updated); callback.onSuccess(updated);
                    }
                    @Override public void onError(ApiException error) { callback.onError(error); }
                });
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
        updateReservation(reservation, date, time, partySize, note, reservation.getTableId(), callback);
    }

    /** tableId: the table chosen on the map (the current one when it is still free). */
    public void updateReservation(Reservation reservation, String date, String time, int partySize,
                                  String note, String chosenTableId, Callback<Reservation> callback) {
        try {
            JSONObject body = new JSONObject().put("partySize", partySize)
                    .put("reservedAt", reservationInstant(date, time)).put("note", note);
            int tableId = chosenTableId == null || chosenTableId.isEmpty() ? 0 : Integer.parseInt(chosenTableId);
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
                                    SafeText.json(row, "dishNameSnapshot"), Math.round(row.optDouble("unitPriceSnapshot")),
                                    0, 0, "", "", "", "", 0, true, false,
                                    Collections.singletonList("Tiêu chuẩn"), new ArrayList<>(), "", 0);
                            cart.add(new CartItem(food, row.optInt("quantity"), "Tiêu chuẩn",
                                    new ArrayList<>(), 0, SafeText.json(row, "note")));
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
        activeSessionRequests.add(callback);
        if (activeSessionRequests.size() > 1) return;
        long started = android.os.SystemClock.elapsedRealtime();
        api().get("/api/mobile/v1/dining-sessions/active", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                if (BuildConfig.DEBUG) android.util.Log.i("SessionPerf", "active responseMs="
                        + (android.os.SystemClock.elapsedRealtime() - started));
                DiningSession session = parseSession((JSONObject) body); state.setActiveDiningSession(session);
                if (BuildConfig.DEBUG) android.util.Log.i("SessionPerf", "active card stateMs="
                        + (android.os.SystemClock.elapsedRealtime() - started));
                // REST already verified this session. Catalog display metadata must
                // not delay restoration or current-order reads; never infer a table
                // number from its identity. Ignore enrichment of replaced sessions.
                String branchId = String.valueOf(((JSONObject) body).optInt("branchId"));
                loadTableNumbers(branchId, () -> {
                    if (state.getActiveDiningSession() != session) return;
                    session.setTableLabel(tableLabel(branchId, session.getTableId()));
                    state.setActiveDiningSession(session);
                });
                loadOrders("active", new Callback<List<Order>>() {
                    @Override public void onSuccess(List<Order> value) {
                        if (BuildConfig.DEBUG) android.util.Log.i("SessionPerf", "orders completionMs="
                                + (android.os.SystemClock.elapsedRealtime() - started));
                        finishActiveSession(state.getActiveDiningSession(), null);
                    }
                    @Override public void onError(ApiException error) { finishActiveSession(session, null); }
                });
            }
            @Override public void onError(ApiException error) {
                if (BuildConfig.DEBUG) android.util.Log.i("SessionPerf", "active status="
                        + error.getStatusCode() + " responseMs="
                        + (android.os.SystemClock.elapsedRealtime() - started));
                if (error.getStatusCode() == 404) { state.setActiveDiningSession(null); finishActiveSession(null, null); }
                else finishActiveSession(null, error);
            }
        });
    }

    private void finishActiveSession(DiningSession session, ApiException error) {
        List<Callback<DiningSession>> callbacks = new ArrayList<>(activeSessionRequests);
        activeSessionRequests.clear();
        for (Callback<DiningSession> callback : callbacks) {
            if (error == null) finishSuccess(callback, session); else finishError(callback, error);
        }
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

    /** Leaves a table with no order rounds. Unsent cart items are kept for the next table. */
    public void cancelEmptySession(Callback<Void> callback) {
        DiningSession session = state.getActiveDiningSession();
        if (session == null) { callback.onError(new ApiException(0, "session_required", "Chưa có phiên phục vụ.", null)); return; }
        api().post("/api/mobile/v1/dining-sessions/" + session.getId() + "/cancel", new JSONObject(), true, null, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                if (state.getActiveDiningSession() == session) state.setActiveDiningSession(null);
                callback.onSuccess(null);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void cancelPaymentRequest(Callback<DiningSession> callback) {
        DiningSession session = state.getActiveDiningSession();
        if (session == null) { callback.onError(new ApiException(0, "session_required", "Chưa có phiên phục vụ.", null)); return; }
        api().post("/api/mobile/v1/dining-sessions/" + session.getId() + "/cancel-payment-request", new JSONObject(), true, null, sessionCallback(callback));
    }

    public void loadOrders(String scope, Callback<List<Order>> callback) {
        List<Callback<List<Order>>> pending = orderRequests.get(scope);
        if (pending != null) { pending.add(callback); return; }
        pending = new ArrayList<>(); pending.add(callback); orderRequests.put(scope, pending);
        api().get("/api/mobile/v1/orders?scope=" + scope, true, tableAware(new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray rows = (JSONArray) body; List<Order> values = new ArrayList<>();
                for (int i = 0; i < rows.length(); i++) values.add(parseOrder(rows.optJSONObject(i)));
                state.replaceOrders(scope, values); finishOrderRequests(scope, values, null);
            }
            @Override public void onError(ApiException error) { finishOrderRequests(scope, null, error); }
        }));
    }

    private void finishOrderRequests(String scope, List<Order> orders, ApiException error) {
        List<Callback<List<Order>>> callbacks = orderRequests.remove(scope);
        if (callbacks == null) return;
        for (Callback<List<Order>> callback : callbacks) {
            if (error == null) finishSuccess(callback, orders); else finishError(callback, error);
        }
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
    /** The quote's status as the server last reported it (OPEN, PAYMENT_REQUESTED, ...); empty when there is no quote. */
    public String currentCheckoutQuoteStatus() { return currentQuote == null ? "" : currentQuote.optString("status", ""); }
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

    /** The session's waiting payment (and its quote, kept as the current checkout); null when nothing waits. */
    public void loadActivePayment(Callback<PaymentInfo> callback) {
        DiningSession session = state.getActiveDiningSession();
        if (session == null) { callback.onError(new ApiException(0, "session_required", "Chưa có phiên phục vụ.", null)); return; }
        api().get("/api/mobile/v1/dining-sessions/" + session.getId() + "/active-payment", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONObject value = (JSONObject) body;
                JSONObject quote = value.optJSONObject("quote");
                JSONObject row = value.optJSONObject("payment");
                if (quote == null || row == null) { callback.onSuccess(null); return; }
                currentQuote = quote;
                PaymentInfo payment = parsePayment(row);
                currentPaymentId = payment.getPaymentId();
                callback.onSuccess(payment);
            }
            @Override public void onError(ApiException error) {
                if (error.getStatusCode() == 404) callback.onSuccess(null); else callback.onError(error);
            }
        });
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

    /** One page of "Hoá đơn của tôi" (owner 05/11): newest first, filtered on the server; nextCursor null = last page. */
    public static final class InvoicePage {
        public final List<com.restaurant.viettable.model.Receipt> items;
        public final String nextCursor;
        InvoicePage(List<com.restaurant.viettable.model.Receipt> items, String nextCursor) { this.items = items; this.nextCursor = nextCursor; }
    }

    public void loadInvoicePage(@Nullable String month, int branchId, @Nullable String method, @Nullable String query,
                                @Nullable String cursor, int limit, Callback<InvoicePage> callback) {
        StringBuilder path = new StringBuilder("/api/mobile/v1/invoices?limit=").append(Math.max(1, Math.min(100, limit)));
        if (month != null && !month.isEmpty()) path.append("&month=").append(encode(month));
        if (branchId > 0) path.append("&branchId=").append(branchId);
        if (method != null && !method.isEmpty()) path.append("&method=").append(encode(method));
        if (query != null && !query.trim().isEmpty()) path.append("&q=").append(encode(query.trim()));
        if (cursor != null && !cursor.isEmpty()) path.append("&cursor=").append(encode(cursor));
        api().get(path.toString(), true, tableAware(new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONObject page = (JSONObject) body;
                JSONArray rows = page.optJSONArray("items");
                List<com.restaurant.viettable.model.Receipt> items = new ArrayList<>();
                if (rows != null) for (int i = 0; i < rows.length(); i++) {
                    JSONObject row = rows.optJSONObject(i);
                    if (row != null) items.add(withBranchFallback(com.restaurant.viettable.model.Receipt.fromJson(row)));
                }
                callback.onSuccess(new InvoicePage(items, SafeText.json(page, "nextCursor", null)));
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        }));
    }

    public void loadReceipt(int invoiceId, Callback<com.restaurant.viettable.model.Receipt> callback) {
        api().get("/api/mobile/v1/invoices/" + invoiceId, true, tableAware(new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                callback.onSuccess(withBranchFallback(com.restaurant.viettable.model.Receipt.fromJson((JSONObject) body)));
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        }));
    }

    /** Old invoices (before 05/11) have no branch or table snapshot: show the current name, address and table number from the server's lists. */
    private com.restaurant.viettable.model.Receipt withBranchFallback(com.restaurant.viettable.model.Receipt receipt) {
        if (receipt.branchName == null && receipt.branchId > 0) {
            Branch branch = state.findBranch(String.valueOf(receipt.branchId));
            if (branch != null) {
                receipt.branchName = SafeText.clean(branch.getName()).isEmpty() ? null : branch.getName();
                if (receipt.branchAddress == null && !SafeText.clean(branch.getAddress()).isEmpty()) receipt.branchAddress = branch.getAddress();
            }
        }
        if (receipt.tableName == null && receipt.tableId > 0) {
            String label = tableLabel(String.valueOf(receipt.branchId), String.valueOf(receipt.tableId));
            if (label.startsWith("Bàn ")) receipt.tableName = label; // unknown table: the row stays hidden
        }
        return receipt;
    }

    private static String encode(String value) {
        try { return java.net.URLEncoder.encode(value, "UTF-8"); } catch (java.io.UnsupportedEncodingException e) { return value; }
    }

    /** "Chi tiết món": average, count and latest comments of a dish from real reviews (owner 05/11). */
    public void loadDishReviewSummary(String dishId, Callback<JSONObject> callback) {
        api().get("/api/mobile/v1/dishes/" + encode(dishId) + "/reviews/summary", false, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) { callback.onSuccess((JSONObject) body); }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
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
        InvoiceHistoryItem invoice = new InvoiceHistoryItem(row.optInt("invoiceId"), SafeText.json(row, "invoiceCode"),
                row.optInt("checkoutQuoteId"), row.optInt("paymentId"), row.optInt("diningSessionId"),
                SafeText.json(row, "diningSessionCode"), branch == null ? "Chi nhánh " + branchId : branch.getName(),
                tableId > 0 ? String.valueOf(tableId) : "", Math.round(row.optDouble("subtotal")),
                Math.round(row.optDouble("discount")), Math.round(row.optDouble("totalAmount")),
                SafeText.json(row, "issuedAtUtc"), SafeText.json(row, "paymentCode"), SafeText.json(row, "paymentChannel"),
                SafeText.json(row, "paymentMethod"), SafeText.json(row, "paymentLocation"), SafeText.json(row, "paymentStatus"), lines);
        invoice.setDepositApplied(Math.round(row.optDouble("depositApplied", 0)));
        invoice.setPromotionDiscount(Math.round(row.optDouble("promotionDiscount", 0)));
        invoice.setTableLabel(tableLabel(String.valueOf(branchId), invoice.getTableId()));
        return invoice;
    }

    private static String jsonString(JSONObject row, String... keys) {
        for (String key : keys) if (row.has(key) && !row.isNull(key)) return SafeText.json(row, key);
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

    /** True when the restaurant's receiving bank account is configured, so the app may offer "Chuyển khoản". */
    public void loadTransferAvailable(Callback<Boolean> callback) {
        api().get("/api/mobile/v1/payment-options", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                callback.onSuccess(body instanceof JSONObject && ((JSONObject) body).optBoolean("transferAvailable", false));
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    /** VietQR of a pending bank-transfer payment: bank, account, amount, transfer content and a PNG (base64). */
    /** Promotions running now at a branch (public, like the menu); the server applies them to the bill. */
    public void loadPromotions(String branchId, Callback<List<com.restaurant.viettable.model.Promotion>> callback) {
        String query = branchId == null || branchId.isEmpty() ? "" : "?branchId=" + branchId;
        api().get("/api/mobile/v1/promotions" + query, false, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray rows = body instanceof JSONObject ? ((JSONObject) body).optJSONArray("items") : null;
                List<com.restaurant.viettable.model.Promotion> result = new ArrayList<>();
                if (rows != null) for (int i = 0; i < rows.length(); i++) {
                    JSONObject row = rows.optJSONObject(i);
                    if (row != null) result.add(parsePromotion(row));
                }
                callback.onSuccess(result);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    static com.restaurant.viettable.model.Promotion parsePromotion(JSONObject row) {
        List<Integer> branches = new ArrayList<>();
        JSONArray ids = row.optJSONArray("branchIds");
        if (ids != null) for (int i = 0; i < ids.length(); i++) branches.add(ids.optInt(i));
        return new com.restaurant.viettable.model.Promotion(row.optInt("promotionId"), SafeText.json(row, "name"),
                row.isNull("description") ? null : SafeText.json(row, "description", null),
                row.isNull("imageUrl") ? null : SafeText.json(row, "imageUrl", null),
                SafeText.json(row, "discountType", "PERCENT"), row.optDouble("discountValue", 0),
                row.isNull("maxDiscount") ? 0 : Math.round(row.optDouble("maxDiscount", 0)), Math.round(row.optDouble("minSubtotal", 0)),
                branches, SafeText.json(row, "startsAtUtc"), SafeText.json(row, "endsAtUtc"), SafeText.json(row, "summary"));
    }

    private volatile com.restaurant.viettable.model.DepositPolicy depositPolicy;

    /** Deposit rules from the server (cached once read); OFF until then, so nothing is ever assumed. */
    public void loadDepositPolicy(Callback<com.restaurant.viettable.model.DepositPolicy> callback) {
        com.restaurant.viettable.model.DepositPolicy cached = depositPolicy;
        if (cached != null) { callback.onSuccess(cached); return; }
        api().get("/api/mobile/v1/reservations/deposit-policy", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONObject row = body instanceof JSONObject ? (JSONObject) body : new JSONObject();
                com.restaurant.viettable.model.DepositPolicy policy = new com.restaurant.viettable.model.DepositPolicy(
                        row.optBoolean("enabled"), row.optInt("minGuests"), row.optLong("perGuestVnd"),
                        row.optInt("paymentMinutes"), row.optInt("refundCutoffHours"));
                depositPolicy = policy;
                callback.onSuccess(policy);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public com.restaurant.viettable.model.DepositPolicy cachedDepositPolicy() {
        com.restaurant.viettable.model.DepositPolicy cached = depositPolicy;
        return cached == null ? com.restaurant.viettable.model.DepositPolicy.OFF : cached;
    }

    /** The booking's deposit, with its VietQR while it still waits (the server also repairs any missed step). */
    public void loadDeposit(String reservationId, Callback<com.restaurant.viettable.model.DepositInfo> callback) {
        api().get("/api/mobile/v1/reservations/" + reservationId + "/deposit", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONObject row = body instanceof JSONObject ? (JSONObject) body : new JSONObject();
                callback.onSuccess(new com.restaurant.viettable.model.DepositInfo(row.optBoolean("required"),
                        row.optLong("amountVnd"), SafeText.json(row, "status", "NOT_REQUIRED"), SafeText.json(row, "policyMessage", ""),
                        row.isNull("dueAtUtc") ? null : SafeText.json(row, "dueAtUtc", null),
                        row.isNull("closedReason") ? null : SafeText.json(row, "closedReason", null),
                        row.isNull("reservationStatus") ? null : SafeText.json(row, "reservationStatus", null),
                        row.optJSONObject("transfer")));
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void loadTransferQr(int paymentId, Callback<JSONObject> callback) {
        api().get("/api/mobile/v1/payments/" + paymentId + "/transfer-qr", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                if (body instanceof JSONObject) callback.onSuccess((JSONObject) body);
                else callback.onError(new ApiException(0, "transfer_qr_invalid", "Không đọc được mã QR chuyển khoản.", null));
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    /**
     * The points conversion the server uses (GET /payment-options "pointValueVnd", "pointsStep" and
     * "maxPointsDiscountPercent"); null when one is missing, so the app offers no conversion instead of guessing.
     */
    public void loadPointsConversion(Callback<com.restaurant.viettable.util.LoyaltyDisplayRules.Conversion> callback) {
        api().get("/api/mobile/v1/payment-options", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONObject row = body instanceof JSONObject ? (JSONObject) body : null;
                callback.onSuccess(row == null ? null : com.restaurant.viettable.util.LoyaltyDisplayRules.Conversion.of(
                        row.optLong("pointValueVnd", 0), row.optLong("pointsStep", 0), row.optInt("maxPointsDiscountPercent", 0)));
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    /** Online gateways the server can use right now (e.g. "VNPAY"); empty when none is configured. */
    public void loadOnlinePaymentMethods(Callback<List<String>> callback) {
        api().get("/api/mobile/v1/payment-options", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray rows = body instanceof JSONObject ? ((JSONObject) body).optJSONArray("onlineMethods") : null;
                List<String> result = new ArrayList<>();
                if (rows != null) for (int i = 0; i < rows.length(); i++) {
                    String method = SafeText.json(rows, i).trim();
                    if (!method.isEmpty()) result.add(method);
                }
                callback.onSuccess(result);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void loadFaqs(Callback<List<FaqItem>> callback) {
        api().get("/api/mobile/v1/support/faqs", false, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray rows = ((JSONObject) body).optJSONArray("items"); List<FaqItem> result = new ArrayList<>();
                if (rows != null) for (int i = 0; i < rows.length(); i++) {
                    JSONObject row = rows.optJSONObject(i);
                    if (row != null) result.add(new FaqItem(SafeText.json(row, "faqId"), SafeText.json(row, "question"), SafeText.json(row, "answer")));
                }
                callback.onSuccess(result);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    /** Quick tags the server allows on a review ("Ngon", "Nhanh"…), as last sent with the guest's reviews. */
    private final List<String> reviewTags = new ArrayList<>();
    public List<String> reviewTags() { return new ArrayList<>(reviewTags); }

    public void loadReviews(Callback<List<Review>> callback) {
        api().get("/api/mobile/v1/me/reviews", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONArray tags = ((JSONObject) body).optJSONArray("tags");
                if (tags != null) { reviewTags.clear(); for (int i = 0; i < tags.length(); i++) { String tag = SafeText.json(tags, i); if (!tag.isEmpty()) reviewTags.add(tag); } }
                JSONArray rows = ((JSONObject) body).optJSONArray("items"); List<Review> result = new ArrayList<>();
                if (rows != null) for (int i = 0; i < rows.length(); i++) {
                    JSONObject row = rows.optJSONObject(i); if (row != null) result.add(parseReview(row));
                }
                callback.onSuccess(result);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    public void createReview(int invoiceId, com.restaurant.viettable.model.ReviewDraft draft, Callback<Review> callback) {
        if (invoiceId <= 0) { callback.onError(new ApiException(0, "invoice_required", "Chưa có hóa đơn hoàn tất để đánh giá.", null)); return; }
        try {
            JSONObject body = reviewBody(draft).put("invoiceId", invoiceId).put("imageUrls", new JSONArray());
            api().post("/api/mobile/v1/reviews", body, true, stableKey("reviewcreate", String.valueOf(invoiceId)), new ApiClient.Callback() {
                @Override public void onSuccess(Object body) { callback.onSuccess(parseReview((JSONObject) body)); }
                @Override public void onError(ApiException error) { callback.onError(error); }
            });
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    public void updateReview(Review review, com.restaurant.viettable.model.ReviewDraft draft, Callback<Review> callback) {
        if (review == null || review.getReviewId() <= 0) { callback.onError(new ApiException(0, "review_required", "Không tìm thấy đánh giá cần sửa.", null)); return; }
        try {
            JSONObject body = reviewBody(draft).put("invoiceId", review.getInvoiceId())
                    .put("imageUrls", new JSONArray(review.getImageUrls()));
            api().put("/api/mobile/v1/reviews/" + review.getReviewId(), body, true, null, new ApiClient.Callback() {
                @Override public void onSuccess(Object body) { callback.onSuccess(parseReview((JSONObject) body)); }
                @Override public void onError(ApiException error) { callback.onError(error); }
            });
        } catch (JSONException error) { callback.onError(jsonError()); }
    }

    /** The guest's own choices only: each criterion as picked, dish stars, tags, anonymous (owner 05/11). */
    static JSONObject reviewBody(com.restaurant.viettable.model.ReviewDraft draft) throws JSONException {
        JSONArray dishes = new JSONArray();
        for (java.util.Map.Entry<Integer, Integer> entry : draft.dishRatings.entrySet())
            if (entry.getValue() != null && entry.getValue() >= 1 && entry.getValue() <= 5)
                dishes.put(new JSONObject().put("dishId", entry.getKey()).put("rating", entry.getValue()));
        return new JSONObject().put("overallRating", draft.overall).put("foodRating", draft.food)
                .put("serviceRating", draft.service).put("spaceRating", draft.space)
                .put("comment", ReviewInputPolicy.commentPayload(draft.comment))
                .put("tags", new JSONArray(draft.tags)).put("isAnonymous", draft.anonymous).put("dishRatings", dishes);
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

    /** Every voucher the guest may see (profile "Voucher của tôi"), greyed ones with the server's reason. */
    public void loadVouchers(Callback<List<Voucher>> callback) { loadVouchers(0, callback); }

    /** Vouchers for the open bill: usable first, each with what it takes off THIS bill, or why it cannot be used. */
    public void loadVouchersForBill(Callback<List<Voucher>> callback) { loadVouchers(currentCheckoutQuoteId(), callback); }

    private void loadVouchers(int quoteId, Callback<List<Voucher>> callback) {
        api().get("/api/mobile/v1/me/vouchers" + (quoteId > 0 ? "?quoteId=" + quoteId : ""), true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                List<Voucher> values = parseVouchers((JSONArray) body);
                state.replaceVouchers(values); callback.onSuccess(values);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
    }

    static List<Voucher> parseVouchers(JSONArray rows) {
        List<Voucher> values = new ArrayList<>();
        if (rows == null) return values;
        for (int i = 0; i < rows.length(); i++) {
            JSONObject row = rows.optJSONObject(i); if (row == null) continue;
            String type = SafeText.json(row, "discountType");
            Voucher voucher = new Voucher(SafeText.json(row, "code"), SafeText.json(row, "name"),
                    "FIXED".equals(type) ? Math.round(row.optDouble("discountValue")) : 0,
                    "PERCENT".equals(type) ? row.optInt("discountValue") : 0,
                    Math.round(row.optDouble("minSubtotal")), SafeText.json(row, "endsAtUtc"), true, null,
                    SafeText.json(row, "name"), false);
            voucher.setBackendId(row.optInt("voucherId"));
            voucher.setAvailability(row.optBoolean("usable", true),
                    row.isNull("reasonCode") ? null : SafeText.json(row, "reasonCode", null),
                    row.isNull("reason") ? null : SafeText.json(row, "reason", null),
                    row.isNull("remainingLabel") ? null : SafeText.json(row, "remainingLabel", null),
                    row.isNull("discount") || !row.has("discount") ? -1 : Math.round(row.optDouble("discount")),
                    row.optBoolean("applied", false), row.optInt("usedByMe", 0));
            values.add(voucher);
        }
        return values;
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

    /** "Bỏ voucher": the bill goes back to the promotion (if any) or no discount. */
    public void removeVoucher(Callback<Invoice> callback) {
        if (currentQuote == null) { callback.onError(new ApiException(0, "quote_required", "Chưa có báo giá thanh toán.", null)); return; }
        try {
            JSONObject body = new JSONObject().put("voucherId", JSONObject.NULL);
            api().post("/api/mobile/v1/checkout/quotes/" + currentQuote.optInt("checkoutQuoteId") + "/voucher", body, true, null,
                    quoteCallback(callback));
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    /** "Không dùng khuyến mãi" (use = false) and "Dùng lại khuyến mãi" (use = true). */
    public void choosePromotion(boolean use, Callback<Invoice> callback) {
        if (currentQuote == null) { callback.onError(new ApiException(0, "quote_required", "Chưa có báo giá thanh toán.", null)); return; }
        try {
            JSONObject body = new JSONObject().put("use", use);
            api().post("/api/mobile/v1/checkout/quotes/" + currentQuote.optInt("checkoutQuoteId") + "/promotion", body, true, null,
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

    /** One request for the whole inbox (it used to send one per notification). */
    public void markAllNotificationsRead(Callback<Void> callback) {
        api().post("/api/mobile/v1/notifications/read-all", new JSONObject(), true, null, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                for (NotificationItem item : state.getNotifications()) item.setRead(true);
                state.notifyStateChanged();
                callback.onSuccess(null);
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        });
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

    /** Sets the in-memory user and remembers on this phone which account owns the session. */
    private void setSessionUser(User user) {
        if (user != null) GreetingNameStore.getInstance().remember(user.getId(), user.getFullName());
        state.setCurrentUser(user);
        if (user != null && BiometricLoginPolicy.isKnownAccount(user.getId())) state.useBranchScope(user.getId());
        if (user != null) {
            BiometricTokenStore.getInstance().rememberSession(user.getId(),
                    BiometricLoginPolicy.maskedLabel(user.getEmail(), user.getPhone(), user.getFullName()),
                    BiometricLoginPolicy.loginId(user.getEmail(), user.getPhone()));
        }
    }

    /**
     * "Đăng xuất, giữ đăng nhập vân tay": seals the refresh token for this account and forgets the session
     * locally; the server session stays alive until fingerprint login rotates it or it expires.
     * Throws (session untouched) when sealing fails, e.g. KeyPermanentlyInvalidatedException.
     */
    public void logoutKeepingFingerprint(String accountId, String label, String loginId) throws Exception {
        BiometricTokenStore store = BiometricTokenStore.getInstance();
        api().handOffRefreshToken(refreshToken -> store.seal(accountId, label, loginId, refreshToken));
        clearLocalSession();
    }

    /**
     * Checks a password (or Google id token) for the app lock without touching the current session: the new
     * session the server creates is revoked right away. Returns the customer id the credentials belong to.
     */
    public void verifyPassword(String loginId, String password, Callback<String> callback) {
        try {
            JSONObject body = new JSONObject().put("username", loginId).put("password", password);
            api().post("/api/mobile/v1/auth/login", body, false, null, verifyCallback(callback));
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    public void verifyGoogle(String idToken, Callback<String> callback) {
        try {
            JSONObject body = new JSONObject().put("idToken", idToken);
            api().post("/api/mobile/v1/auth/google", body, false, null, verifyCallback(callback));
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    private ApiClient.Callback verifyCallback(Callback<String> callback) {
        return new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                JSONObject response = (JSONObject) body;
                String extra = SafeText.json(response, "refreshToken");
                if (!extra.isEmpty()) {
                    try {
                        api().post("/api/mobile/v1/auth/logout", new JSONObject().put("refreshToken", extra), false, null,
                                new ApiClient.Callback() {
                                    @Override public void onSuccess(Object ignored) { }
                                    @Override public void onError(ApiException ignored) { }
                                });
                    } catch (JSONException ignored) { }
                }
                callback.onSuccess(parseTokenUser(response.optJSONObject("user")).getId());
            }
            @Override public void onError(ApiException error) { callback.onError(error); }
        };
    }

    /** Fingerprint login: exchanges the unsealed refresh token for a new session. */
    public void loginWithSealedToken(String refreshToken, Callback<User> callback) {
        try {
            JSONObject body = new JSONObject().put("refreshToken", refreshToken);
            api().post("/api/mobile/v1/auth/refresh", body, false, null, tokenCallback(new Callback<User>() {
                @Override public void onSuccess(User value) {
                    // The server rotated the token; using it again would revoke the whole session family.
                    BiometricTokenStore.getInstance().clearSealed();
                    callback.onSuccess(value);
                }
                @Override public void onError(ApiException error) { callback.onError(error); }
            }));
        } catch (JSONException e) { callback.onError(jsonError()); }
    }

    /** Cold start: show the inbox kept on this phone for the signed-in account until the fresh one arrives (owner 07/11). */
    public void restoreNotificationsFromDevice() {
        if (!api().hasSession() || !state.getNotifications().isEmpty()) return;
        JSONArray rows = NotificationInboxStore.getInstance().rowsFor(BiometricTokenStore.getInstance().sessionAccount());
        if (rows != null) state.replaceNotifications(parseNotificationRows(rows));
    }

    static List<NotificationItem> parseNotificationRows(JSONArray rows) {
        List<NotificationItem> values = new ArrayList<>();
        if (rows != null) for (int i = 0; i < rows.length(); i++) {
            JSONObject row = rows.optJSONObject(i);
            if (row == null) continue;
            String type = SafeText.json(row, "type");
            String title = SafeText.json(row, "title", "");
            NotificationItem item = new NotificationItem(String.valueOf(row.optLong("notificationId")),
                    NotificationRules.typeOf(SafeText.json(row, "category", null), type),
                    SafeText.isBlank(title) ? StatusLabels.notificationTitle(type) : title,
                    StatusLabels.notificationMessage(SafeText.json(row, "message"), row.optLong("orderId")),
                    SafeText.json(row, "createdAtUtc"), row.optBoolean("isRead"));
            item.setRefId(row.optLong("refId"));
            item.setEventName(type);
            values.add(item);
        }
        return values;
    }

    public void clearLocalSession() {
        com.restaurant.viettable.reminder.ReservationReminders.clearAll();
        BiometricTokenStore.getInstance().forgetSession();
        GreetingNameStore.getInstance().clear();
        NotificationInboxStore.getInstance().clear();
        state.useBranchScope(null);
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
            ingredients.add(SafeText.json(rows.optJSONObject(i), "name"));
        String image = api().absoluteUrl(SafeText.json(dish, "image"));
        FoodItem food = new FoodItem(String.valueOf(dish.optInt("dishId")), categoryId, branchId,
                SafeText.json(dish, "name"), Math.round(dish.optDouble("price")), 0, 0,
                SafeText.json(dish, "description"), String.join(", ", ingredients), "", "", 0,
                dish.optBoolean("available", true), false, Collections.singletonList("Tiêu chuẩn"),
                new ArrayList<>(), image, 0);
        food.setDailySpecial(dish.optBoolean("isDailySpecial", false));
        return food;
    }

    private User parseTokenUser(JSONObject row) {
        if (row == null) return new User();
        return new User(String.valueOf(row.optInt("customerId")), SafeText.json(row, "name"),
                SafeText.json(row, "email"), SafeText.json(row, "phoneNumber"), "Thành viên",
                row.optInt("loyaltyPoints"), null);
    }

    private User parseProfile(JSONObject row) {
        User user = new User(String.valueOf(row.optInt("customerId")), SafeText.json(row, "name"),
                SafeText.json(row, "email"), SafeText.json(row, "phoneNumber"), SafeText.json(row, "membershipTier", "Thành viên"),
                row.optInt("loyaltyPoints"), null);
        user.setDateOfBirth(SafeText.json(row, "dateOfBirth", null));
        user.setGender(SafeText.json(row, "gender", null));
        user.setAddress(SafeText.json(row, "address", null));
        return user;
    }

    private Reservation parseReservation(JSONObject row) {
        if (row == null) return new Reservation();
        String branchId = String.valueOf(row.optInt("branchId")); Branch branch = state.findBranch(branchId);
        String reserved = SafeText.json(row, "reservedAt");
        String date = DisplayFormatters.date(reserved);
        String time = DisplayFormatters.time(reserved);
        String note = row.isNull("note") ? "" : SafeText.json(row, "note", "");
        int tableId = row.isNull("tableId") ? 0 : row.optInt("tableId");
        JSONArray assignedTables = row.optJSONArray("assignedTables");
        if (tableId <= 0 && assignedTables != null && assignedTables.length() > 0) {
            tableId = assignedTables.optJSONObject(0) == null ? 0 : assignedTables.optJSONObject(0).optInt("tableId");
        }
        Reservation reservation = new Reservation(String.valueOf(row.optInt("reservationId")), branchId,
                branch == null ? "Chi nhánh " + branchId : branch.getName(), branch == null ? "" : branch.getAddress(),
                date, time, row.optInt("partySize"), "", tableId > 0 ? String.valueOf(tableId) : "", note,
                new ArrayList<>(), 0, reservationStatus(SafeText.json(row, "status")), row.optJSONArray("preOrderItems") != null && row.optJSONArray("preOrderItems").length() > 0,
                SafeText.json(row, "customerName"), SafeText.json(row, "phoneNumber"));
        reservation.setTableLabel(tableLabel(branchId, reservation.getTableId()));
        // Pushed-together tables (owner 06/11): "Bàn 5 + 6".
        if (assignedTables != null && assignedTables.length() > 1) {
            List<String> labels = new ArrayList<>();
            for (int i = 0; i < assignedTables.length(); i++) {
                JSONObject assigned = assignedTables.optJSONObject(i);
                if (assigned != null) labels.add(tableLabel(branchId, String.valueOf(assigned.optInt("tableId"))));
            }
            String group = com.restaurant.viettable.data.TableNumberDirectory.groupLabel(labels);
            if (group != null) reservation.setTableLabel(group);
        }
        reservation.setReservedAt(reserved);
        reservation.setStatusReason(row.isNull("statusReason") ? null : SafeText.json(row, "statusReason", null));
        reservation.setDepositAmount(row.isNull("depositAmount") ? 0 : Math.round(row.optDouble("depositAmount", 0)));
        reservation.setDepositDueAtUtc(row.isNull("depositDueAtUtc") ? null : SafeText.json(row, "depositDueAtUtc", null));
        reservation.setArrivalConfirmation(SafeText.json(row, "arrivalConfirmation", "None"));
        return reservation;
    }

    private DiningSession parseSession(JSONObject row) {
        String branchId = String.valueOf(row.optInt("branchId")); Branch branch = state.findBranch(branchId);
        User user = state.getCurrentUser();
        DiningSession session = new DiningSession(String.valueOf(row.optInt("diningSessionId")), branchId,
                branch == null ? "Chi nhánh " + branchId : branch.getName(), String.valueOf(row.optInt("tableId")),
                sessionStatus(SafeText.json(row, "status")), row.isNull("reservationId") ? null : String.valueOf(row.optInt("reservationId")),
                user == null ? "Khách hàng" : user.getFullName(), SafeText.json(row, "startedAtUtc"), new ArrayList<>(), new ArrayList<>());
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
        // Items carry no timestamp of their own: they were ordered together with their round.
        String orderTime = SafeText.json(row, "orderTime");
        if (itemRows != null) for (int i = 0; i < itemRows.length(); i++) {
            JSONObject item = itemRows.optJSONObject(i);
            items.add(new OrderItem(String.valueOf(item.optInt("itemId")), SafeText.json(item, "dishName"),
                    Math.round(item.optDouble("unitPrice")), item.optInt("quantity"), SafeText.json(item, "note"),
                    orderItemStatus(SafeText.json(item, "status")), orderTime));
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
            summary = new DiningSessionSummary(sessionId, SafeText.json(sessionRow, "sessionCode"), branchId,
                    tableId > 0 ? String.valueOf(tableId) : "", SafeText.json(sessionRow, "status"),
                    SafeText.json(sessionRow, "startedAtUtc"), sessionRow.isNull("endedAtUtc") ? null : SafeText.json(sessionRow, "endedAtUtc"));
            summary.setTableLabel(tableDisplay);
        }
        Order order = new Order(String.valueOf(row.optInt("orderId")), sessionId,
                branchName, tableIdValue, Order.Type.DINE_IN,
                orderStatus(SafeText.json(row, "status")), SafeText.json(row, "orderTime"), items, Math.round(row.optDouble("subtotal")), "");
        order.setDiningSessionSummary(summary);
        order.setTableLabel(tableDisplay);
        return order;
    }

    private ServiceRequest parseServiceRequest(JSONObject row) {
        ServiceRequest.Status status = serviceRequestStatus(SafeText.json(row, "status"));
        return new ServiceRequest(String.valueOf(row.optInt("serviceRequestId")), SafeText.json(row, "requestType"), SafeText.json(row, "note"), status, SafeText.json(row, "createdAtUtc"));
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
                0, 0, voucher, points, Math.round(quote.optDouble("totalAmount")), "Chưa chọn", "", SafeText.json(quote, "expiresAtUtc"));
        invoice.setTableLabel(session == null ? "Chưa có thông tin bàn" : session.getTableLabel());
        invoice.setDepositApplied(Math.round(quote.optDouble("depositApplied", 0)));
        invoice.setCharges(quote.optDouble("serviceFeePercent", 0), Math.round(quote.optDouble("serviceFeeAmount", 0)),
                quote.optDouble("vatPercent", 0), Math.round(quote.optDouble("vatAmount", 0)));
        invoice.setPromotion(quote.isNull("promotionName") ? null : SafeText.json(quote, "promotionName", null),
                Math.round(quote.optDouble("promotionDiscount", 0)),
                quote.isNull("discountNote") ? null : SafeText.json(quote, "discountNote", null));
        invoice.setPromotionOffer(quote.isNull("promotionId") ? 0 : Math.round(quote.optDouble("promotionOfferDiscount", 0)),
                quote.optBoolean("promotionDeclined", false));
        invoice.setPickedVoucher(quote.isNull("voucherId") ? 0 : quote.optInt("voucherId"),
                quote.isNull("voucherCode") ? null : SafeText.json(quote, "voucherCode", null));
        return invoice;
    }

    private Invoice invoiceFromIssued(JSONObject value) {
        DiningSession session = state.getActiveDiningSession();
        Branch branch = value.isNull("branchId") ? null : state.findBranch(String.valueOf(value.optInt("branchId")));
        Invoice invoice = new Invoice(SafeText.json(value, "invoiceCode", String.valueOf(value.optInt("invoiceId"))),
                String.valueOf(value.optInt("diningSessionId")),
                issuedBranchName(branch == null ? null : branch.getName(), session == null ? null : session.getBranchName()),
                session == null ? String.valueOf(value.optInt("tableId")) : session.getTableId(), Math.round(value.optDouble("subtotal")), 0, 0,
                Math.round(value.optDouble("discount")), 0, Math.round(value.optDouble("totalAmount")),
                StatusLabels.paymentMethod(value.isNull("paymentMethod") ? "" : SafeText.json(value, "paymentMethod", "")),
                "", SafeText.json(value, "issuedAtUtc"));
        invoice.setBackendInvoiceId(value.optInt("invoiceId"));
        invoice.setBackendPaymentId(value.optInt("paymentId"));
        invoice.setDepositApplied(Math.round(value.optDouble("depositApplied", 0)));
        invoice.setTableLabel(session == null ? tableLabel(String.valueOf(value.optInt("branchId")), String.valueOf(value.optInt("tableId"))) : session.getTableLabel());
        return invoice;
    }

    // The session is usually closed by the time the invoice is issued, so the branch list comes first.
    static String issuedBranchName(String fromBranchList, String fromSession) {
        if (!SafeText.isBlank(fromBranchList)) return fromBranchList;
        return SafeText.isBlank(fromSession) ? "" : fromSession;
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
            parts.add(new SplitBill.Part(part.optInt("splitBillPartId"), SafeText.json(part, "label"),
                    Math.round(part.optDouble("amount")), SafeText.json(part, "status"), paymentId,
                    SafeText.json(part, "paymentStatus", null)));
        }
        return new SplitBill(row.optInt("splitBillId"), row.optInt("checkoutQuoteId"),
                row.optInt("diningSessionId"), Math.round(row.optDouble("totalAmount")),
                SafeText.json(row, "status"), parts);
    }

    private PaymentInfo parsePayment(JSONObject row) {
        return new PaymentInfo(row.optInt("paymentId"), row.optInt("checkoutQuoteId"),
                row.optInt("diningSessionId"), Math.round(row.optDouble("amount")),
                SafeText.json(row, "channel"), SafeText.json(row, "method"), SafeText.json(row, "location", null),
                SafeText.json(row, "status"), SafeText.json(row, "currency", "VND"), SafeText.json(row, "providerName", null),
                SafeText.json(row, "providerReference", null), SafeText.json(row, "providerTransactionId", null),
                SafeText.json(row, "paymentUrl", null), SafeText.json(row, "providerExpiresAtUtc", null));
    }

    static Review parseReview(JSONObject row) {
        JSONArray images = row.optJSONArray("imageUrls"); List<String> imageUrls = new ArrayList<>();
        if (images != null) for (int i = 0; i < images.length(); i++) imageUrls.add(SafeText.json(images, i));
        Review review = new Review(row.optInt("reviewId"), row.optInt("invoiceId"), row.optInt("diningSessionId"),
                row.optInt("overallRating"), row.optInt("foodRating"), row.optInt("serviceRating"),
                row.optInt("spaceRating"), SafeText.json(row, "comment", null), imageUrls,
                SafeText.json(row, "createdAtUtc", null), SafeText.json(row, "updatedAtUtc", null));
        List<String> tags = new ArrayList<>();
        JSONArray tagRows = row.optJSONArray("tags");
        if (tagRows != null) for (int i = 0; i < tagRows.length(); i++) { String tag = SafeText.json(tagRows, i); if (!tag.isEmpty()) tags.add(tag); }
        List<Review.DishRating> dishes = new ArrayList<>();
        JSONArray dishRows = row.optJSONArray("dishRatings");
        if (dishRows != null) for (int i = 0; i < dishRows.length(); i++) {
            JSONObject dish = dishRows.optJSONObject(i);
            if (dish != null) dishes.add(new Review.DishRating(dish.optInt("dishId"), SafeText.json(dish, "dishName"), dish.optInt("rating")));
        }
        review.setDetails(tags, dishes, row.optBoolean("isAnonymous"), row.optBoolean("canEdit"),
                SafeText.json(row, "editableUntilUtc", null), SafeText.json(row, "replyText", null),
                SafeText.json(row, "repliedAtUtc", null), row.optBoolean("isHidden"), SafeText.json(row, "hiddenReason", null));
        return review;
    }

    public static Reservation.Status reservationStatus(String value) {
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
        LocalDate date = ReservationSchedule.parseDate(dateText, LocalDate.now());
        if (date == null) date = LocalDate.now().plusDays(1);
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

    // ---- Chat với nhân viên hỗ trợ (owner 06/11): all data from /api/mobile/v1/support/chat ----
    private static final String SUPPORT = "/api/mobile/v1/support/chat";

    private <T> ApiClient.Callback supportCallback(Callback<T> callback, java.util.function.Function<JSONObject, T> parse) {
        return new ApiClient.Callback() {
            @Override public void onSuccess(Object body) {
                try { finishSuccess(callback, parse.apply(body instanceof JSONObject ? (JSONObject) body : new JSONObject())); }
                catch (RuntimeException e) { finishError(callback, jsonError()); }
            }
            @Override public void onError(ApiException error) { finishError(callback, error); }
        };
    }

    public void loadSupportTopics(Callback<List<com.restaurant.viettable.model.SupportChat.Topic>> callback) {
        api().get(SUPPORT + "/topics", true, supportCallback(callback,
                o -> com.restaurant.viettable.model.SupportChat.items(o, com.restaurant.viettable.model.SupportChat.Topic::parse)));
    }

    public void loadSupportOptions(String topic, Callback<List<com.restaurant.viettable.model.SupportChat.Option>> callback) {
        api().get(SUPPORT + "/options?topic=" + encode(topic), true, supportCallback(callback,
                o -> com.restaurant.viettable.model.SupportChat.items(o, com.restaurant.viettable.model.SupportChat.Option::parse)));
    }

    public void checkSupportPayment(String topic, int contextId, Callback<com.restaurant.viettable.model.SupportChat.Check> callback) {
        api().get(SUPPORT + "/check?topic=" + encode(topic) + "&contextId=" + contextId, true,
                supportCallback(callback, com.restaurant.viettable.model.SupportChat.Check::parse));
    }

    public void loadMySupport(Callback<List<com.restaurant.viettable.model.SupportChat.Conversation>> callback) {
        api().get(SUPPORT + "/conversations", true, supportCallback(callback,
                o -> com.restaurant.viettable.model.SupportChat.items(o, com.restaurant.viettable.model.SupportChat.Conversation::parse)));
    }

    public void startSupport(int branchId, String topic, Integer contextId, String message, Callback<com.restaurant.viettable.model.SupportChat.Detail> callback) {
        try {
            JSONObject body = new JSONObject().put("branchId", branchId).put("topic", topic).put("message", message);
            if (contextId != null && contextId > 0) body.put("contextId", contextId);
            api().post(SUPPORT + "/conversations", body, true, java.util.UUID.randomUUID().toString(),
                    supportCallback(callback, com.restaurant.viettable.model.SupportChat.Detail::parse));
        } catch (JSONException e) { finishError(callback, jsonError()); }
    }

    /** One page of a conversation; beforeId &gt; 0 loads the messages older than it. */
    public void loadSupportConversation(int id, long beforeId, Callback<com.restaurant.viettable.model.SupportChat.Detail> callback) {
        api().get(SUPPORT + "/conversations/" + id + (beforeId > 0 ? "?beforeId=" + beforeId : ""), true,
                supportCallback(callback, com.restaurant.viettable.model.SupportChat.Detail::parse));
    }

    public void sendSupportMessage(int id, String text, Callback<Void> callback) {
        try {
            api().post(SUPPORT + "/conversations/" + id + "/messages", new JSONObject().put("body", text), true,
                    java.util.UUID.randomUUID().toString(), supportCallback(callback, o -> null));
        } catch (JSONException e) { finishError(callback, jsonError()); }
    }

    public void markSupportRead(int id, long lastMessageId) {
        if (id <= 0 || lastMessageId <= 0) return;
        try {
            api().post(SUPPORT + "/conversations/" + id + "/read", new JSONObject().put("lastMessageId", lastMessageId), true, null,
                    supportCallback(null, o -> null));
        } catch (JSONException ignored) { }
    }

    private static <T> void finishSuccess(Callback<T> callback, T value) { if (callback != null) callback.onSuccess(value); }
    private static void finishError(Callback<?> callback, ApiException error) { if (callback != null) callback.onError(error); }
}
