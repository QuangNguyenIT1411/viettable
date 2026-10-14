import { userMessage, cleanText } from "../../../shared/apiError";
import { useEffect, useRef, useState } from "react";
import { cashierApi, type OnsitePayment } from "../lib/api";
import { useControlledRead } from "../lib/useControlledRead";
import { CASHIER_FALLBACK_POLL_MS, CASHIER_OFFLINE_POLL_MS } from "../lib/polling";
import type { TopicBus } from "../../../shared/staffRealtime";

const labels: Record<string, string> = { PENDING: "Chờ thu tiền", SUCCEEDED: "Đã thu tiền", FAILED: "Thất bại", CANCELLED: "Đã hủy", EXPIRED: "Hết hạn" };

export function OnsitePaymentsPanel({ onPendingCountChange, events, live = false }: { onPendingCountChange?: (count: number) => void; events?: TopicBus; live?: boolean } = {}) {
  const [payments, setPayments] = useState<OnsitePayment[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<number | null>(null);
  const submitting = useRef(false);
  const mounted = useRef(false);
  const refresh = useControlledRead({
    read: signal => cashierApi.getOnsitePayments(signal),
    apply: result => { setPayments(result); setError(""); },
    error: e => setError(userMessage(e, "Không tải được giao dịch.")),
    settled: () => setLoading(false),
    interval: live ? CASHIER_FALLBACK_POLL_MS : CASHIER_OFFLINE_POLL_MS,
  });
  useEffect(() => events?.on("onsitePayments", () => void refresh()), [events, refresh]);
  const pendingCount = payments.filter(p => p.status === "PENDING" && p.isCurrentAttempt).length;
  useEffect(() => { onPendingCountChange?.(pendingCount); }, [pendingCount, onPendingCountChange]);
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);
  const confirm = async (payment: OnsitePayment) => {
    if (submitting.current) return;
    if (!window.confirm(`Xác nhận đã thực nhận ${payment.amount.toLocaleString("vi-VN")} đ cho ${payment.paymentCode}${payment.splitPart ? ` — ${payment.splitPart}` : ""}?`)) return;
    submitting.current = true; setBusy(payment.paymentId);
    try {
      await cashierApi.confirmOnsitePayment(payment.paymentId);
      await refresh();
    } catch (e) { if (mounted.current) setError(userMessage(e, "Không thể xác nhận giao dịch.")); }
    finally { submitting.current = false; if (mounted.current) setBusy(null); }
  };
  return <section id="onsite-payments" className="panel cashier-panel-card" aria-label="Thanh toán ứng dụng tại chỗ">
    <div className="cashier-panel-header"><div><h2><i className="bi bi-phone me-2" />Thanh toán ứng dụng tại chỗ</h2><p className="muted">Chỉ xác nhận sau khi thực nhận tiền. Giao dịch ứng dụng được xử lý tại đây, không thu lại qua hóa đơn legacy.</p></div>{pendingCount > 0 ? <span className="soft-badge warning">{pendingCount} chờ thu tiền</span> : null}</div>
    {cleanText(error) && <div className="error-box" role="alert">{cleanText(error)}<button onClick={() => void refresh()}>Thử lại</button></div>}
    {loading ? <p className="muted" role="status">Đang tải giao dịch…</p> : payments.length === 0 ? <p className="muted">Chưa có giao dịch tại chỗ.</p> :
      <div style={{ overflowX: "auto" }}><table className="table"><thead><tr><th>Giao dịch / phiên</th><th>Bàn / phần</th><th>Số tiền</th><th>Phương thức</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
        <tbody>{payments.map(p => <tr key={p.paymentId} data-payment-id={p.paymentId}>
          <td>{p.paymentCode}<br/><small>{p.diningSessionCode}</small></td>
          <td>Bàn #{p.tableId}{p.splitPart && <><br/>{p.splitPart}</>}</td>
          <td>{p.amount.toLocaleString("vi-VN")} đ</td><td>{p.method} / {p.location}</td>
          <td>{labels[p.status] ?? p.status}{!p.isCurrentAttempt && " (đã thay thế)"}</td>
          <td>{p.status === "PENDING" && p.isCurrentAttempt && <button disabled={busy !== null} onClick={() => void confirm(p)}>
            {busy === p.paymentId ? "Đang xác nhận…" : "Xác nhận đã thu tiền"}</button>}</td>
        </tr>)}</tbody></table></div>}
  </section>;
}
