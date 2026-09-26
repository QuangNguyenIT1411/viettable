package com.restaurant.viettable.network;

import org.junit.Test;

import java.io.IOException;

import static org.junit.Assert.assertEquals;

public class ApiFailureMapperTest {
    @Test public void ioException_isReportedAsNetworkFailure() {
        ApiException mapped = ApiFailureMapper.fromRequestFailure(new IOException("connection refused"));

        assertEquals("network_error", mapped.getCode());
        assertEquals("Không thể kết nối đến VietTable. Vui lòng kiểm tra mạng và thử lại.", mapped.getMessage());
    }

    @Test public void nonIoException_isNotMisreportedAsNetworkFailure() {
        ApiException mapped = ApiFailureMapper.fromRequestFailure(new IllegalStateException("invalid response"));

        assertEquals("unexpected_response", mapped.getCode());
        assertEquals("Không thể xử lý phản hồi từ máy chủ. Vui lòng thử lại.", mapped.getMessage());
    }
}
