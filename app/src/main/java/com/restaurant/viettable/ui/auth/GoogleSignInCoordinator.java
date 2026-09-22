package com.restaurant.viettable.ui.auth;

import android.app.Activity;
import android.os.CancellationSignal;

import androidx.annotation.NonNull;
import androidx.core.content.ContextCompat;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.NoCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

import java.lang.ref.WeakReference;

final class GoogleSignInCoordinator {
    interface Callback {
        void onIdToken(@NonNull String idToken);
        void onCancelled();
        void onError(@NonNull String message);
    }

    private CancellationSignal cancellationSignal;
    private WeakReference<Callback> callback = new WeakReference<>(null);
    private final GoogleSignInRequestGuard requestGuard = new GoogleSignInRequestGuard();

    boolean isRequestInFlight() {
        return requestGuard.isInFlight();
    }

    void start(@NonNull Activity activity, @NonNull String clientId, @NonNull Callback callback) {
        if (!requestGuard.tryBegin()) return;
        if (!GoogleSignInConfig.isConfigured(clientId)) {
            requestGuard.finish();
            callback.onError("Google Sign-In chưa được cấu hình cho bản build này.");
            return;
        }

        this.callback = new WeakReference<>(callback);
        cancellationSignal = new CancellationSignal();

        try {
            GetSignInWithGoogleOption googleOption = new GetSignInWithGoogleOption.Builder(clientId.trim()).build();
            GetCredentialRequest request = new GetCredentialRequest.Builder()
                    .addCredentialOption(googleOption)
                    .build();

            CredentialManager.create(activity).getCredentialAsync(
                    activity,
                    request,
                    cancellationSignal,
                    ContextCompat.getMainExecutor(activity),
                    new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override
                    public void onResult(@NonNull GetCredentialResponse result) {
                        Callback target = finishRequest();
                        if (target == null) return;
                        Credential credential = result.getCredential();
                        if (!(credential instanceof CustomCredential)
                                || !GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                            target.onError("Google không trả về credential đăng nhập hợp lệ.");
                            return;
                        }
                        try {
                            GoogleIdTokenCredential googleCredential = GoogleIdTokenCredential.createFrom(
                                    ((CustomCredential) credential).getData());
                            String idToken = googleCredential.getIdToken();
                            if (idToken == null || idToken.trim().isEmpty()) {
                                target.onError("Google không trả về ID token hợp lệ.");
                            } else {
                                target.onIdToken(idToken.trim());
                            }
                        } catch (RuntimeException error) {
                            target.onError("Không thể đọc thông tin đăng nhập Google. Vui lòng thử lại.");
                        }
                    }

                    @Override
                    public void onError(@NonNull GetCredentialException error) {
                        Callback target = finishRequest();
                        if (target == null) return;
                        if (error instanceof GetCredentialCancellationException) {
                            target.onCancelled();
                        } else if (error instanceof NoCredentialException) {
                            target.onError("Không tìm thấy tài khoản Google khả dụng trên thiết bị.");
                        } else {
                            target.onError("Không thể mở đăng nhập Google. Vui lòng thử lại.");
                        }
                    }
                    });
        } catch (RuntimeException error) {
            Callback target = finishRequest();
            if (target != null) target.onError("Không thể khởi tạo đăng nhập Google. Vui lòng thử lại.");
        }
    }

    void cancel() {
        if (cancellationSignal != null) cancellationSignal.cancel();
        cancellationSignal = null;
        requestGuard.finish();
        callback.clear();
    }

    private Callback finishRequest() {
        Callback target = callback.get();
        callback.clear();
        cancellationSignal = null;
        requestGuard.finish();
        return target;
    }
}
