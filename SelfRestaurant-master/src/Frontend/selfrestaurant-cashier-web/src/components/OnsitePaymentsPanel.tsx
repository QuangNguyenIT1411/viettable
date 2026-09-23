import { useEffect, useRef, useState } from "react";
import { cashierApi, type OnsitePayment } from "../lib/api";

const labels: Record<string, string> = { PENDING: "Chờ thu tiền", SUCCEEDED: "Đã thu tiền", FAILED: "Thất bại", CANCELLED: "Đã hủy", EXPIRED: "Hết hạn" };

export function OnsitePaymentsPanel() {
  const [payments, setPayments] = useState<OnsitePayment[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<number | null>(null);
  const submitting = useRef(false);
  const mounted = useRef(false);
  const refresh = async () => {
    try { const result = await cashierApi.getOnsitePayments(); if (mounted.current) { setPayments(result); setError(""); } }
    catch (e) { if (mounted.current) setError(e instanceof Error ? e.message : "Không tải được giao dịch."); }
    finally { if (mounted.current) setLoading(false); }
  };
  useEffect(() => {
    mounted.current = true;
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout>;
    const poll = async () => { await refresh(); if (!cancelled) timer = setTimeout(poll, 5000); };
    void poll();
    return () => { cancelled = true; mounted.current = false; clearTimeout(timer); };
  }, []);
  const confirm = async (payment: OnsitePayment) => {
    if (submitting.current) return;
    if (!window.confirm(`Xác nhận đã thực nhận ${payment.amount.toLocaleString("vi-VN")} đ cho ${payment.paymentCode}${payment.splitPart ? ` — ${payment.splitPart}` : ""}?`)) return;
    submitting.current = true; setBusy(payment.paymentId);
    try {
      await cashierApi.confirmOnsitePayment(payment.paymentId);
      await refresh();
    } catch (e) { if (mounted.current) setError(e instanceof Error ? e.message : "Không thể xác nhận giao dịch."); }
    finally { submitting.current = false; if (mounted.current) setBusy(null); }
  };
  return <section className="panel cashier-panel-card" aria-label="Thanh toán ứng dụng tại chỗ">
    <h2>Thanh toán ứng dụng tại chỗ</h2>
    <p className="muted">Chỉ xác nhận sau khi thực nhận tiền. Giao dịch ứng dụng được xử lý tại đây, không thu lại qua hóa đơn legacy.</p>
    {error && <div className="error-box" role="alert">{error}<button onClick={() => void refresh()}>Thử lại</button></div>}
    {loading ? <p role="status">Đang tải giao dịch…</p> : payments.length === 0 ? <p>Chưa có giao dịch tại chỗ.</p> :
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
