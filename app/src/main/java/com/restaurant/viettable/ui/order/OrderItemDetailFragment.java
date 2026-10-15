package com.restaurant.viettable.ui.order;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;

import com.restaurant.viettable.databinding.FragmentOrderItemDetailBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.data.ServiceRequestType;
import com.restaurant.viettable.model.OrderItem;
import com.restaurant.viettable.model.ServiceRequest;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.DisplayFormatters;

public class OrderItemDetailFragment extends BaseFragment {

    private static final String ARG_ITEM = "arg_item";
    private FragmentOrderItemDetailBinding binding;
    private OrderItem item;

    public static OrderItemDetailFragment newInstance(OrderItem item) {
        OrderItemDetailFragment f = new OrderItemDetailFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_ITEM, item);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            item = (OrderItem) getArguments().getSerializable(ARG_ITEM);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentOrderItemDetailBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        if (item != null) {
            binding.tvItemDetailName.setText(item.getFoodName());
            binding.tvItemDetailPrice.setText(item.getFormattedTotalPrice());
            binding.tvItemDetailStatusBadge.setText("TRẠNG THÁI: " + item.getStatusDisplayText().toUpperCase(new java.util.Locale("vi", "VN")));
            binding.btnCancelItem.setVisibility(item.canRequestCancel() ? View.VISIBLE : View.GONE);
            binding.tvItemDetailQuantity.setText(item.getQuantity() + " phần");
            binding.tvItemDetailOptions.setText(item.getOptionsSummary());
            binding.tvItemDetailCreatedAt.setText(DisplayFormatters.dateTime(item.getCreatedAt()));
        }

        binding.btnRemindKitchen.setOnClickListener(v -> {
            if (item != null) {
                MobileRepository.getInstance().createServiceRequest(ServiceRequestType.KITCHEN_RUSH, item.getFoodName(), new MobileRepository.Callback<ServiceRequest>() {
                    @Override public void onSuccess(ServiceRequest value) { showToast("Đã gửi yêu cầu tới bếp"); openDiningSession(); }
                    @Override public void onError(ApiException error) { showToast(apiError(error)); }
                });
            }
        });

        binding.btnCancelItem.setOnClickListener(v -> {
            if (item != null && !item.canRequestCancel()) {
                com.restaurant.viettable.ui.common.DialogHelper.showInfoDialog(
                        requireContext(),
                        "Không thể hủy món",
                        "Món ăn này đã nấu xong hoặc đã bưng tới bàn của bạn nên không thể hủy trực tiếp trên ứng dụng. Vui lòng liên hệ nhân viên để được hỗ trợ.",
                        "Đã hiểu",
                        null
                );
            } else {
                com.restaurant.viettable.ui.common.DialogHelper.showDestructiveDialog(
                        requireContext(),
                        "Yêu cầu hủy món?",
                        "Gửi yêu cầu hủy món " + (item != null ? item.getFoodName() : "") + " tới bếp? Món chỉ bị hủy khi bếp hoặc thu ngân đồng ý.",
                        "Báo hủy",
                        (dialog, which) -> {
                            if (item != null) {
                                MobileRepository.getInstance().createServiceRequest(ServiceRequestType.KITCHEN_CANCEL, item.getFoodName(), new MobileRepository.Callback<ServiceRequest>() {
                                    @Override public void onSuccess(ServiceRequest value) { showToast("Đã gửi yêu cầu tới bếp"); navigateBack(); }
                                    @Override public void onError(ApiException error) { showToast(apiError(error)); }
                                });
                            }
                        },
                        "Không",
                        null
                );
            }
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
