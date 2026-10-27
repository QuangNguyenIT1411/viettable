import { useCallback, useEffect, useRef, useState } from "react";
import { formatViDateTime } from "../../../shared/dateFormat";
import { canMarkRefunded, depositStatusLabel, depositTone, sortDeposits, type DepositListItem } from "../../../shared/depositStatus";
import { adminApi } from "../lib/api";

/**
 * Reservation deposits of the admin's branch (docs/DEPOSIT_PLAN.md): display only, plus "Đã hoàn cọc" after a refund
 * by hand. Hidden when there is none (feature off).
 */
export function AdminDepositsPanel({ branchId }: { branchId?: number | null }) {
  const [items, setItems] = useState<DepositListItem[]>([]);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState<number | null>(null);
  const mounted = useRef(false);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);

  const load = useCallback(async () => {
    try {
      const value = await adminApi.getDeposits(branchId);
      if (mounted.current) { setItems(sortDeposits(value).slice(0, 50)); setError(""); }
    } catch (e) { if (mounted.current) setError(e instanceof Error ? e.message : "Không tải được danh sách đặt cọc."); }
  }, [branchId]);
  useEffect(() => { void load(); }, [load]);

  const markRefunded = async (item: DepositListItem) => {
    if (busy !== null) return;
    const who = item.reservation ? `${item.reservation.reservationCode} (${item.reservation.customerName})` : `#${item.deposit.reservationId}`;
    if (!window.confirm(`Xác nhận đã hoàn ${item.deposit.amount.toLocaleString("vi-VN")} đ tiền cọc cho đặt bàn ${who}?`)) return;
    setBusy(item.deposit.reservationDepositId);
    try { await adminApi.markDepositRefunded(item.deposit.reservationDepositId); await load(); }
    catch (e) { if (mounted.current) setError(e instanceof Error ? e.message : "Không thể đánh dấu đã hoàn cọc."); }
    finally { if (mounted.current) setBusy(null); }
  };

  if (items.length === 0 && !error) return null;
  const todo = items.filter(x => canMarkRefunded(x.deposit.status)).length;
  return <section className="panel" aria-label="Đặt cọc" style={{ marginBottom: "1rem" }}>
    <div className="panel-head">
      <h2>Đặt cọc</h2>
      <span>{todo > 0 ? `${todo} cần hoàn cọc` : "Không có khoản cần hoàn"}</span>
    </div>
    <p className="muted">Hệ thống không tự hoàn tiền. "Cần hoàn cọc": chuyển trả cho khách rồi bấm "Đã hoàn cọc".</p>
    {error ? <div className="error-box" role="alert">{error} <button type="button" onClick={() => void load()}>Thử lại</button></div> : null}
    {items.length > 0 ? <div style={{ overflowX: "auto" }}><table className="table">
      <thead><tr><th>Đặt bàn</th><th>Giờ đến</th><th>Số tiền</th><th>Trạng thái</th><th>Ghi chú</th><th>Thao tác</th></tr></thead>
      <tbody>{items.map(({ deposit, reservation }) => <tr key={deposit.reservationDepositId}>
        <td>{reservation ? <><strong>{reservation.reservationCode}</strong><br/><small>{reservation.customerName} · {reservation.phoneNumber}</small></>
          : <>#{deposit.reservationId}</>}<br/><small className="muted">Chi nhánh #{deposit.branchId} · {deposit.partySize} khách</small></td>
        <td>{formatViDateTime(new Date(reservation?.reservedAt ?? deposit.reservedAtUtc))}</td>
        <td>{deposit.amount.toLocaleString("vi-VN")} đ</td>
        <td><span className={`status-pill ${depositTone(deposit.status) === "neutral" ? "info" : depositTone(deposit.status)}`}>{depositStatusLabel(deposit.status)}</span></td>
        <td><small>{deposit.closedReason || "—"}</small></td>
        <td>{canMarkRefunded(deposit.status)
          ? <button type="button" disabled={busy !== null} onClick={() => void markRefunded({ deposit, reservation })}>
              {busy === deposit.reservationDepositId ? "Đang lưu…" : "Đã hoàn cọc"}</button>
          : "—"}</td>
      </tr>)}</tbody>
    </table></div> : null}
  </section>;
}
