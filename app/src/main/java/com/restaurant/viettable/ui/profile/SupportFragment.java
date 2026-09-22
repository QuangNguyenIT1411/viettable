package com.restaurant.viettable.ui.profile;

import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentSupportBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.FaqItem;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.List;

public class SupportFragment extends BaseFragment {

    private FragmentSupportBinding binding;

    public static SupportFragment newInstance() {
        return new SupportFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentSupportBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());
        binding.btnRetryFaq.setOnClickListener(v -> loadFaqs());
        loadFaqs();

        binding.btnSendFeedback.setOnClickListener(v -> {
            String feedback = binding.etFeedback.getText() != null ? binding.etFeedback.getText().toString().trim() : "";
            if (TextUtils.isEmpty(feedback)) {
                binding.tilFeedback.setError("Vui lòng nhập nội dung góp ý");
                return;
            }
            binding.tilFeedback.setError(null);
            binding.btnSendFeedback.setEnabled(false);
            MobileRepository.getInstance().submitSupport(feedback, new MobileRepository.Callback<Void>() {
                @Override public void onSuccess(Void value) { showToast("Cảm ơn quý khách đã gửi góp ý! Chúng tôi sẽ phản hồi sớm nhất."); navigateBack(); }
                @Override public void onError(ApiException error) { if (binding != null) { binding.btnSendFeedback.setEnabled(true); showToast(apiError(error)); } }
            });
        });
    }

    private void loadFaqs() {
        if (binding == null) return;
        clearRenderedFaqs();
        binding.progressFaq.setVisibility(View.VISIBLE);
        binding.tvFaqState.setVisibility(View.GONE);
        binding.btnRetryFaq.setVisibility(View.GONE);
        MobileRepository.getInstance().loadFaqs(new MobileRepository.Callback<List<FaqItem>>() {
            @Override public void onSuccess(List<FaqItem> values) {
                if (binding == null) return;
                binding.progressFaq.setVisibility(View.GONE);
                if (values.isEmpty()) {
                    binding.tvFaqState.setText("Hiện chưa có câu hỏi thường gặp.");
                    binding.tvFaqState.setVisibility(View.VISIBLE);
                    return;
                }
                for (int i = 0; i < values.size(); i++) addFaq(values.get(i), i + 1);
            }

            @Override public void onError(ApiException error) {
                if (binding == null) return;
                binding.progressFaq.setVisibility(View.GONE);
                binding.tvFaqState.setText(apiError(error));
                binding.tvFaqState.setVisibility(View.VISIBLE);
                binding.btnRetryFaq.setVisibility(View.VISIBLE);
            }
        });
    }

    private void clearRenderedFaqs() {
        while (binding.layoutFaqItems.getChildCount() > 3) binding.layoutFaqItems.removeViewAt(3);
    }

    private void addFaq(FaqItem item, int position) {
        int horizontal = Math.round(4 * getResources().getDisplayMetrics().density);
        LinearLayout row = new LinearLayout(requireContext());
        row.setOrientation(LinearLayout.VERTICAL);
        row.setPadding(horizontal, horizontal * 2, horizontal, horizontal * 2);
        TextView question = new TextView(requireContext());
        question.setText(position + ". " + item.getQuestion());
        question.setTextSize(14); question.setTextColor(requireContext().getColor(com.restaurant.viettable.R.color.colorTextPrimary));
        question.setTypeface(question.getTypeface(), android.graphics.Typeface.BOLD);
        TextView answer = new TextView(requireContext());
        answer.setText(item.getAnswer()); answer.setTextSize(12);
        answer.setTextColor(requireContext().getColor(com.restaurant.viettable.R.color.colorTextSecondary));
        answer.setPadding(0, horizontal, 0, 0); answer.setVisibility(View.GONE);
        row.addView(question); row.addView(answer);
        row.setOnClickListener(v -> answer.setVisibility(answer.getVisibility() == View.VISIBLE ? View.GONE : View.VISIBLE));
        binding.layoutFaqItems.addView(row);
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
