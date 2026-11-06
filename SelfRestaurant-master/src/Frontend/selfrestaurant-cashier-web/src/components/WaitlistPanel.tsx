import { userMessage, cleanText } from "../../../shared/apiError";
import { formatViDateTime } from "../../../shared/dateFormat";
import { sortWaitlist, waitlistStatusLabel, waitlistTone, type WaitlistItem } from "../../../shared/waitlistStatus";
import { useEffect, useState } from "react";
import { cashierApi } from "../lib/api";
import { parseApiUtcTimestamp } from "../lib/dateTime";
import { useControlledRead } from "../lib/useControlledRead";
import { CASHIER_FALLBACK_POLL_MS, CASHIER_OFFLINE_POLL_MS } from "../lib/polling";
import type { TopicBus } from "../../../shared/staffRealtime";

const time = (utc?: string | null) => (utc ? formatViDateTime(parseApiUtcTimestamp(utc)) : "—");

/**
 * "Danh sách chờ" (owner 06/11): guests waiting for a table at this branch. Read-only: the system offers a freed table
 * to the first guest that fits and holds it a few minutes. Reading this panel also lets it offer.
 */
export function WaitlistPanel({ events, live = false, tableLabel }: { events?: TopicBus; live?: boolean; tableLabel: (tableId: number) => string }) {
  const [items, setItems] = useState<WaitlistItem[]>([]);
  const [minutes, setMinutes] = useState(10);
  const [error, setError] = useState("");
  const refresh = useControlledRead({
    read: signal => cashierApi.getWaitlist(signal),
    apply: value => { setItems(sortWaitlist(value.items ?? [])); setMinutes(value.offerMinutes ?? 10); setError(""); },
    error: e => setError(userMessage(e, "Không tải được danh sách chờ.")),
    interval: live ? CASHIER_FALLBACK_POLL_MS : CASHIER_OFFLINE_POLL_MS,
  });
  useEffect(() => events?.on("reservations", () => void refresh()), [events, refresh]);

  const active = items.filter(x => x.status === "Waiting" || x.status === "Offered").length;
  return <section id="reservation-waitlist" className="panel cashier-panel-card" aria-label="Danh sách chờ">
    <div className="cashier-panel-header">
      <div><h2><i className="bi bi-hourglass-split me-2" />Danh sách chờ</h2>
        <p className="muted">Khi có bàn trống, hệ thống giữ bàn cho khách đăng ký sớm nhất phù hợp trong {minutes} phút. Khách không xác nhận kịp thì mời người kế tiếp.</p></div>
      {active > 0 ? <span className="soft-badge info">{active} khách đang chờ</span> : null}
    </div>
    {cleanText(error) && <div className="error-box" role="alert">{cleanText(error)}<button onClick={() => void refresh()}>Thử lại</button></div>}
    {items.length === 0 && !cleanText(error) ? <p className="muted">Chưa có khách nào trong danh sách chờ.</p> : null}
    {items.length > 0 && <div style={{ overflowX: "auto" }}><table className="table">
      <thead><tr><th>Khách</th><th>Số khách</th><th>Khoảng giờ</th><th>Trạng thái</th><th>Ghi chú</th></tr></thead>
      <tbody>{items.map(x => <tr key={x.waitlistEntryId} data-waitlist-id={x.waitlistEntryId}>
        <td><strong>{x.contactName}</strong><br/><small>{x.phoneNumber}</small></td>
        <td>{x.partySize}</td>
        <td>{time(x.windowStartUtc)}<br/><small className="muted">đến {time(x.windowEndUtc)}</small></td>
        <td><span className={`soft-badge ${waitlistTone(x.status)}`}>{waitlistStatusLabel(x.status)}</span>
          {x.status === "Waiting" && x.position > 0 ? <><br/><small>Thứ {x.position}</small></> : null}
          {x.status === "Offered" && x.offeredTableId ? <><br/><small>{tableLabel(x.offeredTableId)} · {time(x.offeredReservedAtUtc)} · giữ đến {time(x.offerExpiresAtUtc)}</small></> : null}</td>
        <td><small>{cleanText(x.note) || cleanText(x.closedReason) || "—"}</small></td>
      </tr>)}</tbody>
    </table></div>}
  </section>;
}
