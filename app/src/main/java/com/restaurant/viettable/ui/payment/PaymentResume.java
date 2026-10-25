package com.restaurant.viettable.ui.payment;

import android.widget.Toast;

import androidx.annotation.NonNull;
import androidx.fragment.app.Fragment;

import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.PaymentInfo;
import com.restaurant.viettable.navigation.NavigationCoordinator;
import com.restaurant.viettable.network.ApiException;

/**
 * Back to the payment that is already waiting, instead of a second payment or unlocking the table
 * (incident 25/10: Back unlocked the session while the transfer TT28 was still pending).
 */
public final class PaymentResume {
    private PaymentResume() { }

    /** Server codes meaning "a payment of this session is waiting or done". */
    public static boolean isPaymentBlocked(ApiException error) {
        return error != null && ("payment_in_progress".equals(error.getCode()) || "session_already_paid".equals(error.getCode()));
    }

    /** Opens the waiting screen of the session's pending payment; shows the server message when nothing waits. */
    public static void open(@NonNull Fragment from, @NonNull ApiException reason) {
        MobileRepository.getInstance().loadActivePayment(new MobileRepository.Callback<PaymentInfo>() {
            @Override public void onSuccess(PaymentInfo payment) {
                if (!from.isAdded() || !(from.requireActivity() instanceof NavigationCoordinator)) return;
                Invoice base = MobileRepository.getInstance().currentCheckoutInvoice();
                if (payment == null || base == null || !"ONSITE".equals(payment.getChannel())) { toast(from, reason.getMessage()); return; }
                boolean transfer = "TRANSFER".equals(payment.getMethod()) || "QR".equals(payment.getMethod());
                String location = transfer ? PaymentTypeFragment.TRANSFER_LOCATION_LABEL
                        : "CASHIER".equals(payment.getLocation()) ? "Tại quầy thu ngân" : "Tại bàn";
                Invoice invoice = new Invoice(base.getId(), base.getDiningSessionId(), base.getBranchName(), base.getTableId(),
                        base.getSubtotal(), base.getServiceFee(), base.getTax(), base.getVoucherDiscount(), base.getLoyaltyDiscount(),
                        payment.getAmount(), transfer ? PaymentTypeFragment.TRANSFER_LABEL : PaymentTypeFragment.CASH_LABEL, location, "Đang chờ");
                invoice.setTableLabel(base.getTableLabel());
                toast(from, "Bạn đang có một giao dịch chờ xác nhận. Đã mở lại màn chờ thanh toán.");
                ((NavigationCoordinator) from.requireActivity()).navigateTo(OnsiteStatusFragment.newInstance(invoice, location, payment), true);
            }
            @Override public void onError(ApiException error) { toast(from, reason.getMessage()); }
        });
    }

    private static void toast(Fragment from, String message) {
        if (from.getContext() != null) Toast.makeText(from.getContext(), message, Toast.LENGTH_LONG).show();
    }
}
