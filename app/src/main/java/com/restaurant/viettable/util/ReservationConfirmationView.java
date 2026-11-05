package com.restaurant.viettable.util;

import androidx.annotation.Nullable;

import com.restaurant.viettable.model.Reservation;

import java.util.List;

/**
 * What "Đặt bàn thành công" shows for a booking's real status: the guest can come back to it after changing or
 * cancelling, so it must never keep saying the request was sent for a booking that no longer stands.
 */
public final class ReservationConfirmationView {
    public final String title;
    public final String subtitle;
    /** False: the booking no longer stands (red badge, no detail button, "Đặt bàn mới" instead). */
    public final boolean standing;

    private ReservationConfirmationView(String title, String subtitle, boolean standing) {
        this.title = title;
        this.subtitle = subtitle;
        this.standing = standing;
    }

    public static ReservationConfirmationView of(@Nullable Reservation.Status status, @Nullable String reason) {
        if (status == null) status = Reservation.Status.PENDING;
        switch (status) {
            case PENDING_DEPOSIT:
                return new ReservationConfirmationView("Chờ đặt cọc", ReservationSchedule.statusInfo(status, reason), true);
            case CONFIRMED:
                return new ReservationConfirmationView("Đã xác nhận đặt bàn", "Nhà hàng đã xác nhận đặt bàn của bạn. Hẹn gặp bạn đúng giờ.", true);
            case CHECKING_IN:
            case CHECKED_IN:
                return new ReservationConfirmationView("Bạn đã đến nhà hàng", "Chúc bạn có một bữa ăn ngon miệng.", true);
            case COMPLETED:
                return new ReservationConfirmationView("Đã hoàn tất đặt bàn", "Cảm ơn bạn đã dùng bữa tại VietTable.", true);
            case CANCELLED:
                return new ReservationConfirmationView("Đã huỷ đặt bàn", "Đặt bàn này không còn hiệu lực. Bạn có thể đặt bàn mới khi cần.", false);
            case NO_SHOW:
                return new ReservationConfirmationView("Đặt bàn đã quá giờ", "Đặt bàn này không còn hiệu lực. Bạn có thể đặt bàn mới khi cần.", false);
            case REJECTED:
                return new ReservationConfirmationView("Nhà hàng đã từ chối", ReservationSchedule.statusInfo(status, reason), false);
            case PENDING:
            default:
                return new ReservationConfirmationView("Đã gửi yêu cầu đặt bàn", "Bàn đã được giữ cho bạn. Nhà hàng sẽ xác nhận sớm, bạn sẽ nhận thông báo khi có kết quả.", true);
        }
    }

    public static String timeLine(Reservation reservation) {
        return reservation.getTimeSlot() + " • " + DisplayFormatters.date(reservation.getDate());
    }

    /** This booking in a fresh list from the server; null when the list no longer has it. */
    @Nullable
    public static Reservation findById(@Nullable List<Reservation> reservations, @Nullable String id) {
        if (reservations == null || id == null) return null;
        for (Reservation value : reservations) if (id.equals(value.getId())) return value;
        return null;
    }
}
