import { userMessage } from "../../../../shared/apiError";
import { parseCoordinates } from "../../../../shared/coordinates";
import { useEffect, useState } from "react";
import { AdminLayout } from "../../components/AdminLayout";
import { adminApi } from "../../lib/api";
import type { BranchDto, StaffSessionUserDto } from "../../lib/types";
import { useAutoDismissMessage } from "../../lib/useAutoDismissMessage";

type Props = { onLogout: () => Promise<void> };
type Row = { address: string; latitude: string; longitude: string };

const text = (n: number | null | undefined) => (n == null ? "" : String(n));

/**
 * "Vị trí chi nhánh" (owner 06/11): the address and map position the app uses for "Chỉ đường". Without coordinates
 * the app hides the button; nothing is guessed from the address.
 */
export function BranchLocationsPage({ onLogout }: Props) {
  const [staff, setStaff] = useState<StaffSessionUserDto | null>(null);
  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [rows, setRows] = useState<Record<number, Row>>({});
  const [loading, setLoading] = useState(true);
  const [savingId, setSavingId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useAutoDismissMessage(5000);

  async function loadPage() {
    setLoading(true);
    setError(null);
    try {
      const dashboard = await adminApi.getDashboard();
      setStaff(dashboard.staff);
      setBranches(dashboard.branches);
      const form: Record<number, Row> = {};
      dashboard.branches.forEach(b => {
        form[b.branchId] = { address: b.location ?? "", latitude: text(b.latitude), longitude: text(b.longitude) };
      });
      setRows(form);
    } catch (err) {
      setError(userMessage(err, "Không thể tải danh sách chi nhánh."));
    } finally { setLoading(false); }
  }

  useEffect(() => { void loadPage(); }, []);

  async function save(branch: BranchDto) {
    const row = rows[branch.branchId] ?? { address: "", latitude: "", longitude: "" };
    const parsed = parseCoordinates(row.latitude, row.longitude);
    if (parsed.error) { setError(`${branch.name}: ${parsed.error}`); return; }
    setSavingId(branch.branchId);
    setError(null);
    try {
      const saved = await adminApi.saveBranchLocation(branch.branchId,
        { address: row.address.trim() || null, latitude: parsed.latitude, longitude: parsed.longitude });
      setBranches(branches.map(b => (b.branchId === branch.branchId ? { ...b, ...saved } : b)));
      setRows({ ...rows, [branch.branchId]: { address: saved.location ?? "", latitude: text(saved.latitude), longitude: text(saved.longitude) } });
      setMessage(`Đã lưu vị trí ${branch.name}.`);
    } catch (err) { setError(userMessage(err, "Không thể lưu vị trí.")); }
    finally { setSavingId(null); }
  }

  const update = (id: number, patch: Partial<Row>) =>
    setRows({ ...rows, [id]: { ...(rows[id] ?? { address: "", latitude: "", longitude: "" }), ...patch } });

  return (
    <AdminLayout title="Vị trí chi nhánh" description="Địa chỉ và toạ độ để khách bấm “Chỉ đường” trong app. Để trống toạ độ thì app ẩn nút."
      staff={staff} onLogout={onLogout} onRefresh={loadPage} message={message} error={error}>
      {loading ? <div className="screen-message">Đang tải...</div> : (
        <section className="panel">
          <div className="toolbar-card">
            <div>
              <strong>Cách lấy toạ độ</strong>
              <div className="muted">Mở Google Maps, nhấn giữ đúng vị trí quán, chép cặp số (ví dụ 10.776889, 106.700806) rồi dán vào ô Vĩ độ.</div>
            </div>
          </div>
          <table className="data-table">
            <thead><tr><th>Chi nhánh</th><th>Địa chỉ</th><th>Vĩ độ</th><th>Kinh độ</th><th>Bản đồ</th><th>Thao tác</th></tr></thead>
            <tbody>
              {branches.length > 0 ? branches.map(b => {
                const row = rows[b.branchId] ?? { address: "", latitude: "", longitude: "" };
                const parsed = parseCoordinates(row.latitude, row.longitude);
                return (
                  <tr key={b.branchId}>
                    <td><strong>{b.name}</strong></td>
                    <td><input value={row.address} maxLength={500} aria-label={`Địa chỉ ${b.name}`}
                      onChange={e => update(b.branchId, { address: e.target.value })} /></td>
                    <td><input inputMode="decimal" value={row.latitude} aria-label={`Vĩ độ ${b.name}`} placeholder="10.776889"
                      onChange={e => update(b.branchId, { latitude: e.target.value })} style={{ maxWidth: 150 }} /></td>
                    <td><input inputMode="decimal" value={row.longitude} aria-label={`Kinh độ ${b.name}`} placeholder="106.700806"
                      onChange={e => update(b.branchId, { longitude: e.target.value })} style={{ maxWidth: 150 }} /></td>
                    <td>{parsed.latitude != null && parsed.longitude != null
                      ? <a href={`https://www.google.com/maps/search/?api=1&query=${parsed.latitude},${parsed.longitude}`} target="_blank" rel="noreferrer">Xem thử</a>
                      : <small className="muted">Chưa có</small>}</td>
                    <td><button className="ghost" disabled={savingId === b.branchId} onClick={() => void save(b)}>{savingId === b.branchId ? "Đang lưu..." : "Lưu"}</button></td>
                  </tr>
                );
              }) : <tr><td colSpan={6} className="text-right">Chưa có chi nhánh.</td></tr>}
            </tbody>
          </table>
        </section>
      )}
    </AdminLayout>
  );
}
