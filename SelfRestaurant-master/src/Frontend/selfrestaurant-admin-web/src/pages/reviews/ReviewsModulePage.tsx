import { userMessage } from "../../../../shared/apiError";
import { formatViDateTime } from "../../../../shared/dateFormat";
import { useEffect, useState } from "react";
import { AdminLayout } from "../../components/AdminLayout";
import { adminApi, type AdminReviewDto, type AdminReviewStatsDto } from "../../lib/api";
import type { BranchDto, StaffSessionUserDto } from "../../lib/types";
import { useAutoDismissMessage } from "../../lib/useAutoDismissMessage";

type Props = { onLogout: () => Promise<void> };

const stars = (n: number) => "★".repeat(n) + "☆".repeat(Math.max(0, 5 - n));
const avg = (n: number | null | undefined) => (n == null ? "—" : String(n).replace(".", ","));

/**
 * "Đánh giá" (owner 05/11/2026): every review with its criteria, dish stars and tags; the restaurant answers (the
 * guest sees the answer in the app) or hides a rule-breaking review with a reason (kept with who hid it and when).
 * Averages per branch and per dish count visible reviews only.
 */
export function ReviewsModulePage({ onLogout }: Props) {
  const [staff, setStaff] = useState<StaffSessionUserDto | null>(null);
  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [items, setItems] = useState<AdminReviewDto[]>([]);
  const [stats, setStats] = useState<AdminReviewStatsDto | null>(null);
  const [branchId, setBranchId] = useState("");
  const [starFilter, setStarFilter] = useState("");
  const [visibility, setVisibility] = useState("");
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(0);
  const [replies, setReplies] = useState<Record<number, string>>({});
  const [reasons, setReasons] = useState<Record<number, string>>({});
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useAutoDismissMessage(5000);

  async function loadPage() {
    setLoading(true);
    setError(null);
    try {
      const [dashboard, list, summary] = await Promise.all([
        adminApi.getDashboard(),
        adminApi.getReviews({ branchId: branchId ? Number(branchId) : undefined, stars: starFilter ? Number(starFilter) : undefined, visibility: visibility || undefined, page }),
        adminApi.getReviewStats(),
      ]);
      setStaff(dashboard.staff);
      setBranches(dashboard.branches);
      setItems(list.items);
      setTotalPages(list.totalPages);
      setStats(summary);
      const draft: Record<number, string> = {};
      list.items.forEach(x => { draft[x.reviewId] = x.replyText ?? ""; });
      setReplies(draft);
    } catch (err) {
      setError(userMessage(err, "Không thể tải đánh giá."));
    } finally { setLoading(false); }
  }

  useEffect(() => { void loadPage(); }, [branchId, starFilter, visibility, page]);

  const branchName = (id?: number | null) => branches.find(b => b.branchId === id)?.name ?? (id ? `Chi nhánh #${id}` : "Không rõ chi nhánh");

  async function reply(r: AdminReviewDto) {
    setBusyId(r.reviewId);
    setError(null);
    try {
      await adminApi.replyReview(r.reviewId, replies[r.reviewId] ?? "");
      setMessage((replies[r.reviewId] ?? "").trim() ? "Đã gửi câu trả lời. Khách sẽ thấy trong ứng dụng." : "Đã gỡ câu trả lời.");
      await loadPage();
    } catch (err) { setError(userMessage(err, "Không thể trả lời đánh giá.")); }
    finally { setBusyId(null); }
  }

  async function setHidden(r: AdminReviewDto, hidden: boolean) {
    const reason = (reasons[r.reviewId] ?? "").trim();
    if (hidden && !reason) { setError("Ghi lý do ẩn đánh giá (ví dụ: ngôn từ không phù hợp, quảng cáo)."); return; }
    setBusyId(r.reviewId);
    setError(null);
    try {
      await adminApi.setReviewVisibility(r.reviewId, hidden, hidden ? reason : null);
      setMessage(hidden ? "Đã ẩn đánh giá." : "Đã hiện lại đánh giá.");
      await loadPage();
    } catch (err) { setError(userMessage(err, "Không thể đổi trạng thái đánh giá.")); }
    finally { setBusyId(null); }
  }

  return (
    <AdminLayout title="Đánh giá" description="Đánh giá của khách sau khi thanh toán. Trả lời để khách thấy; ẩn đánh giá vi phạm kèm lý do."
      staff={staff} onLogout={onLogout} onRefresh={loadPage} message={message} error={error}>
      {stats ? (
        <section className="panel">
          <div className="toolbar-card">
            <div>
              <strong>Trung bình {avg(stats.overall)} / 5</strong>
              <div className="muted">{stats.total} đánh giá đang hiện (không tính đánh giá đã ẩn).</div>
            </div>
          </div>
          <table className="data-table">
            <thead><tr><th>Chi nhánh</th><th>Lượt</th><th>Tổng thể</th><th>Món ăn</th><th>Phục vụ</th><th>Không gian</th></tr></thead>
            <tbody>
              {stats.branches.length > 0 ? stats.branches.map(b => (
                <tr key={String(b.branchId)}><td>{branchName(b.branchId)}</td><td>{b.count}</td><td>{avg(b.overall)}</td><td>{avg(b.food)}</td><td>{avg(b.service)}</td><td>{avg(b.space)}</td></tr>
              )) : <tr><td colSpan={6} className="text-right">Chưa có đánh giá.</td></tr>}
            </tbody>
          </table>
          {stats.dishes.length > 0 ? (
            <table className="data-table">
              <thead><tr><th>Món</th><th>Lượt chấm</th><th>Trung bình</th></tr></thead>
              <tbody>{stats.dishes.slice(0, 15).map(d => <tr key={d.dishId}><td>{d.dishName}</td><td>{d.count}</td><td>{avg(d.average)}</td></tr>)}</tbody>
            </table>
          ) : null}
        </section>
      ) : null}

      <section className="panel">
        <div className="admin-filter-bar">
          <label className="admin-filter-field"><span>Chi nhánh</span>
            <select value={branchId} onChange={e => { setPage(1); setBranchId(e.target.value); }}>
              <option value="">Tất cả</option>{branches.map(b => <option key={b.branchId} value={b.branchId}>{b.name}</option>)}
            </select></label>
          <label className="admin-filter-field"><span>Số sao</span>
            <select value={starFilter} onChange={e => { setPage(1); setStarFilter(e.target.value); }}>
              <option value="">Tất cả</option>{[5, 4, 3, 2, 1].map(n => <option key={n} value={n}>{n} sao</option>)}
            </select></label>
          <label className="admin-filter-field"><span>Trạng thái</span>
            <select value={visibility} onChange={e => { setPage(1); setVisibility(e.target.value); }}>
              <option value="">Tất cả</option><option value="unanswered">Chưa trả lời</option><option value="visible">Đang hiện</option><option value="hidden">Đã ẩn</option>
            </select></label>
        </div>
        {loading ? <div className="screen-message">Đang tải đánh giá...</div> : items.length === 0 ? <div className="screen-message">Không có đánh giá khớp bộ lọc.</div> : (
          <div className="stack">
            {items.map(r => (
              <article key={r.reviewId} className="entry-form-card">
                <div className="toolbar-card">
                  <div>
                    <strong>{stars(r.overallRating)} · {r.isAnonymous ? `${r.authorName ?? "Khách"} (ẩn danh với khách khác)` : r.authorName ?? "Khách"}</strong>
                    <div className="muted">{branchName(r.branchId)} · Hóa đơn #{r.invoiceId} · {formatViDateTime(new Date(r.createdAtUtc))}</div>
                    <div className="muted">Món ăn {r.foodRating}★ · Phục vụ {r.serviceRating}★ · Không gian {r.spaceRating}★</div>
                  </div>
                  {r.isHidden ? <span className="status-pill danger">Đã ẩn</span> : <span className="status-pill success">Đang hiện</span>}
                </div>
                {r.tags.length > 0 ? <p>{r.tags.map(t => <span key={t} className="status-pill info" style={{ marginRight: 6 }}>{t}</span>)}</p> : null}
                {r.comment ? <p>{r.comment}</p> : <p className="muted">(Không có nhận xét)</p>}
                {r.dishRatings.length > 0 ? <p className="muted">{r.dishRatings.map(d => `${d.dishName} ${d.rating}★`).join(" · ")}</p> : null}
                {r.isHidden ? <p className="muted">Lý do ẩn: {r.hiddenReason}{r.hiddenByName ? ` — ${r.hiddenByName}` : ""}{r.hiddenAtUtc ? `, ${formatViDateTime(new Date(r.hiddenAtUtc))}` : ""}</p> : null}
                <label>Trả lời của nhà hàng
                  <textarea value={replies[r.reviewId] ?? ""} maxLength={1000} rows={2}
                    onChange={e => setReplies({ ...replies, [r.reviewId]: e.target.value })} />
                </label>
                {r.repliedAtUtc ? <small className="muted">Đã trả lời {formatViDateTime(new Date(r.repliedAtUtc))}{r.repliedByName ? ` · ${r.repliedByName}` : ""}</small> : null}
                <div className="button-row wrap">
                  <button className="ghost" disabled={busyId === r.reviewId} onClick={() => void reply(r)}>{r.replyText ? "Lưu câu trả lời" : "Gửi câu trả lời"}</button>
                  {r.isHidden ? (
                    <button className="ghost" disabled={busyId === r.reviewId} onClick={() => void setHidden(r, false)}>Hiện lại</button>
                  ) : (
                    <>
                      <input placeholder="Lý do ẩn (bắt buộc)" value={reasons[r.reviewId] ?? ""} maxLength={300}
                        onChange={e => setReasons({ ...reasons, [r.reviewId]: e.target.value })} />
                      <button className="danger" disabled={busyId === r.reviewId} onClick={() => void setHidden(r, true)}>Ẩn đánh giá</button>
                    </>
                  )}
                </div>
              </article>
            ))}
          </div>
        )}
        {totalPages > 1 ? (
          <div className="button-row">
            <button className="ghost" disabled={page <= 1} onClick={() => setPage(page - 1)}>Trang trước</button>
            <span className="muted">Trang {page} / {totalPages}</span>
            <button className="ghost" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>Trang sau</button>
          </div>
        ) : null}
      </section>
    </AdminLayout>
  );
}
