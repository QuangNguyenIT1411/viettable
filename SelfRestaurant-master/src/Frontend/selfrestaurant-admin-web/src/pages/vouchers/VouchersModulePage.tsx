import { userMessage } from "../../../../shared/apiError";
import { formatViDateTime } from "../../../../shared/dateFormat";
import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { parseVnd } from "../../../../shared/money";
import { AdminLayout } from "../../components/AdminLayout";
import { MoneyInput } from "../../components/MoneyInput";
import { adminApi, type AdminVoucherDto, type AdminVoucherRequest } from "../../lib/api";
import type { StaffSessionUserDto } from "../../lib/types";
import { useAutoDismissMessage } from "../../lib/useAutoDismissMessage";

type Props = { mode: "index" | "create" | "edit"; onLogout: () => Promise<void> };

/** "once" = 1 lần, "many" = N lần, "unlimited" = không giới hạn (per customer). */
type PerCustomer = "once" | "many" | "unlimited";

type Form = {
  code: string; name: string; discountType: "PERCENT" | "FIXED"; discountValue: string; maxDiscount: string; minSubtotal: string;
  startsAt: string; endsAt: string; perCustomer: PerCustomer; perCustomerCount: string; totalUses: string; isActive: boolean;
};

/** "2026-10-28T19:00" (local, for <input type="datetime-local">) from an ISO time. */
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
  const end = new Date(start.getTime() + 30 * 24 * 3600 * 1000);
  return { code: "", name: "", discountType: "FIXED", discountValue: "50000", maxDiscount: "", minSubtotal: "0",
    startsAt: toLocalInput(start.toISOString()), endsAt: toLocalInput(end.toISOString()), perCustomer: "once", perCustomerCount: "3",
    totalUses: "", isActive: true };
}

function fromVoucher(v: AdminVoucherDto): Form {
  const per = v.maxUsesPerCustomer ?? null;
  return { code: v.code, name: v.name, discountType: v.discountType === "PERCENT" ? "PERCENT" : "FIXED", discountValue: String(v.discountValue),
    maxDiscount: v.maxDiscount == null ? "" : String(v.maxDiscount), minSubtotal: String(v.minSubtotal),
    startsAt: toLocalInput(v.startsAtUtc), endsAt: toLocalInput(v.endsAtUtc),
    perCustomer: per == null ? "unlimited" : per === 1 ? "once" : "many", perCustomerCount: per != null && per > 1 ? String(per) : "3",
    totalUses: v.maxTotalUses == null ? "" : String(v.maxTotalUses), isActive: v.isActive };
}

function toRequest(f: Form): AdminVoucherRequest {
  return {
    code: f.code.trim().toUpperCase(), name: f.name.trim(), discountType: f.discountType, discountValue: f.discountType === "FIXED" ? parseVnd(f.discountValue) : Number(f.discountValue || "0"),
    maxDiscount: f.discountType === "PERCENT" && f.maxDiscount.trim() ? parseVnd(f.maxDiscount) : null, minSubtotal: parseVnd(f.minSubtotal),
    startsAtUtc: new Date(f.startsAt).toISOString(), endsAtUtc: new Date(f.endsAt).toISOString(),
    maxUsesPerCustomer: f.perCustomer === "once" ? 1 : f.perCustomer === "many" ? Number(f.perCustomerCount || "0") : null,
    maxTotalUses: f.totalUses.trim() ? Number(f.totalUses) : null, isActive: f.isActive,
  };
}

const perCustomerText = (v: AdminVoucherDto) =>
  v.maxUsesPerCustomer == null ? "Không giới hạn" : v.maxUsesPerCustomer === 1 ? "1 lần" : `${v.maxUsesPerCustomer} lần`;

/**
 * "Voucher" (owner 28/10/2026): the admin creates codes the guest picks on the bill (never applied by themselves).
 * Uses: per customer 1 / N / no limit, and an optional total. A use is a bill paid or waiting for the money.
 */
export function VouchersModulePage({ mode, onLogout }: Props) {
  const navigate = useNavigate();
  const { voucherId } = useParams();
  const [staff, setStaff] = useState<StaffSessionUserDto | null>(null);
  const [items, setItems] = useState<AdminVoucherDto[]>([]);
  const [form, setForm] = useState<Form>(emptyForm);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useAutoDismissMessage(5000);
  const id = voucherId ? Number.parseInt(voucherId, 10) : 0;

  async function loadPage() {
    setLoading(true);
    setError(null);
    try {
      const [dashboard, list] = await Promise.all([adminApi.getDashboard(), adminApi.getVouchers()]);
      setStaff(dashboard.staff);
      setItems(list);
      if (mode === "edit") {
        const found = list.find(x => x.voucherId === id);
        if (!found) { navigate("/Admin/Vouchers/Index", { replace: true }); return; }
        setForm(fromVoucher(found));
      } else if (mode === "create") setForm(emptyForm());
    } catch (err) {
      setError(userMessage(err, "Không thể tải danh sách voucher."));
    } finally { setLoading(false); }
  }

  useEffect(() => { void loadPage(); }, [mode, id]);

  async function save() {
    if (saving) return;
    if (!form.code.trim() || !form.name.trim()) { setError("Vui lòng nhập mã và tên voucher."); return; }
    if (!form.startsAt || !form.endsAt) { setError("Vui lòng chọn thời gian bắt đầu và kết thúc."); return; }
    if (form.perCustomer === "many" && !(Number(form.perCustomerCount) >= 2)) { setError("Số lần mỗi khách phải từ 2 trở lên (hoặc chọn 1 lần)."); return; }
    const money = toRequest(form);
    if ([money.discountValue, money.maxDiscount ?? 0, money.minSubtotal].some(Number.isNaN)) { setError("Số tiền chỉ được nhập số, ví dụ 300000."); return; }
    setSaving(true);
    setError(null);
    try {
      if (mode === "create") await adminApi.createVoucher(toRequest(form));
      else await adminApi.updateVoucher(id, toRequest(form));
      navigate("/Admin/Vouchers/Index", { state: { message: mode === "create" ? "Đã tạo voucher." : "Đã lưu voucher." } });
    } catch (err) { setError(userMessage(err, "Không thể lưu voucher.")); }
    finally { setSaving(false); }
  }

  async function toggle(v: AdminVoucherDto) {
    try {
      await adminApi.toggleVoucher(v.voucherId, !v.isActive);
      setMessage(v.isActive ? "Đã tắt voucher." : "Đã bật voucher.");
      await loadPage();
    } catch (err) { setError(userMessage(err, "Không thể đổi trạng thái voucher.")); }
  }

  const statusPill = (v: AdminVoucherDto) => !v.isActive ? <span className="status-pill danger">Đã tắt</span>
    : v.remainingTotal === 0 ? <span className="status-pill info">Hết lượt</span>
    : v.isRunning ? <span className="status-pill success">Đang chạy</span>
    : new Date(v.endsAtUtc).getTime() < Date.now() ? <span className="status-pill info">Đã hết hạn</span>
    : <span className="status-pill warning">Sắp chạy</span>;
  const title = mode === "create" ? "Thêm voucher" : mode === "edit" ? "Sửa voucher" : "Voucher";
  const description = "Voucher chỉ áp khi khách tự chọn trên hóa đơn; nếu cùng có khuyến mãi thì chỉ áp cái lợi hơn cho khách.";

  return (
    <AdminLayout title={title} description={description} staff={staff} onLogout={onLogout} onRefresh={loadPage} message={message} error={error}>
      {loading ? <div className="screen-message">Đang tải voucher...</div> : null}

      {!loading && mode === "index" ? (
        <section className="panel">
          <div className="toolbar-card">
            <div>
              <strong>Danh sách voucher</strong>
              <div className="muted">Một lượt dùng = một hóa đơn đã thanh toán (hoặc đang chờ thanh toán); khách hủy yêu cầu thanh toán thì được trả lại lượt.</div>
            </div>
            <button className="ghost" onClick={() => navigate("/Admin/Vouchers/Create")}>Thêm mới</button>
          </div>
          <table className="data-table">
            <thead><tr><th>Voucher</th><th>Mức giảm</th><th>Mỗi khách</th><th>Đã dùng / tổng</th><th>Thời gian</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
            <tbody>
              {items.length > 0 ? items.map(v => (
                <tr key={v.voucherId}>
                  <td><strong>{v.code}</strong><br /><small className="muted">{v.name}</small></td>
                  <td>{v.summary}</td>
                  <td>{perCustomerText(v)}</td>
                  <td>{v.usedTotal} / {v.maxTotalUses ?? "∞"}</td>
                  <td><small>{formatViDateTime(new Date(v.startsAtUtc))}<br />→ {formatViDateTime(new Date(v.endsAtUtc))}</small></td>
                  <td>{statusPill(v)}</td>
                  <td><div className="button-row wrap">
                    <button className="ghost" onClick={() => navigate(`/Admin/Vouchers/Edit/${v.voucherId}`)}>Sửa</button>
                    <button className={v.isActive ? "danger" : "ghost"} onClick={() => void toggle(v)}>{v.isActive ? "Tắt" : "Bật"}</button>
                  </div></td>
                </tr>
              )) : <tr><td colSpan={7} className="text-right">Chưa có voucher nào.</td></tr>}
            </tbody>
          </table>
        </section>
      ) : null}

      {!loading && mode !== "index" ? (
        <section className="panel">
          <div className="toolbar-card">
            <div>
              <strong>{title}</strong>
              <div className="muted">Mã gồm chữ không dấu, số, gạch ngang (sẽ viết hoa). Giảm theo % có thể đặt trần.</div>
            </div>
            <button className="ghost" onClick={() => navigate("/Admin/Vouchers/Index")}>Quay lại danh sách</button>
          </div>
          <div className="entry-form-card">
            <div className="entry-form-grid">
              <label>Mã voucher<input value={form.code} maxLength={50} placeholder="VD: CHAOBAN50" onChange={e => setForm({ ...form, code: e.target.value.toUpperCase() })} /></label>
              <label>Tên voucher<input value={form.name} maxLength={200} onChange={e => setForm({ ...form, name: e.target.value })} /></label>
              <label>Loại giảm
                <select value={form.discountType} onChange={e => setForm({ ...form, discountType: e.target.value === "PERCENT" ? "PERCENT" : "FIXED" })}>
                  <option value="FIXED">Số tiền cố định (đ)</option>
                  <option value="PERCENT">Phần trăm (%)</option>
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
              <label>Mỗi khách được dùng
                <select value={form.perCustomer} onChange={e => setForm({ ...form, perCustomer: e.target.value as PerCustomer })}>
                  <option value="once">1 lần</option>
                  <option value="many">Nhiều lần (nhập số)</option>
                  <option value="unlimited">Không giới hạn</option>
                </select>
              </label>
              {form.perCustomer === "many" ? <label>Số lần mỗi khách
                <input type="number" min={2} max={1000} value={form.perCustomerCount} onChange={e => setForm({ ...form, perCustomerCount: e.target.value })} /></label> : null}
              <label>Tổng số lượt toàn hệ thống (để trống = không giới hạn)
                <input type="number" min={1} value={form.totalUses} onChange={e => setForm({ ...form, totalUses: e.target.value })} /></label>
            </div>
            <div className="filter-chip-row">
              <button type="button" className={`ghost ${form.isActive ? "active-toggle" : ""}`} onClick={() => setForm({ ...form, isActive: !form.isActive })}>
                {form.isActive ? "Đang bật" : "Đang tắt"}</button>
            </div>
            <div className="entry-form-actions">
              <button className="ghost" onClick={() => navigate("/Admin/Vouchers/Index")}>Hủy</button>
              <button disabled={saving} onClick={() => void save()}>{saving ? "Đang lưu…" : mode === "create" ? "Thêm mới" : "Lưu thay đổi"}</button>
            </div>
          </div>
        </section>
      ) : null}
    </AdminLayout>
  );
}
