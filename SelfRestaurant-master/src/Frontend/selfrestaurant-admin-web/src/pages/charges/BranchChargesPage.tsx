import { userMessage } from "../../../../shared/apiError";
import { formatViDateTime } from "../../../../shared/dateFormat";
import { useEffect, useState } from "react";
import { AdminLayout } from "../../components/AdminLayout";
import { adminApi, type BranchChargeDto } from "../../lib/api";
import type { BranchDto, StaffSessionUserDto } from "../../lib/types";
import { useAutoDismissMessage } from "../../lib/useAutoDismissMessage";

type Props = { onLogout: () => Promise<void> };
type Row = { fee: string; vat: string };

/** "7,5" or "7.5" → 7.5; empty → 0; anything else → NaN (refused). */
function percent(text: string): number {
  const value = text.trim().replace(",", ".");
  if (value === "") return 0;
  return /^\d+(\.\d{1,2})?$/.test(value) ? Number(value) : Number.NaN;
}

const show = (n: number) => String(n).replace(".", ",");

/**
 * "Phí & VAT" (owner 05/11/2026): service fee and VAT per branch, 0 = off. The percentages are copied onto each new
 * bill when it is priced, so changing them never changes a bill already issued. Default after deploy: 0% / 0%.
 */
export function BranchChargesPage({ onLogout }: Props) {
  const [staff, setStaff] = useState<StaffSessionUserDto | null>(null);
  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [saved, setSaved] = useState<Record<number, BranchChargeDto>>({});
  const [rows, setRows] = useState<Record<number, Row>>({});
  const [limits, setLimits] = useState({ fee: 30, vat: 20 });
  const [loading, setLoading] = useState(true);
  const [savingId, setSavingId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useAutoDismissMessage(5000);

  async function loadPage() {
    setLoading(true);
    setError(null);
    try {
      const [dashboard, charges] = await Promise.all([adminApi.getDashboard(), adminApi.getBranchCharges()]);
      setStaff(dashboard.staff);
      setBranches(dashboard.branches);
      setLimits({ fee: charges.maxServiceFeePercent, vat: charges.maxVatPercent });
      const byBranch: Record<number, BranchChargeDto> = {};
      charges.items.forEach(x => { byBranch[x.branchId] = x; });
      setSaved(byBranch);
      const form: Record<number, Row> = {};
      dashboard.branches.forEach(b => {
        const s = byBranch[b.branchId];
        form[b.branchId] = { fee: show(s?.serviceFeePercent ?? 0), vat: show(s?.vatPercent ?? 0) };
      });
      setRows(form);
    } catch (err) {
      setError(userMessage(err, "Không thể tải phí phục vụ và VAT."));
    } finally { setLoading(false); }
  }

  useEffect(() => { void loadPage(); }, []);

  async function save(branch: BranchDto) {
    const row = rows[branch.branchId];
    const fee = percent(row?.fee ?? "0");
    const vat = percent(row?.vat ?? "0");
    if (Number.isNaN(fee) || Number.isNaN(vat)) { setError("Phần trăm chỉ nhập số, tối đa 2 chữ số thập phân (ví dụ 5 hoặc 7,5)."); return; }
    if (fee < 0 || fee > limits.fee) { setError(`Phí phục vụ phải từ 0 đến ${limits.fee}%.`); return; }
    if (vat < 0 || vat > limits.vat) { setError(`VAT phải từ 0 đến ${limits.vat}%.`); return; }
    setSavingId(branch.branchId);
    setError(null);
    try {
      const result = await adminApi.saveBranchCharges(branch.branchId, { serviceFeePercent: fee, vatPercent: vat });
      setSaved({ ...saved, [branch.branchId]: result });
      setMessage(`Đã lưu cho ${branch.name}. Áp dụng cho hóa đơn tạo từ bây giờ.`);
    } catch (err) { setError(userMessage(err, "Không thể lưu.")); }
    finally { setSavingId(null); }
  }

  return (
    <AdminLayout title="Phí phục vụ & VAT" description="Theo từng chi nhánh. 0% = tắt. Hóa đơn đã thanh toán giữ nguyên phần trăm lúc thanh toán."
      staff={staff} onLogout={onLogout} onRefresh={loadPage} message={message} error={error}>
      {loading ? <div className="screen-message">Đang tải...</div> : (
        <section className="panel">
          <div className="toolbar-card">
            <div>
              <strong>Công thức</strong>
              <div className="muted">Tạm tính → giảm giá → điểm → phí phục vụ (trên số sau giảm) → VAT (trên số sau giảm + phí) → trừ cọc.</div>
            </div>
          </div>
          <table className="data-table">
            <thead><tr><th>Chi nhánh</th><th>Phí phục vụ (%)</th><th>VAT (%)</th><th>Cập nhật</th><th>Thao tác</th></tr></thead>
            <tbody>
              {branches.length > 0 ? branches.map(b => {
                const s = saved[b.branchId];
                const row = rows[b.branchId] ?? { fee: "0", vat: "0" };
                return (
                  <tr key={b.branchId}>
                    <td><strong>{b.name}</strong></td>
                    <td><input inputMode="decimal" value={row.fee} aria-label={`Phí phục vụ ${b.name}`}
                      onChange={e => setRows({ ...rows, [b.branchId]: { ...row, fee: e.target.value } })} style={{ maxWidth: 110 }} /></td>
                    <td><input inputMode="decimal" value={row.vat} aria-label={`VAT ${b.name}`}
                      onChange={e => setRows({ ...rows, [b.branchId]: { ...row, vat: e.target.value } })} style={{ maxWidth: 110 }} /></td>
                    <td><small className="muted">{s ? `${formatViDateTime(new Date(s.updatedAtUtc))}${s.updatedByName ? ` · ${s.updatedByName}` : ""}` : "Chưa đặt (0% / 0%)"}</small></td>
                    <td><button className="ghost" disabled={savingId === b.branchId} onClick={() => void save(b)}>{savingId === b.branchId ? "Đang lưu..." : "Lưu"}</button></td>
                  </tr>
                );
              }) : <tr><td colSpan={5} className="text-right">Chưa có chi nhánh.</td></tr>}
            </tbody>
          </table>
        </section>
      )}
    </AdminLayout>
  );
}
