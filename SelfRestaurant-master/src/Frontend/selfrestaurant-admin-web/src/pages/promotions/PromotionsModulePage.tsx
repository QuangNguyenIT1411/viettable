import { userMessage } from "../../../../shared/apiError";
import { formatViDateTime } from "../../../../shared/dateFormat";
import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { parseVnd } from "../../../../shared/money";
import { AdminLayout } from "../../components/AdminLayout";
import { MoneyInput } from "../../components/MoneyInput";
import { adminApi, type AdminPromotionDto, type AdminPromotionRequest } from "../../lib/api";
import type { BranchDto, StaffSessionUserDto } from "../../lib/types";
import { useAutoDismissMessage } from "../../lib/useAutoDismissMessage";

type Props = { mode: "index" | "create" | "edit"; onLogout: () => Promise<void> };

type Form = {
  name: string; description: string; imageUrl: string; discountType: "PERCENT" | "FIXED"; discountValue: string;
  maxDiscount: string; minSubtotal: string; branchIds: number[]; startsAt: string; endsAt: string; isActive: boolean;
};

/** "2026-10-28T19:00" (local, for <input type="datetime-local">) from an ISO time, and back. */
function toLocalInput(iso: string | null | undefined): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function emptyForm(): Form {
  const start = new Date();
  start.setMinutes(0, 0, 0);
  const end = new Date(start.getTime() + 7 * 24 * 3600 * 1000);
  return { name: "", description: "", imageUrl: "", discountType: "PERCENT", discountValue: "10", maxDiscount: "50000",
    minSubtotal: "0", branchIds: [], startsAt: toLocalInput(start.toISOString()), endsAt: toLocalInput(end.toISOString()), isActive: true };
}

function fromPromotion(p: AdminPromotionDto): Form {
  return { name: p.name, description: p.description ?? "", imageUrl: p.imageUrl ?? "", discountType: p.discountType === "FIXED" ? "FIXED" : "PERCENT",
    discountValue: String(p.discountValue), maxDiscount: p.maxDiscount == null ? "" : String(p.maxDiscount), minSubtotal: String(p.minSubtotal),
    branchIds: p.branchIds ?? [], startsAt: toLocalInput(p.startsAtUtc), endsAt: toLocalInput(p.endsAtUtc), isActive: p.isActive };
}

function toRequest(f: Form): AdminPromotionRequest {
  return {
    name: f.name.trim(), description: f.description.trim() || null, imageUrl: f.imageUrl.trim() || null, discountType: f.discountType,
    discountValue: f.discountType === "FIXED" ? parseVnd(f.discountValue) : Number(f.discountValue || "0"),
    maxDiscount: f.discountType === "PERCENT" && f.maxDiscount.trim() ? parseVnd(f.maxDiscount) : null,
    minSubtotal: parseVnd(f.minSubtotal), branchIds: f.branchIds,
    startsAtUtc: new Date(f.startsAt).toISOString(), endsAtUtc: new Date(f.endsAt).toISOString(), isActive: f.isActive,
  };
}

/**
 * "Khuyến mãi" (docs/PROMO_PLAN.md): the admin creates promotions; the backend applies the best one to a bill that
 * qualifies (never together with a voucher) and tells customers ("Ưu đãi mới") once it runs.
 */
export function PromotionsModulePage({ mode, onLogout }: Props) {
  const navigate = useNavigate();
  const { promotionId } = useParams();
  const [staff, setStaff] = useState<StaffSessionUserDto | null>(null);
  const [items, setItems] = useState<AdminPromotionDto[]>([]);
  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [form, setForm] = useState<Form>(emptyForm);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useAutoDismissMessage(5000);
  const id = promotionId ? Number.parseInt(promotionId, 10) : 0;

  async function loadPage() {
    setLoading(true);
    setError(null);
    try {
      const [dashboard, list] = await Promise.all([adminApi.getDashboard(), adminApi.getPromotions()]);
      setStaff(dashboard.staff);
      setBranches(dashboard.branches);
      setItems(list);
      if (mode === "edit") {
        const found = list.find(x => x.promotionId === id);
        if (!found) { navigate("/Admin/Promotions/Index", { replace: true }); return; }
        setForm(fromPromotion(found));
      } else if (mode === "create") setForm(emptyForm());
    } catch (err) {
      setError(userMessage(err, "Không thể tải danh sách khuyến mãi."));
    } finally { setLoading(false); }
  }

  useEffect(() => { void loadPage(); }, [mode, id]);

  async function save() {
    if (saving) return;
    if (!form.name.trim()) { setError("Vui lòng nhập tên khuyến mãi."); return; }
    if (!form.startsAt || !form.endsAt) { setError("Vui lòng chọn thời gian bắt đầu và kết thúc."); return; }
    const money = toRequest(form);
    if ([money.discountValue, money.maxDiscount ?? 0, money.minSubtotal].some(Number.isNaN)) { setError("Số tiền chỉ được nhập số, ví dụ 300000."); return; }
    setSaving(true);
    setError(null);
    try {
      if (mode === "create") await adminApi.createPromotion(toRequest(form));
      else await adminApi.updatePromotion(id, toRequest(form));
      navigate("/Admin/Promotions/Index", { state: { message: mode === "create" ? "Đã tạo khuyến mãi." : "Đã lưu khuyến mãi." } });
    } catch (err) { setError(userMessage(err, "Không thể lưu khuyến mãi.")); }
    finally { setSaving(false); }
  }

  async function toggle(p: AdminPromotionDto) {
    try {
      await adminApi.togglePromotion(p.promotionId, !p.isActive);
      setMessage(p.isActive ? "Đã tắt khuyến mãi." : "Đã bật khuyến mãi.");
      await loadPage();
    } catch (err) { setError(userMessage(err, "Không thể đổi trạng thái khuyến mãi.")); }
  }

  const branchNames = (ids: number[]) => ids.length === 0 ? "Mọi chi nhánh"
    : ids.map(b => branches.find(x => x.branchId === b)?.name ?? `#${b}`).join(", ");
  const statusPill = (p: AdminPromotionDto) => !p.isActive ? <span className="status-pill danger">Đã tắt</span>
    : p.isRunning ? <span className="status-pill success">Đang chạy</span>
    : new Date(p.endsAtUtc).getTime() < Date.now() ? <span className="status-pill info">Đã hết hạn</span>
    : <span className="status-pill warning">Sắp chạy</span>;
  const title = mode === "create" ? "Thêm khuyến mãi" : mode === "edit" ? "Sửa khuyến mãi" : "Khuyến mãi";
  const description = "Khuyến mãi tự áp vào hóa đơn đủ điều kiện (mỗi hóa đơn 1 khuyến mãi tốt nhất, không cộng dồn với voucher).";

  return (
    <AdminLayout title={title} description={description} staff={staff} onLogout={onLogout} onRefresh={loadPage} message={message} error={error}>
      {loading ? <div className="screen-message">Đang tải khuyến mãi...</div> : null}

      {!loading && mode === "index" ? (
        <section className="panel">
          <div className="toolbar-card">
            <div>
              <strong>Chương trình khuyến mãi</strong>
              <div className="muted">Khi một khuyến mãi bắt đầu chạy, khách nhận thông báo "Ưu đãi mới" một lần.</div>
            </div>
            <button className="ghost" onClick={() => navigate("/Admin/Promotions/Create")}>Thêm mới</button>
          </div>
          <table className="data-table">
            <thead><tr><th>Khuyến mãi</th><th>Mức giảm</th><th>Chi nhánh</th><th>Thời gian</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
            <tbody>
              {items.length > 0 ? items.map(p => (
                <tr key={p.promotionId}>
                  <td><strong>{p.name}</strong>{p.description ? <><br /><small className="muted">{p.description}</small></> : null}</td>
                  <td>{p.summary}</td>
                  <td>{branchNames(p.branchIds)}</td>
                  <td><small>{formatViDateTime(new Date(p.startsAtUtc))}<br />→ {formatViDateTime(new Date(p.endsAtUtc))}</small></td>
                  <td>{statusPill(p)}</td>
                  <td><div className="button-row wrap">
                    <button className="ghost" onClick={() => navigate(`/Admin/Promotions/Edit/${p.promotionId}`)}>Sửa</button>
                    <button className={p.isActive ? "danger" : "ghost"} onClick={() => void toggle(p)}>{p.isActive ? "Tắt" : "Bật"}</button>
                  </div></td>
                </tr>
              )) : <tr><td colSpan={6} className="text-right">Chưa có khuyến mãi nào.</td></tr>}
            </tbody>
          </table>
        </section>
      ) : null}

      {!loading && mode !== "index" ? (
        <section className="panel">
          <div className="toolbar-card">
            <div>
              <strong>{title}</strong>
              <div className="muted">Giảm theo % có thể đặt trần; giảm cố định là số tiền trừ thẳng. Ảnh là đường dẫn https (tùy chọn).</div>
            </div>
            <button className="ghost" onClick={() => navigate("/Admin/Promotions/Index")}>Quay lại danh sách</button>
          </div>
          <div className="entry-form-card">
            <div className="entry-form-grid">
              <label>Tên khuyến mãi<input value={form.name} maxLength={200} onChange={e => setForm({ ...form, name: e.target.value })} /></label>
              <label>Ảnh (https, tùy chọn)<input value={form.imageUrl} maxLength={1000} placeholder="https://..." onChange={e => setForm({ ...form, imageUrl: e.target.value })} /></label>
              <label className="full-span">Mô tả<textarea rows={3} maxLength={1000} value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} /></label>
              <label>Loại giảm
                <select value={form.discountType} onChange={e => setForm({ ...form, discountType: e.target.value === "FIXED" ? "FIXED" : "PERCENT" })}>
                  <option value="PERCENT">Phần trăm (%)</option>
                  <option value="FIXED">Số tiền cố định (đ)</option>
                </select>
              </label>
              {form.discountType === "PERCENT" ? <label>Mức giảm (%)
                <input type="number" min={1} max={100} value={form.discountValue}
                  onChange={e => setForm({ ...form, discountValue: e.target.value })} /></label>
                : <MoneyInput label="Số tiền giảm (đ)" value={form.discountValue} onChange={v => setForm({ ...form, discountValue: v })} />}
              {form.discountType === "PERCENT" ? <MoneyInput label="Trần giảm (đ, để trống = không trần)" value={form.maxDiscount}
                onChange={v => setForm({ ...form, maxDiscount: v })} /> : null}
              <MoneyInput label="Đơn tối thiểu (đ)" value={form.minSubtotal} onChange={v => setForm({ ...form, minSubtotal: v })} />
              <label>Bắt đầu<input type="datetime-local" value={form.startsAt} onChange={e => setForm({ ...form, startsAt: e.target.value })} /></label>
              <label>Kết thúc<input type="datetime-local" value={form.endsAt} onChange={e => setForm({ ...form, endsAt: e.target.value })} /></label>
            </div>
            <div className="toolbar-card" style={{ marginTop: "1rem" }}>
              <div><strong>Chi nhánh áp dụng</strong><div className="muted">Không chọn chi nhánh nào = áp dụng mọi chi nhánh.</div></div>
            </div>
            <div className="filter-chip-row">
              {branches.map(b => {
                const on = form.branchIds.includes(b.branchId);
                return <button key={b.branchId} type="button" className={`ghost ${on ? "active-toggle" : ""}`}
                  onClick={() => setForm({ ...form, branchIds: on ? form.branchIds.filter(x => x !== b.branchId) : [...form.branchIds, b.branchId] })}>
                  {b.name}</button>;
              })}
            </div>
            <div className="filter-chip-row">
              <button type="button" className={`ghost ${form.isActive ? "active-toggle" : ""}`} onClick={() => setForm({ ...form, isActive: !form.isActive })}>
                {form.isActive ? "Đang bật" : "Đang tắt"}</button>
            </div>
            <div className="entry-form-actions">
              <button className="ghost" onClick={() => navigate("/Admin/Promotions/Index")}>Hủy</button>
              <button disabled={saving} onClick={() => void save()}>{saving ? "Đang lưu…" : mode === "create" ? "Thêm mới" : "Lưu thay đổi"}</button>
            </div>
          </div>
        </section>
      ) : null}
    </AdminLayout>
  );
}
