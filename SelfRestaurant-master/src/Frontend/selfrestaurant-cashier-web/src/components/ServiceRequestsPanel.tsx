import { useState } from "react";
import { useControlledRead } from "../lib/useControlledRead";
import { cashierApi } from "../lib/api";
import { parseApiUtcTimestamp } from "../lib/dateTime";
import type { StaffServiceRequestDto } from "../lib/types";
import type { CashierDashboardDto } from "../lib/types";

const labels: Record<string, string> = { CALL_STAFF: "Gọi nhân viên", WATER: "Xin nước", UTENSILS: "Xin dụng cụ", OTHER: "Yêu cầu khác" };

export function ServiceRequestsPanel({ tables = [] }: { tables?: CashierDashboardDto["tables"] }) {
  const [requests, setRequests] = useState<StaffServiceRequestDto[]>([]);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState<number | null>(null);
  const load = useControlledRead({
    read: signal => cashierApi.getServiceRequests(signal),
    apply: value => { setRequests(value); setError(""); },
    error: e => setError(e instanceof Error ? e.message : "Không tải được yêu cầu phục vụ."),
    interval: 5000,
  });

  async function act(item: StaffServiceRequestDto, action: "acknowledge" | "complete") {
    setBusy(item.serviceRequestId);
    try {
      if (action === "acknowledge") await cashierApi.acknowledgeServiceRequest(item.serviceRequestId);
      else await cashierApi.completeServiceRequest(item.serviceRequestId);
      await load();
    } catch (e) { setError(e instanceof Error ? e.message : "Không cập nhật được yêu cầu."); }
    finally { setBusy(null); }
  }

  return <section className="panel cashier-panel-card" aria-label="Yêu cầu phục vụ">
    <div className="cashier-panel-header"><div><h2><i className="bi bi-bell me-2" />Yêu cầu phục vụ</h2><p className="muted">Tự cập nhật mỗi 5 giây cho chi nhánh đang đăng nhập.</p></div><button type="button" className="ghost" onClick={() => void load()}>Làm mới</button></div>
    {error ? <div className="error-box" role="alert">{error}</div> : null}
    {requests.length === 0 ? <p className="muted">Hiện không có yêu cầu đang chờ xử lý.</p> : <div className="service-request-list">
      {requests.map(item => <article className="service-request-card" key={item.serviceRequestId}>
        <div><strong>{tables.find(table => table.tableId === item.tableId)?.number ?? `#${item.tableId}`} · {labels[item.requestType] ?? item.requestType}</strong><p>{item.note || "Không có ghi chú"}</p><small>{parseApiUtcTimestamp(item.createdAtUtc)?.toLocaleString("vi-VN") ?? item.createdAtUtc} · {item.status === "OPEN" ? "Chờ tiếp nhận" : "Đang xử lý"}</small></div>
        <div>{item.status === "OPEN" ? <button disabled={busy === item.serviceRequestId} onClick={() => void act(item, "acknowledge")}>Tiếp nhận</button> : <button disabled={busy === item.serviceRequestId} onClick={() => void act(item, "complete")}>Hoàn tất</button>}</div>
      </article>)}
    </div>}
  </section>;
}
