import { userMessage, cleanText } from "../../../shared/apiError";
import { formatViDateTime } from "../../../shared/dateFormat";
import { canMarkRefunded, depositStatusLabel, depositTone, sortDeposits, type DepositListItem } from "../../../shared/depositStatus";
import { useEffect, useRef, useState } from "react";
import { cashierApi } from "../lib/api";
import { parseApiUtcTimestamp } from "../lib/dateTime";
import { useControlledRead } from "../lib/useControlledRead";
import { CASHIER_FALLBACK_POLL_MS, CASHIER_OFFLINE_POLL_MS } from "../lib/polling";
import type { TopicBus } from "../../../shared/staffRealtime";

/**
 * "Đặt cọc": deposits of the branch's bookings (docs/DEPOSIT_PLAN.md). Display only, plus "Đã hoàn cọc" once the
 * cashier refunded a guest by hand. Hidden when there is none (feature off).
 */
export function DepositsPanel({ events, live = false, onCountChange }: { events?: TopicBus; live?: boolean; onCountChange?: (count: number) => void }) {
  const [items, setItems] = useState<DepositListItem[]>([]);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState<number | null>(null);
  const mounted = useRef(false);
  const refresh = useControlledRead({
    read: signal => cashierApi.getDeposits(signal),
    apply: value => { setItems(sortDeposits(value).slice(0, 30)); setError(""); },
    error: e => setError(userMessage(e, "Không tải được danh sách đặt cọc.")),
    interval: live ? CASHIER_FALLBACK_POLL_MS : CASHIER_OFFLINE_POLL_MS,
  });
  useEffect(() => events?.on("reservations", () => void refresh()), [events, refresh]);
  useEffect(() => events?.on("onsitePayments", () => void refresh()), [events, refresh]);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  const todoCount = items.filter(x => canMarkRefunded(x.deposit.status)).length;
  useEffect(() => { onCountChange?.(todoCount); }, [todoCount, onCountChange]);

  const markRefunded = async (item: DepositListItem) => {
    if (busy !== null) return;
    const who = item.reservation ? `${item.reservation.reservationCode} (${item.reservation.customerName})` : `#${item.deposit.reservationId}`;
    if (!window.confirm(`Xác nhận đã hoàn ${item.deposit.amount.toLocaleString("vi-VN")} đ tiền cọc cho đặt bàn ${who}?\n\nChỉ bấm sau khi đã chuyển trả tiền cho khách.`)) return;
    setBusy(item.deposit.reservationDepositId);
    try { await cashierApi.markDepositRefunded(item.deposit.reservationDepositId); await refresh(); }
    catch (e) { if (mounted.current) setError(userMessage(e, "Không thể đánh dấu đã hoàn cọc.")); }
    finally { if (mounted.current) setBusy(null); }
  };

  if (items.length === 0 && !cleanText(error)) return null;
  const todo = items.filter(x => canMarkRefunded(x.deposit.status)).length;
  return <section id="reservation-deposits" className="panel cashier-panel-card" aria-label="Đặt cọc">
    <div className="cashier-panel-header">
      <div><h2><i className="bi bi-piggy-bank me-2" />Đặt cọc</h2>
        <p className="muted">Khách đặt từ 6 người chuyển cọc qua VietQR, hệ thống tự xác nhận. Hệ thống không tự hoàn tiền: "Cần hoàn cọc" thì chuyển trả cho khách rồi bấm "Đã hoàn cọc".</p></div>
      {todo > 0 ? <span className="soft-badge warning">{todo} cần hoàn cọc</span> : null}
    </div>
    {cleanText(error) && <div className="error-box" role="alert">{cleanText(error)}<button onClick={() => void refresh()}>Thử lại</button></div>}
    {items.length > 0 && <div style={{ overflowX: "auto" }}><table className="table">
      <thead><tr><th>Đặt bàn</th><th>Giờ đến</th><th>Số tiền</th><th>Trạng thái</th><th>Ghi chú</th><th>Thao tác</th></tr></thead>
      <tbody>{items.map(({ deposit, reservation }) => <tr key={deposit.reservationDepositId} data-deposit-id={deposit.reservationDepositId}>
        <td>{reservation ? <><strong>{reservation.reservationCode}</strong><br/><small>{reservation.customerName} · {reservation.phoneNumber}</small></>
          : <>#{deposit.reservationId}</>}<br/><small className="muted">{deposit.partySize} khách · ND: {deposit.transferContent}</small></td>
        <td>{formatViDateTime(parseApiUtcTimestamp(reservation?.reservedAt ?? deposit.reservedAtUtc))}</td>
        <td>{deposit.amount.toLocaleString("vi-VN")} đ{deposit.paidAmount != null && deposit.paidAmount !== deposit.amount
          ? <><br/><small className="muted">Đã nhận {deposit.paidAmount.toLocaleString("vi-VN")} đ</small></> : null}</td>
        <td><span className={`soft-badge ${depositTone(deposit.status) === "neutral" ? "secondary" : depositTone(deposit.status)}`}>{depositStatusLabel(deposit.status)}</span></td>
        <td><small>{cleanText(deposit.closedReason) || "—"}</small></td>
        <td>{canMarkRefunded(deposit.status)
          ? <button className="cashier-button-outline" disabled={busy !== null} onClick={() => void markRefunded({ deposit, reservation })}>
              {busy === deposit.reservationDepositId ? "Đang lưu…" : "Đã hoàn cọc"}</button>
          : "—"}</td>
      </tr>)}</tbody>
    </table></div>}
  </section>;
}
