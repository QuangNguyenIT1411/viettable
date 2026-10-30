import { userMessage, cleanText } from "../../../shared/apiError";
import { formatViDateTime } from "../../../shared/dateFormat";
import { useEffect, useRef, useState } from "react";
import { cashierApi } from "../lib/api";
import { parseApiUtcTimestamp } from "../lib/dateTime";
import { useControlledRead } from "../lib/useControlledRead";
import { CASHIER_FALLBACK_POLL_MS, CASHIER_OFFLINE_POLL_MS } from "../lib/polling";
import type { TopicBus } from "../../../shared/staffRealtime";
import type { ReservationDto } from "../lib/types";

/** Quick reasons shown to the guest ("Đặt bàn bị từ chối: <lý do>"). */
export const rejectReasons = ["Hết bàn", "Ngoài giờ phục vụ", "Nhà hàng có sự kiện riêng"] as const;

/** The reserved time has come (the guest may arrive, or be marked absent). */
export function reservationIsDue(reservation: ReservationDto, now = Date.now()) {
  const at = parseApiUtcTimestamp(reservation.reservedAt);
  return !!at && at.getTime() <= now;
}

/**
 * Xác nhận / Từ chối (pending bookings) and "Khách không đến" (pending or confirmed, from the reserved time).
 * Used by the pending panel and by the day list; onChanged reloads the lists.
 */
export function ReservationActions({ reservation, onChanged }: { reservation: ReservationDto; onChanged: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [rejecting, setRejecting] = useState(false);
  const [reason, setReason] = useState<string>(rejectReasons[0]);
  const [other, setOther] = useState("");
  const mounted = useRef(false);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);

  const pending = reservation.status === "Pending";
  const canNoShow = (pending || reservation.status === "Confirmed") && reservationIsDue(reservation);
  if (!pending && !canNoShow) return null;

  const run = async (action: () => Promise<unknown>, failure: string) => {
    if (busy) return;
    setBusy(true);
    setError("");
    try { await action(); setRejecting(false); onChanged(); }
    catch (e) { if (mounted.current) setError(userMessage(e, failure)); }
    finally { if (mounted.current) setBusy(false); }
  };
  const label = `${reservation.reservationCode} (${reservation.customerName})`;
  const confirmBooking = () => void run(() => cashierApi.confirmReservation(reservation.reservationId), "Không thể xác nhận đặt bàn.");
  const finalReason = reason === "OTHER" ? other.trim() : reason;
  const reject = () => {
    if (!finalReason) { setError("Vui lòng nhập lý do từ chối."); return; }
    void run(() => cashierApi.rejectReservation(reservation.reservationId, finalReason), "Không thể từ chối đặt bàn.");
  };
  const noShow = () => {
    if (!window.confirm(`Đánh dấu khách không đến cho đặt bàn ${label}?\n\nBàn sẽ được nhả và món đặt trước (nếu có) bị hủy.`)) return;
    void run(() => cashierApi.markReservationNoShow(reservation.reservationId), "Không thể đánh dấu khách không đến.");
  };

  return <div className="reservation-decision" data-reservation-actions={reservation.reservationId}>
    <div className="reservation-decision-buttons">
      {pending ? <button type="button" className="cashier-button-primary" disabled={busy} onClick={confirmBooking}>
        <i className="bi bi-check2-circle me-1" />{busy && !rejecting ? "Đang lưu…" : "Xác nhận"}</button> : null}
      {pending ? <button type="button" className="cashier-button-outline" disabled={busy} onClick={() => { setRejecting(true); setError(""); }}>
        <i className="bi bi-x-circle me-1" />Từ chối</button> : null}
      {canNoShow ? <button type="button" className="cashier-button-outline" disabled={busy} onClick={noShow}>
        <i className="bi bi-person-x me-1" />Khách không đến</button> : null}
    </div>
    {rejecting ? <div className="reservation-reject-box" role="group" aria-label={`Lý do từ chối ${reservation.reservationCode}`}>
      <strong>Lý do từ chối (khách sẽ thấy)</strong>
      <div className="reservation-reject-reasons">
        {[...rejectReasons, "OTHER"].map(value => <label key={value}>
          <input type="radio" name={`reject-${reservation.reservationId}`} value={value} checked={reason === value}
            onChange={() => setReason(value)} disabled={busy} />
          {value === "OTHER" ? "Khác…" : value}
        </label>)}
      </div>
      {reason === "OTHER" ? <input type="text" maxLength={300} value={other} placeholder="Nhập lý do" disabled={busy}
        onChange={event => setOther(event.target.value)} aria-label="Lý do khác" /> : null}
      <div className="reservation-decision-buttons">
        <button type="button" className="danger" disabled={busy} onClick={reject}>
          {busy ? "Đang lưu…" : "Từ chối đặt bàn"}</button>
        <button type="button" className="ghost" disabled={busy} onClick={() => setRejecting(false)}>Quay lại</button>
      </div>
    </div> : null}
    {cleanText(error) ? <div className="error-box" role="alert">{cleanText(error)}</div> : null}
  </div>;
}

/**
 * "Đặt bàn chờ xác nhận": every pending booking of the branch, today and the coming days, soonest first.
 * A booking whose time has come is highlighted (the guest may already be at the door). Hidden when empty.
 */
export function PendingReservationsPanel({ events, live = false, tableLabel, onChanged, onCheckIn, onCountChange }: {
  events?: TopicBus;
  live?: boolean;
  onCountChange?: (count: number) => void;
  tableLabel: (reservation: ReservationDto) => string;
  onChanged: () => void;
  onCheckIn: (reservation: ReservationDto) => void;
}) {
  const [items, setItems] = useState<ReservationDto[]>([]);
  const [error, setError] = useState("");
  const refresh = useControlledRead({
    read: signal => cashierApi.getPendingReservations(signal),
    apply: value => { setItems(value); setError(""); },
    error: e => setError(userMessage(e, "Không tải được đặt bàn chờ xác nhận.")),
    interval: live ? CASHIER_FALLBACK_POLL_MS : CASHIER_OFFLINE_POLL_MS,
  });
  useEffect(() => events?.on("reservations", () => void refresh()), [events, refresh]);
  useEffect(() => { onCountChange?.(items.length); }, [items.length, onCountChange]);

  if (items.length === 0 && !cleanText(error)) return null;
  const changed = () => { void refresh(); onChanged(); };
  return <section id="pending-reservations" className="panel cashier-panel-card" aria-label="Đặt bàn chờ xác nhận">
    <div className="cashier-panel-header">
      <div><h2><i className="bi bi-hourglass-split me-2" />Đặt bàn chờ xác nhận</h2>
        <p className="muted">Gồm cả các ngày tới. Bàn đang được giữ cho khách; từ chối thì bàn được nhả và khách nhận thông báo kèm lý do.</p></div>
      {items.length > 0 ? <span className="soft-badge warning">{items.length} chờ xác nhận</span> : null}
    </div>
    {cleanText(error) && <div className="error-box" role="alert">{cleanText(error)}<button onClick={() => void refresh()}>Thử lại</button></div>}
    {items.length > 0 && <div className="cashier-reservation-grid">
      {items.map(r => {
        const due = reservationIsDue(r);
        return <article key={r.reservationId} data-pending-reservation={r.reservationId}
          className={`cashier-reservation-card${due ? " reservation-due" : ""}`}>
          <div className="reservation-card-topline">
            <strong>{r.reservationCode}</strong>
            <span className={due ? "soft-badge danger" : "soft-badge warning"}>{due ? "Đã tới giờ hẹn" : "Chờ xác nhận"}</span>
          </div>
          <div className="reservation-main-info">
            <div><span>Khách hàng</span><strong>{r.customerName}</strong></div>
            <div><span>Điện thoại</span><strong>{r.phoneNumber}</strong></div>
            <div><span>Số khách</span><strong>{r.partySize} khách</strong></div>
            <div><span>Thời gian</span><strong>{formatViDateTime(parseApiUtcTimestamp(r.reservedAt))}</strong></div>
          </div>
          <div className="reservation-meta-row"><span><i className="bi bi-table me-1" />{tableLabel(r)}</span></div>
          {r.note ? <p className="reservation-note">Ghi chú: {r.note}</p> : null}
          <ReservationActions reservation={r} onChanged={changed} />
          {due ? <button type="button" className="cashier-link-button cashier-link-button-outline" onClick={() => onCheckIn(r)}>
            <i className="bi bi-person-check me-1" />Nhận khách (tự xác nhận)</button> : null}
        </article>;
      })}
    </div>}
  </section>;
}
