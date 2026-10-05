package com.restaurant.viettable.ui.session;

import android.Manifest;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Bundle;
import android.provider.Settings;
import android.view.ViewGroup;
import android.view.inputmethod.EditorInfo;
import android.view.LayoutInflater;
import android.view.View;
import android.view.inputmethod.InputMethodManager;

import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.annotation.OptIn;
import androidx.camera.core.CameraSelector;
import androidx.camera.core.ExperimentalGetImage;
import androidx.camera.core.ImageAnalysis;
import androidx.camera.core.ImageProxy;
import androidx.camera.core.Preview;
import androidx.camera.lifecycle.ProcessCameraProvider;
import androidx.coordinatorlayout.widget.CoordinatorLayout;
import androidx.core.content.ContextCompat;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowInsetsCompat;

import com.google.common.util.concurrent.ListenableFuture;
import com.google.mlkit.vision.barcode.BarcodeScanner;
import com.google.mlkit.vision.barcode.BarcodeScannerOptions;
import com.google.mlkit.vision.barcode.BarcodeScanning;
import com.google.mlkit.vision.barcode.common.Barcode;
import com.google.mlkit.vision.common.InputImage;
import com.restaurant.viettable.databinding.FragmentQrScanBinding;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.QrPayloadParser;

import java.util.List;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;

public class QrScanFragment extends BaseFragment {
    private static final String CAMERA_PERMISSION_PREFS = "qr_camera_permission";
    private static final String KEY_CAMERA_PERMISSION_REQUESTED = "requested";

    private FragmentQrScanBinding binding;
    private ProcessCameraProvider cameraProvider;
    private ImageAnalysis imageAnalysis;
    private BarcodeScanner barcodeScanner;
    private ExecutorService cameraExecutor;
    private boolean cameraStarted;
    private boolean cameraStartRequested;
    private boolean manualEntryMode;
    private int manualPanelBaseBottomMargin;
    private final AtomicBoolean frameInFlight = new AtomicBoolean(false);
    private final AtomicBoolean qrConsumed = new AtomicBoolean(false);

    private final ActivityResultLauncher<String> cameraPermissionLauncher =
            registerForActivityResult(new ActivityResultContracts.RequestPermission(), granted -> {
                if (binding == null) return;
                if (granted) {
                    startCamera();
                } else {
                    showCameraPermissionUnavailable(!shouldShowRequestPermissionRationale(Manifest.permission.CAMERA));
                }
            });

    public static QrScanFragment newInstance() {
        return new QrScanFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container,
                             @Nullable Bundle savedInstanceState) {
        binding = FragmentQrScanBinding.inflate(inflater, container, false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        qrConsumed.set(false);
        frameInFlight.set(false);
        cameraExecutor = Executors.newSingleThreadExecutor();
        BarcodeScannerOptions options = new BarcodeScannerOptions.Builder()
                .setBarcodeFormats(Barcode.FORMAT_QR_CODE)
                .build();
        barcodeScanner = BarcodeScanning.getClient(options);

        binding.btnCameraPermission.setOnClickListener(v -> handleCameraPermissionAction());
        binding.btnToggleManualEntry.setOnClickListener(v -> {
            if (manualEntryMode) showCameraMode(); else showManualMode(true);
        });
        binding.btnSubmitTableCode.setOnClickListener(v -> submitManualCode());
        binding.etTableCode.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == EditorInfo.IME_ACTION_DONE) {
                submitManualCode();
                return true;
            }
            return false;
        });
        ViewGroup.LayoutParams panelParams = binding.manualEntryCard.getLayoutParams();
        if (panelParams instanceof CoordinatorLayout.LayoutParams) {
            manualPanelBaseBottomMargin = ((CoordinatorLayout.LayoutParams) panelParams).bottomMargin;
        }
        ViewCompat.setOnApplyWindowInsetsListener(binding.manualEntryCard, (panel, windowInsets) -> {
            int imeBottom = windowInsets.isVisible(WindowInsetsCompat.Type.ime())
                    ? windowInsets.getInsets(WindowInsetsCompat.Type.ime()).bottom : 0;
            int navigationBottom = windowInsets.getInsets(WindowInsetsCompat.Type.navigationBars()).bottom;
            int extraBottom = Math.max(0, imeBottom - navigationBottom);
            ViewGroup.LayoutParams raw = panel.getLayoutParams();
            if (raw instanceof CoordinatorLayout.LayoutParams) {
                CoordinatorLayout.LayoutParams params = (CoordinatorLayout.LayoutParams) raw;
                params.bottomMargin = manualPanelBaseBottomMargin + extraBottom;
                panel.setLayoutParams(params);
            }
            return windowInsets;
        });
        ensureCameraPermission();
    }

    @Override
    public void onResume() {
        super.onResume();
        setStatusBarLightIcons(true);
        if (binding == null) return;

        // Returning from validation (including a real API rejection) must make the scanner
        // usable again. The Fragment view can be retained while the back-stack transaction
        // is popped, so onViewCreated() is not guaranteed to reset this gate.
        if (qrConsumed.getAndSet(false)) {
            frameInFlight.set(false);
            binding.progressCamera.setVisibility(View.GONE);
            binding.btnSubmitTableCode.setEnabled(true);
            if (manualEntryMode) {
                showManualMode(false);
            } else if (!hasCameraPermission()) {
                showCameraPermissionUnavailable(
                        !shouldShowRequestPermissionRationale(Manifest.permission.CAMERA));
            }
        }
        if (!manualEntryMode && hasCameraPermission() && !cameraStarted) {
            startCamera();
        }
    }

    @Override
    public void onPause() {
        stopCamera();
        setStatusBarLightIcons(false);
        super.onPause();
    }

    private void submitManualCode() {
        String input = binding.etTableCode.getText() == null
                ? "" : binding.etTableCode.getText().toString().trim().toUpperCase(Locale.ROOT);
        String code = QrPayloadParser.parse(input);
        if (code == null) {
            binding.tilTableCode.setError("Vui lòng nhập mã bàn hợp lệ");
            return;
        }
        binding.tilTableCode.setError(null);
        consumeQrCode(code);
    }

    private void ensureCameraPermission() {
        if (hasCameraPermission()) {
            startCamera();
            return;
        }
        boolean requestedBefore = requireContext()
                .getSharedPreferences(CAMERA_PERMISSION_PREFS, Context.MODE_PRIVATE)
                .getBoolean(KEY_CAMERA_PERMISSION_REQUESTED, false);
        if (!requestedBefore) {
            requestCameraPermission();
        } else {
            showCameraPermissionUnavailable(!shouldShowRequestPermissionRationale(Manifest.permission.CAMERA));
        }
    }

    private void handleCameraPermissionAction() {
        if (hasCameraPermission()) {
            startCamera();
        } else if (shouldShowRequestPermissionRationale(Manifest.permission.CAMERA)) {
            requestCameraPermission();
        } else {
            Intent intent = new Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                    Uri.fromParts("package", requireContext().getPackageName(), null));
            startActivity(intent);
        }
    }

    private void requestCameraPermission() {
        requireContext().getSharedPreferences(CAMERA_PERMISSION_PREFS, Context.MODE_PRIVATE)
                .edit().putBoolean(KEY_CAMERA_PERMISSION_REQUESTED, true).apply();
        cameraPermissionLauncher.launch(Manifest.permission.CAMERA);
    }

    private boolean hasCameraPermission() {
        return ContextCompat.checkSelfPermission(requireContext(), Manifest.permission.CAMERA)
                == PackageManager.PERMISSION_GRANTED;
    }

    private void showCameraPermissionUnavailable(boolean permanentlyDenied) {
        if (binding == null) return;
        binding.tvCameraStatus.setText(permanentlyDenied
                ? "Quyền camera đang bị tắt. Bạn vẫn có thể nhập mã bàn thủ công."
                : "Cần quyền camera để quét QR. Bạn vẫn có thể nhập mã bàn thủ công.");
        binding.tvScanHint.setText("Camera chưa được cấp quyền");
        binding.btnCameraPermission.setText(permanentlyDenied ? "Mở cài đặt" : "Cho phép camera");
        binding.btnCameraPermission.setVisibility(View.VISIBLE);
        showManualMode(false);
    }

    private void startCamera() {
        if (binding == null || manualEntryMode || cameraStarted || cameraStartRequested
                || qrConsumed.get() || !hasCameraPermission()) return;
        cameraStartRequested = true;
        showCameraInitializing();
        ListenableFuture<ProcessCameraProvider> providerFuture =
                ProcessCameraProvider.getInstance(requireContext());
        providerFuture.addListener(() -> {
            if (binding == null || !isAdded() || !cameraStartRequested || qrConsumed.get()) return;
            try {
                cameraProvider = providerFuture.get();
                Preview preview = new Preview.Builder().build();
                preview.setSurfaceProvider(binding.previewView.getSurfaceProvider());

                imageAnalysis = new ImageAnalysis.Builder()
                        .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                        .build();
                imageAnalysis.setAnalyzer(cameraExecutor, this::analyzeImage);

                cameraProvider.unbindAll();
                cameraProvider.bindToLifecycle(getViewLifecycleOwner(), CameraSelector.DEFAULT_BACK_CAMERA,
                        preview, imageAnalysis);
                cameraStarted = true;
                cameraStartRequested = false;
                showCameraActive();
            } catch (Exception error) {
                cameraStarted = false;
                cameraStartRequested = false;
                showCameraError("Không thể mở camera. Hãy dùng mã bàn nhập thủ công.");
            }
        }, ContextCompat.getMainExecutor(requireContext()));
    }

    @OptIn(markerClass = ExperimentalGetImage.class)
    private void analyzeImage(@NonNull ImageProxy imageProxy) {
        if (qrConsumed.get() || barcodeScanner == null || !frameInFlight.compareAndSet(false, true)) {
            imageProxy.close();
            return;
        }
        if (imageProxy.getImage() == null) {
            frameInFlight.set(false);
            imageProxy.close();
            return;
        }

        InputImage image = InputImage.fromMediaImage(
                imageProxy.getImage(), imageProxy.getImageInfo().getRotationDegrees());
        barcodeScanner.process(image)
                .addOnSuccessListener(this::handleDetectedBarcodes)
                .addOnFailureListener(error -> postCameraError("Không thể đọc khung hình. Đang thử lại..."))
                .addOnCompleteListener(task -> {
                    frameInFlight.set(false);
                    imageProxy.close();
                });
    }

    private void handleDetectedBarcodes(List<Barcode> barcodes) {
        if (qrConsumed.get()) return;
        for (Barcode barcode : barcodes) {
            String code = QrPayloadParser.parse(barcode.getRawValue());
            if (code != null) {
                if (qrConsumed.compareAndSet(false, true)) {
                    if (imageAnalysis != null) imageAnalysis.clearAnalyzer();
                    if (binding != null) binding.getRoot().post(() -> navigateWithConsumedQr(code));
                }
                return;
            }
        }
        if (!barcodes.isEmpty()) postCameraError("QR không có mã bàn hợp lệ. Hãy thử lại.");
    }

    private void consumeQrCode(String code) {
        if (!qrConsumed.compareAndSet(false, true)) return;
        if (imageAnalysis != null) imageAnalysis.clearAnalyzer();
        navigateWithConsumedQr(code);
    }

    private void navigateWithConsumedQr(String code) {
        if (binding == null || !isAdded()) return;
        hideKeyboard();
        binding.progressCamera.setVisibility(View.VISIBLE);
        binding.tvCameraStatus.setText("Đã đọc QR. Đang xác thực mã bàn...");
        binding.tvScanHint.setText("Đang xác thực");
        binding.btnSubmitTableCode.setEnabled(false);
        navigateTo(QrValidationFragment.newInstance(code));
    }

    private void showCameraInitializing() {
        if (binding == null) return;
        binding.progressCamera.setVisibility(View.VISIBLE);
        binding.btnCameraPermission.setVisibility(View.GONE);
        binding.tvCameraStatus.setText("Đang khởi tạo camera...");
        binding.tvScanHint.setText("Vui lòng chờ");
    }

    private void showCameraActive() {
        if (binding == null) return;
        binding.progressCamera.setVisibility(View.GONE);
        binding.btnCameraPermission.setVisibility(View.GONE);
        binding.tvCameraStatus.setText("Camera đang hoạt động. Đưa QR trên bàn vào giữa khung.");
        binding.tvScanHint.setText("Đưa mã QR vào giữa khung");
    }

    private void showCameraError(String message) {
        if (binding == null) return;
        binding.cameraContainer.setVisibility(View.VISIBLE);
        binding.progressCamera.setVisibility(View.GONE);
        binding.tvCameraStatus.setText(message);
        binding.tvScanHint.setText("Có thể nhập mã bàn bên dưới");
    }

    private void showManualMode(boolean focusInput) {
        if (binding == null) return;
        manualEntryMode = true;
        stopCamera();
        binding.tvQrTitle.setText("Nhập mã bàn");
        binding.btnToggleManualEntry.setText("Quét mã QR");
        binding.cameraContainer.setVisibility(View.GONE);
        binding.manualEntryCard.setVisibility(View.VISIBLE);
        binding.tvCameraStatus.setText(hasCameraPermission()
                ? "Nhập mã bàn được in bên dưới QR để tiếp tục."
                : "Camera không khả dụng; bạn vẫn có thể nhập mã bàn.");
        if (focusInput) {
            binding.etTableCode.requestFocus();
            binding.etTableCode.post(() -> {
                if (binding == null || !manualEntryMode) return;
                InputMethodManager manager = (InputMethodManager) requireContext()
                        .getSystemService(Context.INPUT_METHOD_SERVICE);
                if (manager != null) manager.showSoftInput(binding.etTableCode, InputMethodManager.SHOW_IMPLICIT);
                ViewCompat.requestApplyInsets(binding.manualEntryCard);
            });
        }
    }

    private void showCameraMode() {
        if (binding == null) return;
        hideKeyboard();
        binding.etTableCode.clearFocus();
        manualEntryMode = false;
        binding.tvQrTitle.setText("Quét mã QR tại bàn");
        binding.btnToggleManualEntry.setText("Nhập mã bàn thủ công");
        binding.manualEntryCard.setVisibility(View.GONE);
        binding.cameraContainer.setVisibility(View.VISIBLE);
        if (hasCameraPermission()) startCamera();
        else showCameraPermissionUnavailable(!shouldShowRequestPermissionRationale(Manifest.permission.CAMERA));
    }

    private void hideKeyboard() {
        if (binding == null || getContext() == null) return;
        InputMethodManager manager = (InputMethodManager) requireContext()
                .getSystemService(Context.INPUT_METHOD_SERVICE);
        if (manager != null) manager.hideSoftInputFromWindow(binding.etTableCode.getWindowToken(), 0);
    }

    private void postCameraError(String message) {
        if (binding != null) binding.getRoot().post(() -> {
            if (!qrConsumed.get()) showCameraError(message);
        });
    }

    private void stopCamera() {
        if (imageAnalysis != null) imageAnalysis.clearAnalyzer();
        if (cameraProvider != null) cameraProvider.unbindAll();
        imageAnalysis = null;
        cameraStarted = false;
        cameraStartRequested = false;
        frameInFlight.set(false);
    }

    @Override
    public void onDestroyView() {
        stopCamera();
        if (barcodeScanner != null) {
            barcodeScanner.close();
            barcodeScanner = null;
        }
        if (cameraExecutor != null) {
            cameraExecutor.shutdownNow();
            cameraExecutor = null;
        }
        binding = null;
        super.onDestroyView();
    }
}
