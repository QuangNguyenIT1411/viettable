package com.restaurant.viettable.network;

public final class ApiException extends Exception {
    private final int statusCode;
    private final String code;
    private final String correlationId;

    public ApiException(int statusCode, String code, String message, String correlationId) {
        super(message);
        this.statusCode = statusCode;
        this.code = code == null ? "request_failed" : code;
        this.correlationId = correlationId;
    }

    public int getStatusCode() { return statusCode; }
    public String getCode() { return code; }
    public String getCorrelationId() { return correlationId; }
}
