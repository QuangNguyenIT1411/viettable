package com.restaurant.viettable.util;

import com.restaurant.viettable.model.OrderItem;

import java.util.List;

/** "x/y món đã xong" on Tiến độ món: x = dishes ready or served, y = every dish line that is not cancelled. */
public final class DishProgress {
    public final int done;
    public final int total;

    private DishProgress(int done, int total) {
        this.done = done;
        this.total = total;
    }

    public static DishProgress of(List<OrderItem> items) {
        int done = 0, total = 0;
        if (items != null) for (OrderItem item : items) {
            if (item.getStatus() == OrderItem.Status.CANCELLED) continue;
            total++;
            if (item.getStatus() == OrderItem.Status.READY || item.getStatus() == OrderItem.Status.SERVED) done++;
        }
        return new DishProgress(done, total);
    }

    /** The kitchen's READY maps to IN_PROGRESS; once every dish of that round is out, Tiến độ món shows it as done. */
    public static com.restaurant.viettable.model.Order.Status trackingStatus(
            com.restaurant.viettable.model.Order.Status status, List<OrderItem> items) {
        DishProgress progress = of(items);
        boolean allOut = progress.total > 0 && progress.done == progress.total;
        return status == com.restaurant.viettable.model.Order.Status.IN_PROGRESS && allOut
                ? com.restaurant.viettable.model.Order.Status.COMPLETED : status;
    }

    public int percent() {
        return total == 0 ? 0 : Math.round(done * 100f / total);
    }

    public String label() {
        return done + "/" + total;
    }
}
