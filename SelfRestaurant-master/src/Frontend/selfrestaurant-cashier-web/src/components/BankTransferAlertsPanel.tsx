import { userMessage, cleanText } from "../../../shared/apiError";
import { formatViDateTime } from "../../../shared/dateFormat";
import { useEffect, useRef, useState } from "react";
import { cashierApi, type BankTransferAlert } from "../lib/api";
import { parseApiUtcTimestamp } from "../lib/dateTime";
import { useControlledRead } from "../lib/useControlledRead";
import { CASHIER_FALLBACK_POLL_MS, CASHIER_OFFLINE_POLL_MS } from "../lib/polling";
import type { TopicBus } from "../../../shared/staffRealtime";

/** Why a received transfer was not confirmed automatically. */
export const bankTransferReasons: Record<string, string> = {
  AMOUNT_TOO_LOW: "Chuyển thiếu tiền",
  NO_MATCH: "Không đọc được mã đơn",
  BRANCH_MISMATCH: "Mã đơn của chi nhánh khác",
  NOT_TRANSFER: "Đơn không chọn chuyển khoản",
  PAYMENT_NOT_PENDING: "Đơn đã thanh toán hoặc đã hủy",
};

/**
 * "Chuyển khoản cần xử lý": money arrived (SePay) but the system did not confirm it. The cashier checks the bank app,
 * confirms by hand in "Thanh toán ứng dụng tại chỗ" if right, then marks the warning as handled. Hidden when empty.
 */
export function BankTransferAlertsPanel({ events, live = false }: { events?: TopicBus; live?: boolean }) {
  const [alerts, setAlerts] = useState<BankTransferAlert[]>([]);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState<number | null>(null);
  const mounted = useRef(false);
  const refresh = useControlledRead({
    read: signal => cashierApi.getBankTransferAlerts(signal),
    apply: value => { setAlerts(value); setError(""); },
    error: e => setError(userMessage(e, "Không tải được chuyển khoản cần xử lý.")),
    interval: live ? CASHIER_FALLBACK_POLL_MS : CASHIER_OFFLINE_POLL_MS,
  });
  useEffect(() => events?.on("onsitePayments", () => void refresh()), [events, refresh]);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);

  const resolve = async (alert: BankTransferAlert) => {
    if (busy !== null) return;
    if (!window.confirm(`Đánh dấu đã xử lý khoản chuyển ${alert.amount.toLocaleString("vi-VN")} đ?`)) return;
    setBusy(alert.eventId);
    try { await cashierApi.resolveBankTransferAlert(alert.eventId); await refresh(); }
    catch (e) { if (mounted.current) setError(userMessage(e, "Không thể đánh dấu đã xử lý.")); }
    finally { if (mounted.current) setBusy(null); }
  };

  if (alerts.length === 0 && !cleanText(error)) return null;
  return <section id="bank-transfer-alerts" className="panel cashier-panel-card" aria-label="Chuyển khoản cần xử lý">
    <div className="cashier-panel-header">
      <div><h2><i className="bi bi-exclamation-triangle me-2" />Chuyển khoản cần xử lý</h2>
        <p className="muted">Tiền đã vào tài khoản nhưng hệ thống không tự xác nhận. Kiểm tra app ngân hàng, xác nhận tay nếu đúng, rồi bấm "Đã xử lý".</p></div>
      {alerts.length > 0 ? <span className="soft-badge warning">{alerts.length} cần xử lý</span> : null}
    </div>
    {cleanText(error) && <div className="error-box" role="alert">{cleanText(error)}<button onClick={() => void refresh()}>Thử lại</button></div>}
    {alerts.length > 0 && <div style={{ overflowX: "auto" }}><table className="table">
      <thead><tr><th>Thời gian</th><th>Số tiền</th><th>Nội dung chuyển khoản</th><th>Lý do</th><th>Đơn liên quan</th><th>Thao tác</th></tr></thead>
      <tbody>{alerts.map(a => <tr key={a.eventId} data-bank-transfer-id={a.eventId}>
        <td>{formatViDateTime(parseApiUtcTimestamp(a.receivedAtUtc))}</td>
        <td>{a.amount.toLocaleString("vi-VN")} đ{a.expectedAmount != null && a.expectedAmount !== a.amount
          ? <><br/><small className="muted">Phải trả {a.expectedAmount.toLocaleString("vi-VN")} đ</small></> : null}</td>
        <td><small>{cleanText(a.content) || "—"}</small></td>
        <td><span className="soft-badge warning">{bankTransferReasons[a.outcome] ?? a.outcome}</span></td>
        <td>{a.paymentId ? <>Giao dịch #{a.paymentId}{a.tableId ? <><br/><small>Bàn #{a.tableId}</small></> : null}</> : "—"}</td>
        <td><button className="cashier-button-outline" disabled={busy !== null} onClick={() => void resolve(a)}>
          {busy === a.eventId ? "Đang lưu…" : "Đã xử lý"}</button></td>
      </tr>)}</tbody>
    </table></div>}
  </section>;
}
