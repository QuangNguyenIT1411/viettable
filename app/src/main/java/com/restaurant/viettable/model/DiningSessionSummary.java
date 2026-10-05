package com.restaurant.viettable.model;

import java.io.Serializable;

public class DiningSessionSummary implements Serializable {
    private final String id;
    private final String code;
    private final String branchId;
    private final String tableId;
    private final String status;
    private final String startedAtUtc;
    private final String endedAtUtc;

    public DiningSessionSummary(String id, String code, String branchId, String tableId,
                                String status, String startedAtUtc, String endedAtUtc) {
        this.id = id;
        this.code = code;
        this.branchId = branchId;
        this.tableId = tableId;
        this.status = status;
        this.startedAtUtc = startedAtUtc;
        this.endedAtUtc = endedAtUtc;
    }

    public String getId() { return id; }
    public String getCode() { return code; }
    public String getBranchId() { return branchId; }
    public String getTableId() { return tableId; }
    public String getStatus() { return status; }
    public String getStartedAtUtc() { return startedAtUtc; }
    public String getEndedAtUtc() { return endedAtUtc; }
}
