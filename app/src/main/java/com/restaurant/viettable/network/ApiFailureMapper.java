package com.restaurant.viettable.network;

import java.io.IOException;

/** Classifies transport failures separately from malformed/unexpected client responses. */
public final class ApiFailureMapper {
    private ApiFailureMapper() { }

    public static ApiException fromRequestFailure(Exception error) {
        if (error instanceof IOException) {
            return new ApiException(0, "network_error",
                    "Không thể kết nối đến VietTable. Vui lòng kiểm tra mạng và thử lại.", null);
        }
        return new ApiException(0, "unexpected_response",
                "Không thể xử lý phản hồi từ máy chủ. Vui lòng thử lại.", null);
    }
}
