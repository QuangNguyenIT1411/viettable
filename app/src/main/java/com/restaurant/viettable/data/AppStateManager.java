package com.restaurant.viettable.data;

import com.restaurant.viettable.model.*;

import java.util.ArrayList;
import java.util.List;

public class AppStateManager {
    private static AppStateManager instance;

    public interface StateChangeListener {
        void onStateChanged();
    }

    private final List<StateChangeListener> listeners = new ArrayList<>();

    private User currentUser;
    private Branch selectedBranch;
    private final List<Branch> branches = new ArrayList<>();
    private final List<Category> categories = new ArrayList<>();
    private final List<FoodItem> foodItems = new ArrayList<>();
    private final List<CartItem> cartItems = new ArrayList<>();
    private final List<Reservation> reservations = new ArrayList<>();
    private final List<Voucher> vouchers = new ArrayList<>();
    private final List<NotificationItem> notifications = new ArrayList<>();
    private final List<Order> pastOrders = new ArrayList<>();

    private DiningSession activeDiningSession;
    private boolean loggedIn = false; // Default to false so cold start showcases Splash -> Welcome

    public static synchronized AppStateManager getInstance() {
        if (instance == null) {
            instance = new AppStateManager();
        }
        return instance;
    }

    private AppStateManager() {
        // Authenticated and business state is populated from the mobile Gateway.
        currentUser = null;
    }

    public void addListener(StateChangeListener listener) {
        if (!listeners.contains(listener)) {
            listeners.add(listener);
        }
    }

    public void removeListener(StateChangeListener listener) {
        listeners.remove(listener);
    }

    public void notifyStateChanged() {
        for (StateChangeListener l : new ArrayList<>(listeners)) {
            if (l != null) l.onStateChanged();
        }
    }

    public boolean isLoggedIn() { return loggedIn; }
    public void setLoggedIn(boolean loggedIn) { this.loggedIn = loggedIn; notifyStateChanged(); }

    // --- Getters & Setters ---
    public User getCurrentUser() { return currentUser; }
    public void setCurrentUser(User user) { this.currentUser = user; notifyStateChanged(); }

    public Branch getSelectedBranch() { return selectedBranch; }
    public void setSelectedBranch(Branch branch) {
        if (selectedBranch != null && !selectedBranch.getId().equals(branch.getId())) {
            // If branch changes, clear cart
            cartItems.clear();
        }
        this.selectedBranch = branch;
        notifyStateChanged();
    }

    public List<Branch> getBranches() { return branches; }
    public List<Category> getCategories() { return categories; }
    public List<FoodItem> getFoodItems() { return foodItems; }
    public List<CartItem> getCartItems() { return cartItems; }
    public List<Reservation> getReservations() { return reservations; }
    public List<Voucher> getVouchers() { return vouchers; }
    public List<NotificationItem> getNotifications() { return notifications; }
    public List<Order> getPastOrders() { return pastOrders; }

    public void replaceBranches(List<Branch> values) {
        String selectedId = selectedBranch == null ? null : selectedBranch.getId();
        branches.clear();
        branches.addAll(values);
        selectedBranch = null;
        for (Branch branch : branches) if (branch.getId().equals(selectedId)) selectedBranch = branch;
        if (selectedBranch == null && !branches.isEmpty()) selectedBranch = branches.get(0);
        notifyStateChanged();
    }

    public void replaceCatalog(List<Category> newCategories, List<FoodItem> newFoodItems) {
        categories.clear();
        categories.addAll(newCategories);
        foodItems.clear();
        foodItems.addAll(newFoodItems);
        notifyStateChanged();
    }

    public void applyFavoriteIds(java.util.Set<String> ids) {
        for (FoodItem item : foodItems) item.setFavorite(ids.contains(item.getId()));
        notifyStateChanged();
    }

    public void replaceNotifications(List<NotificationItem> values) {
        notifications.clear();
        notifications.addAll(values);
        notifyStateChanged();
    }

    public Branch findBranch(String id) {
        for (Branch branch : branches) if (branch.getId().equals(id)) return branch;
        return null;
    }

    public FoodItem findFoodItem(String id) {
        for (FoodItem item : foodItems) if (item.getId().equals(id)) return item;
        return null;
    }

    public void replaceCart(List<CartItem> values) {
        cartItems.clear();
        cartItems.addAll(values);
        notifyStateChanged();
    }

    public Reservation getUpcomingReservation() {
        for (Reservation reservation : reservations) if (reservation.getStatus() == Reservation.Status.CONFIRMED) return reservation;
        return null;
    }

    public void replaceReservations(List<Reservation> values) {
        reservations.clear(); reservations.addAll(values); notifyStateChanged();
    }

    public void replaceVouchers(List<Voucher> values) { vouchers.clear(); vouchers.addAll(values); notifyStateChanged(); }

    public void upsertReservation(Reservation value) {
        for (int i = 0; i < reservations.size(); i++) if (reservations.get(i).getId().equals(value.getId())) {
            reservations.set(i, value); notifyStateChanged(); return;
        }
        reservations.add(0, value); notifyStateChanged();
    }

    public void addServerOrder(Order order) {
        if (activeDiningSession != null) {
            for (int i = 0; i < activeDiningSession.getOrders().size(); i++) {
                if (activeDiningSession.getOrders().get(i).getId().equals(order.getId())) {
                    activeDiningSession.getOrders().set(i, order);
                    notifyStateChanged();
                    return;
                }
            }
            activeDiningSession.getOrders().add(0, order);
        }
        notifyStateChanged();
    }

    public void upsertServiceRequest(ServiceRequest request) {
        if (activeDiningSession != null) {
            List<ServiceRequest> requests = activeDiningSession.getServiceRequests();
            for (int i = 0; i < requests.size(); i++) {
                if (requests.get(i).getId().equals(request.getId())) {
                    requests.set(i, request);
                    notifyStateChanged();
                    return;
                }
            }
            requests.add(0, request);
        }
        notifyStateChanged();
    }

    public void replaceOrders(String scope, List<Order> values) {
        if ("active".equalsIgnoreCase(scope) && activeDiningSession != null) {
            activeDiningSession.getOrders().clear(); activeDiningSession.getOrders().addAll(values);
        } else if ("history".equalsIgnoreCase(scope)) {
            pastOrders.clear(); pastOrders.addAll(values);
        }
        notifyStateChanged();
    }

    public void clearAuthenticatedData() {
        loggedIn = false;
        currentUser = null;
        activeDiningSession = null;
        cartItems.clear();
        for (FoodItem item : foodItems) item.setFavorite(false);
        reservations.clear();
        vouchers.clear();
        notifications.clear();
        pastOrders.clear();
        notifyStateChanged();
    }

    public DiningSession getActiveDiningSession() { return activeDiningSession; }
    public void setActiveDiningSession(DiningSession session) {
        this.activeDiningSession = session;
        notifyStateChanged();
    }

    public boolean hasActiveDiningSession() {
        return activeDiningSession != null &&
                (activeDiningSession.getStatus() == DiningSession.Status.ACTIVE ||
                        activeDiningSession.getStatus() == DiningSession.Status.PAYMENT_REQUESTED);
    }

    // --- Cart Actions ---
    public void addToCart(FoodItem item, int quantity, String size, List<String> toppings, int spicyLevel, String note) {
        for (CartItem ci : cartItems) {
            if (ci.getFoodItem().getId().equals(item.getId()) &&
                    ci.getSelectedSize().equals(size) &&
                    ci.getSpicyLevel() == spicyLevel) {
                ci.setQuantity(ci.getQuantity() + quantity);
                notifyStateChanged();
                return;
            }
        }
        cartItems.add(new CartItem(item, quantity, size, toppings, spicyLevel, note));
        notifyStateChanged();
    }

    public void removeCartItem(CartItem item) {
        cartItems.remove(item);
        notifyStateChanged();
    }

    public void clearCart() {
        cartItems.clear();
        notifyStateChanged();
    }

    public long calculateCartSubtotal() {
        long total = 0;
        for (CartItem ci : cartItems) {
            total += ci.calculateTotalPrice();
        }
        return total;
    }

    public int getCartItemCount() {
        int count = 0;
        for (CartItem ci : cartItems) {
            count += ci.getQuantity();
        }
        return count;
    }

    public void closeActiveDiningSession() {
        if (activeDiningSession != null) {
            activeDiningSession.setStatus(DiningSession.Status.CLOSED);
            pastOrders.addAll(activeDiningSession.getOrders());
            activeDiningSession = null;
            notifyStateChanged();
        }
    }
}
