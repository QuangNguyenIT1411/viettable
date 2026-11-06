import { userMessage } from "../../../../shared/apiError";
import { linkedNumbers, parseLinkedTables, type GroupingRow } from "../../../../shared/tableGrouping";
import { useEffect, useState } from "react";
import { AdminLayout } from "../../components/AdminLayout";
import { adminApi } from "../../lib/api";
import type { BranchDto, StaffSessionUserDto } from "../../lib/types";
import { useAutoDismissMessage } from "../../lib/useAutoDismissMessage";

type Props = { onLogout: () => Promise<void> };
type Form = { area: string; linked: string };

/**
 * "Khu vực & ghép bàn" (owner 06/11): each table's area and which tables can be pushed together. When no single
 * table seats a party, the system books the fewest linked tables of one area ("Bàn 5 + 6").
 */
export function TableGroupingPage({ onLogout }: Props) {
  const [staff, setStaff] = useState<StaffSessionUserDto | null>(null);
  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [branchId, setBranchId] = useState<number | null>(null);
  const [rows, setRows] = useState<GroupingRow[]>([]);
  const [forms, setForms] = useState<Record<number, Form>>({});
  const [loading, setLoading] = useState(true);
  const [savingId, setSavingId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useAutoDismissMessage(5000);

  async function loadBranch(id: number) {
    setLoading(true);
    setError(null);
    try {
      const data = await adminApi.getTableGrouping(id);
      setRows(data);
      const next: Record<number, Form> = {};
      data.forEach(r => { next[r.tableId] = { area: r.area ?? "", linked: linkedNumbers(r.linkedTableIds, data) }; });
      setForms(next);
    } catch (err) { setError(userMessage(err, "Không thể tải danh sách bàn.")); }
    finally { setLoading(false); }
  }

  async function loadPage() {
    try {
      const dashboard = await adminApi.getDashboard();
      setStaff(dashboard.staff);
      setBranches(dashboard.branches);
      const first = branchId ?? dashboard.branches[0]?.branchId ?? null;
      setBranchId(first);
      if (first) await loadBranch(first); else setLoading(false);
    } catch (err) { setError(userMessage(err, "Không thể tải trang.")); setLoading(false); }
  }

  useEffect(() => { void loadPage(); }, []);

  async function save(row: GroupingRow) {
    const form = forms[row.tableId] ?? { area: "", linked: "" };
    const parsed = parseLinkedTables(form.linked, row, rows);
    if (parsed.error) { setError(`Bàn ${row.tableNumber}: ${parsed.error}`); return; }
    if (parsed.ids.length > 0 && !form.area.trim()) { setError(`Bàn ${row.tableNumber}: đặt khu vực trước khi chọn bàn ghép.`); return; }
    setSavingId(row.tableId);
    setError(null);
    try {
      await adminApi.saveTableGrouping(row.tableId, { area: form.area.trim() || null, linkedTableIds: parsed.ids });
      setMessage(`Đã lưu Bàn ${row.tableNumber}.`);
      if (branchId) await loadBranch(branchId); // links are kept both ways: reload the other tables too
    } catch (err) { setError(userMessage(err, "Không thể lưu.")); }
    finally { setSavingId(null); }
  }

  const update = (id: number, patch: Partial<Form>) => setForms({ ...forms, [id]: { ...(forms[id] ?? { area: "", linked: "" }), ...patch } });

  return (
    <AdminLayout title="Khu vực & ghép bàn" description="Bàn cùng khu vực và được đánh dấu “ghép được” sẽ được ghép cho nhóm đông khi không có bàn nào đủ chỗ."
      staff={staff} onLogout={onLogout} onRefresh={loadPage} message={message} error={error}>
      <section className="panel">
        <div className="toolbar-card">
          <label>Chi nhánh{" "}
            <select value={branchId ?? ""} onChange={e => { const id = Number(e.target.value); setBranchId(id); void loadBranch(id); }}>
              {branches.map(b => <option key={b.branchId} value={b.branchId}>{b.name}</option>)}
            </select>
          </label>
          <div className="muted">Ví dụ: Bàn 5 và Bàn 6 cùng khu vực “Tầng 1”, ô “Ghép được với” của Bàn 5 ghi “6”. Lưu một bên là đủ, bên kia tự cập nhật.</div>
        </div>
        {loading ? <div className="screen-message">Đang tải...</div> : (
          <table className="data-table">
            <thead><tr><th>Bàn</th><th>Số ghế</th><th>Khu vực</th><th>Ghép được với (số bàn)</th><th>Thao tác</th></tr></thead>
            <tbody>
              {rows.length > 0 ? rows.map(r => {
                const form = forms[r.tableId] ?? { area: "", linked: "" };
                return (
                  <tr key={r.tableId}>
                    <td><strong>Bàn {r.tableNumber}</strong></td>
                    <td>{r.numberOfSeats}</td>
                    <td><input value={form.area} maxLength={50} placeholder="Tầng 1" aria-label={`Khu vực Bàn ${r.tableNumber}`}
                      onChange={e => update(r.tableId, { area: e.target.value })} style={{ maxWidth: 160 }} /></td>
                    <td><input value={form.linked} placeholder="5, 6" aria-label={`Ghép được với Bàn ${r.tableNumber}`}
                      onChange={e => update(r.tableId, { linked: e.target.value })} style={{ maxWidth: 160 }} /></td>
                    <td><button className="ghost" disabled={savingId === r.tableId} onClick={() => void save(r)}>{savingId === r.tableId ? "Đang lưu..." : "Lưu"}</button></td>
                  </tr>
                );
              }) : <tr><td colSpan={5} className="text-right">Chi nhánh chưa có bàn.</td></tr>}
            </tbody>
          </table>
        )}
      </section>
    </AdminLayout>
  );
}
