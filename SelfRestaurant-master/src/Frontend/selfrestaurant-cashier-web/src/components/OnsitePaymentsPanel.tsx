import { userMessage, cleanText } from "../../../shared/apiError";
import { useEffect, useRef, useState } from "react";
import { cashierApi, type OnsitePayment, type TransferQrDto } from "../lib/api";
import { paymentMethodLabel, paymentMethodGroup } from "../../../shared/paymentMethod";
import { TransferQrCard } from "./TransferQrCard";
import { useControlledRead } from "../lib/useControlledRead";
import { CASHIER_FALLBACK_POLL_MS, CASHIER_OFFLINE_POLL_MS } from "../lib/polling";
import type { TopicBus } from "../../../shared/staffRealtime";

const labels: Record<string, string> = { PENDING: "Chờ thu tiền", SUCCEEDED: "Đã thu tiền", FAILED: "Thất bại", CANCELLED: "Đã hủy", EXPIRED: "Hết hạn" };

export function OnsitePaymentsPanel({ onPendingCountChange, events, live = false }: { onPendingCountChange?: (count: number) => void; events?: TopicBus; live?: boolean } = {}) {
  const [payments, setPayments] = useState<OnsitePayment[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<number | null>(null);
  const [qrView, setQrView] = useState<{ payment: OnsitePayment; qr: TransferQrDto | null; error: string | null } | null>(null);
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
  const showQr = async (payment: OnsitePayment) => {
    setQrView({ payment, qr: null, error: null });
    try { const qr = await cashierApi.getPaymentTransferQr(payment.paymentId); setQrView({ payment, qr, error: null }); }
    catch (e) { setQrView({ payment, qr: null, error: userMessage(e, "Không tạo được mã QR chuyển khoản.") }); }
  };
  const confirm = async (payment: OnsitePayment) => {
    if (submitting.current) return;
    const what = paymentMethodGroup(payment.method) === "TRANSFER" ? "đã nhận chuyển khoản" : "đã thực nhận";
    if (!window.confirm(`Xác nhận ${what} ${payment.amount.toLocaleString("vi-VN")} đ cho ${payment.paymentCode}${payment.splitPart ? ` — ${payment.splitPart}` : ""}?`)) return;
    submitting.current = true; setBusy(payment.paymentId);
    try {
      await cashierApi.confirmOnsitePayment(payment.paymentId);
      await refresh();
    } catch (e) { if (mounted.current) setError(userMessage(e, "Không thể xác nhận giao dịch.")); }
    finally { submitting.current = false; if (mounted.current) setBusy(null); }
  };
  const cancel = async (payment: OnsitePayment) => {
    if (submitting.current) return;
    const warning = paymentMethodGroup(payment.method) === "TRANSFER"
      ? "Chỉ hủy khi chắc chắn khách CHƯA chuyển khoản (hỏi khách và xem app ngân hàng của khách)."
      : "Chỉ hủy khi chắc chắn CHƯA thu tiền của khách.";
    if (!window.confirm(`Hủy giao dịch chờ ${payment.paymentCode} (${payment.amount.toLocaleString("vi-VN")} đ)?\n\n${warning}\n\nSau khi hủy, khách hoặc thu ngân chọn lại cách thanh toán.`)) return;
    submitting.current = true; setBusy(payment.paymentId);
    try {
      await cashierApi.cancelOnsitePayment(payment.paymentId);
      await refresh();
    } catch (e) { if (mounted.current) setError(userMessage(e, "Không thể hủy giao dịch.")); }
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
          <td>{p.amount.toLocaleString("vi-VN")} đ</td><td>{paymentMethodLabel(p.method)}{p.location ? <><br/><small>{p.location === "CASHIER" ? "Tại quầy" : p.location === "TABLE" ? "Tại bàn" : p.location}</small></> : null}</td>
          <td>{p.status === "SUCCEEDED" && p.providerName === "SEPAY"
            ? <span className="soft-badge success">Đã thanh toán tự động</span>
            : labels[p.status] ?? p.status}{!p.isCurrentAttempt && " (đã thay thế)"}</td>
          <td>{p.status === "PENDING" && p.isCurrentAttempt && <div className="onsite-actions">
            {paymentMethodGroup(p.method) === "TRANSFER" && <button className="cashier-button-outline" onClick={() => void showQr(p)}>Hiện mã QR</button>}
            <button disabled={busy !== null} onClick={() => void confirm(p)}>
              {busy === p.paymentId ? "Đang xử lý…" : paymentMethodGroup(p.method) === "TRANSFER" ? "Đã nhận chuyển khoản" : "Xác nhận đã thu tiền"}</button>
            {!p.splitPart && <button className="danger" disabled={busy !== null} onClick={() => void cancel(p)}>Hủy giao dịch chờ</button>}
          </div>}</td>
        </tr>)}</tbody></table></div>}
    {qrView && <div className="modal-backdrop" onClick={() => setQrView(null)}>
      <div className="modal-card" role="dialog" aria-modal="true" aria-label="Mã QR chuyển khoản" onClick={e => e.stopPropagation()}>
        <div className="cashier-panel-header"><h2>Mã QR chuyển khoản · Bàn #{qrView.payment.tableId}</h2>
          <button className="cashier-button-outline" onClick={() => setQrView(null)}>Đóng</button></div>
        <TransferQrCard qr={qrView.qr} loading={!qrView.qr && !qrView.error} error={qrView.error} />
      </div>
    </div>}
  </section>;
}
