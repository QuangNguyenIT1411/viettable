package com.restaurant.viettable.reminder;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.network.ApiException;

import org.junit.Test;

public class ReminderActionsTest {
    @Test
    public void buttonsAreTheTwoAnswers() {
        assertTrue(ReminderActions.isKnown("will_come"));
        assertTrue(ReminderActions.isKnown("cancel"));
        assertFalse(ReminderActions.isKnown("delete"));
        assertFalse(ReminderActions.isKnown(null));
    }

    @Test
    public void onlyWillComeCountsAsAnswered() {
        assertTrue(ReminderActions.isAnswered("WillCome"));
        assertFalse(ReminderActions.isAnswered("None"));
        assertFalse(ReminderActions.isAnswered(null));
    }

    @Test
    public void serverReasonIsShownButNotANetworkFailure() {
        assertEquals("Đặt bàn đã quá giờ hẹn.",
                ReminderActions.failure("will_come", new ApiException(409, "reservation_state_conflict", "Đặt bàn đã quá giờ hẹn.", null)));
        assertEquals("Chưa huỷ được. Chạm để mở đặt bàn và thử lại.",
                ReminderActions.failure("cancel", new ApiException(0, "network", "timeout", null)));
        assertEquals("Chưa gửi được xác nhận. Chạm để mở đặt bàn và thử lại.", ReminderActions.failure("will_come", null));
    }

    @Test
    public void paidDepositIsOnlyOneThatWasTransferred() {
        Reservation unpaid = new Reservation();
        unpaid.setDepositAmount(300_000);
        unpaid.setStatus(Reservation.Status.PENDING_DEPOSIT);
        assertFalse(unpaid.hasPaidDeposit());
        Reservation paid = new Reservation();
        paid.setDepositAmount(300_000);
        paid.setStatus(Reservation.Status.CONFIRMED);
        assertTrue(paid.hasPaidDeposit());
        assertFalse(new Reservation().hasPaidDeposit());
    }
}
