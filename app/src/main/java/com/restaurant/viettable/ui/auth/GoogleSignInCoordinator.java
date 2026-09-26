package com.restaurant.viettable.ui.auth;

import android.app.Activity;
import android.os.CancellationSignal;
import android.util.Log;

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
    private static final String TAG = "GoogleSignIn";

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
            if (com.restaurant.viettable.BuildConfig.DEBUG) Log.d(TAG, "Credential request started");
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
                        if (com.restaurant.viettable.BuildConfig.DEBUG) {
                            Log.d(TAG, "Credential response received; type=" + credential.getType());
                        }
                        if (!(credential instanceof CustomCredential)
                                || !GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                            if (com.restaurant.viettable.BuildConfig.DEBUG) Log.w(TAG, "Credential type is not a Google ID token credential");
                            target.onError("Google không trả về credential đăng nhập hợp lệ.");
                            return;
                        }
                        try {
                            GoogleIdTokenCredential googleCredential = GoogleIdTokenCredential.createFrom(
                                    ((CustomCredential) credential).getData());
                            String idToken = googleCredential.getIdToken();
                            if (idToken == null || idToken.trim().isEmpty()) {
                                if (com.restaurant.viettable.BuildConfig.DEBUG) Log.w(TAG, "Google ID token was empty");
                                target.onError("Google không trả về ID token hợp lệ.");
                            } else {
                                if (com.restaurant.viettable.BuildConfig.DEBUG) Log.d(TAG, "Google ID token parsed successfully");
                                target.onIdToken(idToken.trim());
                            }
                        } catch (RuntimeException error) {
                            if (com.restaurant.viettable.BuildConfig.DEBUG) {
                                Log.e(TAG, "Google ID token parse failed; exception=" + error.getClass().getName()
                                        + "; message=" + safeDiagnostic(error.getMessage()));
                            }
                            target.onError("Không thể đọc thông tin đăng nhập Google. Vui lòng thử lại.");
                        }
                    }

                    @Override
                    public void onError(@NonNull GetCredentialException error) {
                        Callback target = finishRequest();
                        if (target == null) return;
                        if (com.restaurant.viettable.BuildConfig.DEBUG) {
                            Log.w(TAG, "Credential request failed; exception=" + error.getClass().getName());
                        }
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
            if (com.restaurant.viettable.BuildConfig.DEBUG) {
                Log.e(TAG, "Credential request initialization failed; exception=" + error.getClass().getName()
                        + "; message=" + safeDiagnostic(error.getMessage()));
            }
            if (target != null) target.onError("Không thể khởi tạo đăng nhập Google. Vui lòng thử lại.");
        }
    }

    private static String safeDiagnostic(String message) {
        if (message == null) return "<none>";
        return message.replaceAll("eyJ[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+", "[token-redacted]")
                .replaceAll("(?i)[A-Z0-9._%+-]+@[A-Z0-9.-]+\\.[A-Z]{2,}", "[email-redacted]")
                .replaceAll("(?i)(idToken|accessToken|refreshToken)(\\s*[:=]\\s*)[^, }]+", "$1$2[redacted]");
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
