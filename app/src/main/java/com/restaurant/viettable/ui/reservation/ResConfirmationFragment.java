package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.databinding.FragmentResConfirmationBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.reminder.ReservationReminders;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.common.ListState;
import com.restaurant.viettable.util.ReservationConfirmationView;
import com.restaurant.viettable.util.SafeText;

import java.util.List;

/**
 * "Đặt bàn thành công". The guest can go to the detail screen, change or cancel, and come back, so the booking is read
 * again from the server every time this screen returns; the copy in the arguments is only true right after booking.
 */
public class ResConfirmationFragment extends BaseFragment implements AppStateManager.StateChangeListener {

    private static final String ARG_RES = "arg_res";
    private FragmentResConfirmationBinding binding;
    private Reservation reservation;
    /** The booking may have changed since it was last read: show nothing of it until the server answers. */
    private boolean staleUntilLoaded;
    private boolean loading;
    private String loadError;
    private int loadToken;

    public static ResConfirmationFragment newInstance(Reservation res) {
        ResConfirmationFragment f = new ResConfirmationFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_RES, res);
        f.setArguments(b);
        return f;
    }

    // Android 13+: booking reminders need the notification permission; asked once, a refusal changes nothing else.
    private final androidx.activity.result.ActivityResultLauncher<String> notificationPermission =
            registerForActivityResult(new androidx.activity.result.contract.ActivityResultContracts.RequestPermission(), granted -> {
                if (granted) MobileRepository.getInstance().loadReservations(new MobileRepository.Callback<java.util.List<Reservation>>() {
                    @Override public void onSuccess(java.util.List<Reservation> value) {}
                    @Override public void onError(com.restaurant.viettable.network.ApiException error) {}
                });
            });

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            reservation = (Reservation) getArguments().getSerializable(ARG_RES);
        }
        staleUntilLoaded = savedInstanceState != null;
        if (savedInstanceState == null && ReservationReminders.shouldAskPermission(requireContext())) {
            ReservationReminders.markPermissionAsked(requireContext());
            notificationPermission.launch(android.Manifest.permission.POST_NOTIFICATIONS);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentResConfirmationBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (reservation == null) {
            showToast("Không tìm thấy thông tin đặt bàn.");
            setBottomNavVisible(true);
            switchTab(R.id.nav_home);
            return;
        }

        binding.btnViewReservationDetail.setOnClickListener(v -> navigateTo(ResDetailFragment.newInstance(reservation)));
        binding.btnNewReservation.setOnClickListener(v -> {
            setBottomNavVisible(true);
            switchTab(R.id.nav_reservation);
        });
        binding.btnGoHome.setOnClickListener(v -> goHome());
        // Back = "Về trang chủ" in every state: switchTab clears the back stack, so the booking form's steps never
        // open again under this screen (owner, 05/11).
        requireActivity().getOnBackPressedDispatcher().addCallback(getViewLifecycleOwner(),
                new androidx.activity.OnBackPressedCallback(true) {
                    @Override public void handleOnBackPressed() { goHome(); }
                });
        binding.btnConfirmRetry.setOnClickListener(v -> reload());

        loading = staleUntilLoaded;
        render();
        appStateManager.addListener(this);
    }

    @Override
    public void onResume() {
        super.onResume();
        if (reservation != null) reload();
    }

    private void goHome() {
        setBottomNavVisible(true);
        switchTab(R.id.nav_home);
    }

    /** Right after booking the screen already holds the server's answer, so that read stays silent; a return waits for it. */
    private void reload() {
        boolean silent = !staleUntilLoaded;
        int token = ++loadToken;
        if (!silent) {
            loading = true;
            loadError = null;
            render();
        }
        MobileRepository.getInstance().loadReservations(new MobileRepository.Callback<List<Reservation>>() {
            @Override public void onSuccess(List<Reservation> values) {
                if (token != loadToken) return;
                Reservation fresh = ReservationConfirmationView.findById(values, reservation.getId());
                loading = false;
                if (fresh != null) {
                    reservation = fresh;
                    staleUntilLoaded = false;
                    loadError = null;
                } else if (!silent) {
                    loadError = "Không tìm thấy đặt bàn này.";
                }
                render();
            }
            @Override public void onError(ApiException error) {
                if (token != loadToken) return;
                loading = false;
                if (!silent && isAdded()) loadError = apiError(error);
                render();
            }
        });
    }

    /** A push from the server (the restaurant confirmed, say) while this screen is open. */
    @Override
    public void onStateChanged() {
        if (binding == null || staleUntilLoaded || loading) return;
        Reservation fresh = ReservationConfirmationView.findById(appStateManager.getReservations(), reservation.getId());
        if (fresh != null) {
            reservation = fresh;
            render();
        }
    }

    private void render() {
        if (binding == null) return;
        boolean hasBooking = !staleUntilLoaded;
        ListState.Mode mode = ListState.render(binding.layoutConfirmSummary, binding.progressConfirm, binding.tvConfirmError,
                binding.btnConfirmRetry, hasBooking, loading, loadError, "");
        boolean showing = mode == ListState.Mode.CONTENT;
        ReservationConfirmationView view = ReservationConfirmationView.of(reservation.getStatus(), reservation.getStatusReason());
        binding.btnViewReservationDetail.setVisibility(showing && view.standing ? View.VISIBLE : View.GONE);
        binding.btnNewReservation.setVisibility(showing && !view.standing ? View.VISIBLE : View.GONE);
        if (!showing) return;

        binding.viewConfirmGlow.setBackgroundResource(view.standing ? R.drawable.pd_glow_mint : R.drawable.pd_glow_red);
        binding.viewConfirmRing.setVisibility(view.standing ? View.VISIBLE : View.GONE);
        binding.ivConfirmIcon.setBackgroundResource(view.standing ? R.drawable.pd_circle_mint : R.drawable.pd_circle_red);
        binding.ivConfirmIcon.setImageResource(view.standing ? R.drawable.ic_check : R.drawable.ic_close);
        binding.tvConfirmTitle.setText(view.title);
        binding.tvConfirmSubtitle.setText(view.subtitle);

        binding.tvConfirmCode.setText("#" + reservation.getId());
        if (reservation.getStatus() != null) {
            binding.tvConfirmStatus.setVisibility(View.VISIBLE);
            com.restaurant.viettable.ui.common.PdStatusPill.apply(binding.tvConfirmStatus, reservation.getStatusDisplayText(),
                    com.restaurant.viettable.ui.common.PdStatusPill.toneFor(reservation.getStatus()));
        }
        binding.tvConfirmBranch.setText(SafeText.isBlank(reservation.getBranchAddress()) ? SafeText.clean(reservation.getBranchName())
                : SafeText.join(" ", SafeText.clean(reservation.getBranchName()), "(" + SafeText.clean(reservation.getBranchAddress()) + ")"));
        binding.tvConfirmTimeDate.setText(ReservationConfirmationView.timeLine(reservation));
        binding.tvConfirmTableArea.setText(SafeText.join(" • ", reservation.getTableLabel(), reservation.getAreaName(), reservation.getGuestCount() + " khách"));

        if (reservation.getDepositAmount() > 0) {
            binding.tvConfirmDeposit.setText("Tiền cọc: Đã thanh toán " + String.format(java.util.Locale.GERMANY, "%,dđ", reservation.getDepositAmount()));
        } else {
            binding.tvConfirmDeposit.setText("Tiền cọc: Không yêu cầu");
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        appStateManager.removeListener(this);
        binding = null;
        // Gone to another screen: whatever happens there, this copy can no longer be trusted when coming back.
        staleUntilLoaded = true;
        loadToken++;
    }
}
