import { userMessage } from "../../../../shared/apiError";
import { useEffect, useState } from "react";
import { AdminLayout } from "../../components/AdminLayout";
import { adminApi, type DishAttributesDto } from "../../lib/api";
import type { StaffSessionUserDto } from "../../lib/types";
import { useAutoDismissMessage } from "../../lib/useAutoDismissMessage";

type Props = { onLogout: () => Promise<void> };
type Form = { course: string; spicy: string; servings: string; vegetarian: boolean };

const COURSES: [string, string][] = [["", "— Chưa gắn —"], ["MAIN", "Món chính"], ["STARTER", "Khai vị"], ["SOUP", "Canh / súp"],
  ["SIDE", "Món phụ"], ["DESSERT", "Tráng miệng"], ["DRINK", "Đồ uống"]];

const toForm = (d: DishAttributesDto): Form => ({
  course: d.course ?? "", spicy: d.isSpicy == null ? "" : d.isSpicy ? "yes" : "no",
  servings: d.servings == null ? "" : String(d.servings), vegetarian: d.isVegetarian === true,
});

/**
 * "Thuộc tính món" (owner 06/11): what "Gợi ý món" needs to know about each dish. A dish without a kind is never
 * suggested; spicy and servings are left unknown until set here.
 */
export function DishAttributesPage({ onLogout }: Props) {
  const [staff, setStaff] = useState<StaffSessionUserDto | null>(null);
  const [dishes, setDishes] = useState<DishAttributesDto[]>([]);
  const [forms, setForms] = useState<Record<number, Form>>({});
  const [loading, setLoading] = useState(true);
  const [savingId, setSavingId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useAutoDismissMessage(5000);

  async function loadPage() {
    setLoading(true);
    setError(null);
    try {
      const [dashboard, rows] = await Promise.all([adminApi.getDashboard(), adminApi.getDishAttributes()]);
      setStaff(dashboard.staff);
      setDishes(rows);
      const next: Record<number, Form> = {};
      rows.forEach(d => { next[d.dishId] = toForm(d); });
      setForms(next);
    } catch (err) { setError(userMessage(err, "Không thể tải danh sách món.")); }
    finally { setLoading(false); }
  }

  useEffect(() => { void loadPage(); }, []);

  async function save(dish: DishAttributesDto) {
    const form = forms[dish.dishId] ?? toForm(dish);
    const servings = form.servings.trim() === "" ? null : Number(form.servings);
    if (servings != null && (!Number.isInteger(servings) || servings < 1 || servings > 20)) { setError(`${dish.name}: khẩu phần từ 1 đến 20 người.`); return; }
    setSavingId(dish.dishId);
    setError(null);
    try {
      await adminApi.saveDishAttributes(dish.dishId, {
        course: form.course || null, isSpicy: form.spicy === "" ? null : form.spicy === "yes", servings, isVegetarian: form.vegetarian,
      });
      setMessage(`Đã lưu ${dish.name}.`);
    } catch (err) { setError(userMessage(err, "Không thể lưu.")); }
    finally { setSavingId(null); }
  }

  const update = (id: number, patch: Partial<Form>) => setForms({ ...forms, [id]: { ...(forms[id] ?? { course: "", spicy: "", servings: "", vegetarian: false }), ...patch } });
  const tagged = dishes.filter(d => forms[d.dishId]?.course).length;

  return (
    <AdminLayout title="Thuộc tính món" description="Dùng cho “Gợi ý món theo nhóm và ngân sách” trong app. Món chưa gắn loại sẽ không được gợi ý."
      staff={staff} onLogout={onLogout} onRefresh={loadPage} message={message} error={error}>
      {loading ? <div className="screen-message">Đang tải...</div> : (
        <section className="panel">
          <div className="toolbar-card"><div><strong>{tagged}/{dishes.length} món đã gắn loại</strong>
            <div className="muted">Khẩu phần: một phần đủ cho mấy người (lẩu 4 người ghi 4). Để trống nếu là 1 người.</div></div></div>
          <table className="data-table">
            <thead><tr><th>Món</th><th>Giá</th><th>Loại món</th><th>Chay</th><th>Cay</th><th>Khẩu phần</th><th>Thao tác</th></tr></thead>
            <tbody>
              {dishes.length > 0 ? dishes.map(d => {
                const f = forms[d.dishId] ?? toForm(d);
                return (
                  <tr key={d.dishId}>
                    <td><strong>{d.name}</strong></td>
                    <td>{d.price.toLocaleString("vi-VN")}đ</td>
                    <td><select value={f.course} aria-label={`Loại món ${d.name}`} onChange={e => update(d.dishId, { course: e.target.value })}>
                      {COURSES.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></td>
                    <td><input type="checkbox" checked={f.vegetarian} aria-label={`Món chay ${d.name}`} onChange={e => update(d.dishId, { vegetarian: e.target.checked })} /></td>
                    <td><select value={f.spicy} aria-label={`Cay ${d.name}`} onChange={e => update(d.dishId, { spicy: e.target.value })}>
                      <option value="">— Chưa gắn —</option><option value="no">Không cay</option><option value="yes">Cay</option></select></td>
                    <td><input inputMode="numeric" value={f.servings} placeholder="1" aria-label={`Khẩu phần ${d.name}`}
                      onChange={e => update(d.dishId, { servings: e.target.value })} style={{ maxWidth: 70 }} /></td>
                    <td><button className="ghost" disabled={savingId === d.dishId} onClick={() => void save(d)}>{savingId === d.dishId ? "Đang lưu..." : "Lưu"}</button></td>
                  </tr>
                );
              }) : <tr><td colSpan={7} className="text-right">Chưa có món.</td></tr>}
            </tbody>
          </table>
        </section>
      )}
    </AdminLayout>
  );
}
