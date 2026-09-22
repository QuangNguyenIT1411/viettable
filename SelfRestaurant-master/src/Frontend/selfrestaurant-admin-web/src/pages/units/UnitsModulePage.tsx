import { useEffect, useState } from "react";
import { useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { AdminLayout } from "../../components/AdminLayout";
import { AdminPagination } from "../../components/AdminPagination";
import { useAppDialog } from "../../components/AppDialog";
import { adminApi } from "../../lib/api";
import type { AdminUnitDto, AdminUnitsScreenDto, StaffSessionUserDto } from "../../lib/types";
import { useAutoDismissMessage } from "../../lib/useAutoDismissMessage";

type Props = {
  mode: "index" | "create" | "edit";
  onLogout: () => Promise<void>;
};

const emptyUnitForm = { name: "", description: "", displayOrder: "0", isActive: true };
const DELETE_REQUIRES_INACTIVE_MESSAGE = "Vui l\u00f2ng v\u00f4 hi\u1ec7u h\u00f3a tr\u01b0\u1edbc khi x\u00f3a.";
const HARD_DELETE_CONFIRM_MESSAGE = "B\u1ea1n c\u00f3 ch\u1eafc mu\u1ed1n x\u00f3a d\u1eef li\u1ec7u n\u00e0y kh\u1ecfi h\u1ec7 th\u1ed1ng kh\u00f4ng?";

export function UnitsModulePage({ mode, onLogout }: Props) {
  const location = useLocation();
  const navigate = useNavigate();
  const { unitId } = useParams();
  const [searchParams] = useSearchParams();
  const [staff, setStaff] = useState<StaffSessionUserDto | null>(null);
  const [screen, setScreen] = useState<AdminUnitsScreenDto | null>(null);
  const [form, setForm] = useState(emptyUnitForm);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useAutoDismissMessage(5000);
  const { confirm, Dialog } = useAppDialog();

  const search = searchParams.get("search") ?? "";
  const page = Math.max(1, Number.parseInt(searchParams.get("page") ?? "1", 10) || 1);
  const unitIdValue = unitId ? Number.parseInt(unitId, 10) : 0;

  useEffect(() => {
    const flash = (location.state as { message?: string } | null)?.message;
    if (flash) {
      setMessage(flash);
      navigate(location.pathname + location.search, { replace: true, state: null });
    }
  }, [location.pathname, location.search, location.state, navigate]);

  async function loadPage() {
    setLoading(true);
    setError(null);
    try {
      const session = await adminApi.getSession();
      setStaff(session.staff ?? null);

      if (mode === "edit") {
        if (!unitIdValue) {
          navigate("/Admin/Units/Index", { replace: true });
          return;
        }

        const [next, unit] = await Promise.all([
          adminApi.getUnits("", 1, 10, true),
          adminApi.getUnitById(unitIdValue),
        ]);
        setScreen(next);
        setForm({
          name: unit.name,
          description: unit.description ?? "",
          displayOrder: String(unit.displayOrder),
          isActive: unit.isActive,
        });
        return;
      }

      setScreen(await adminApi.getUnits(mode === "index" ? search : "", mode === "index" ? page : 1, 10, true));
      if (mode === "create") {
        setForm(emptyUnitForm);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể tải dữ liệu đơn vị.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadPage();
  }, [mode, search, page, unitIdValue]);

  function buildIndexUrl(nextPage = page, nextSearch = search) {
    const params = new URLSearchParams();
    if (nextSearch.trim()) params.set("search", nextSearch.trim());
    if (nextPage > 1) params.set("page", String(nextPage));
    return `/Admin/Units/Index${params.toString() ? `?${params.toString()}` : ""}`;
  }

  async function handleCreate() {
    if (!form.name.trim()) {
      setError("Tên đơn vị không được để trống.");
      return;
    }

    try {
      const response = await adminApi.createUnit({
        name: form.name.trim(),
        description: form.description.trim() || null,
        displayOrder: Number(form.displayOrder || "0"),
        isActive: form.isActive,
      });
      navigate("/Admin/Units/Index", { replace: true, state: { message: response.message } });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể tạo đơn vị.");
    }
  }

  async function handleEdit() {
    if (!unitIdValue) return;
    if (!form.name.trim()) {
      setError("Tên đơn vị không được để trống.");
      return;
    }

    try {
      const response = await adminApi.updateUnit(unitIdValue, {
        name: form.name.trim(),
        description: form.description.trim() || null,
        displayOrder: Number(form.displayOrder || "0"),
        isActive: form.isActive,
      });
      navigate("/Admin/Units/Index", { replace: true, state: { message: response.message } });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể cập nhật đơn vị.");
    }
  }

  async function handleDelete(unit: AdminUnitDto) {
    if (unit.isActive) {
      setMessage(null);
      setError(DELETE_REQUIRES_INACTIVE_MESSAGE);
      return;
    }

    const approved = await confirm({
      title: "Xác nhận xóa",
      message: HARD_DELETE_CONFIRM_MESSAGE,
      confirmLabel: "Xóa",
      cancelLabel: "Hủy",
      variant: "danger",
    });
    if (!approved) return;

    try {
      const response = await adminApi.deleteUnit(unit.unitId);
      setMessage(response.message);
      await loadPage();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể xóa đơn vị.");
    }
  }

  async function handleSetActive(unit: AdminUnitDto, isActive: boolean) {
    try {
      await adminApi.updateUnit(unit.unitId, {
        name: unit.name,
        description: unit.description ?? null,
        displayOrder: unit.displayOrder,
        isActive,
      });
      setMessage(isActive ? "Đã bật lại đơn vị." : "Đã vô hiệu hóa đơn vị.");
      await loadPage();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể cập nhật trạng thái đơn vị.");
    }
  }
  const title = mode === "create" ? "Thêm đơn vị" : mode === "edit" ? "Cập nhật đơn vị" : "Quản lý đơn vị";
  const description = mode === "index" ? "Quản lý đơn vị dùng cho món ăn và nguyên liệu." : "Cập nhật thông tin đơn vị.";

  return (
    <AdminLayout title={title} description={description} staff={staff} onLogout={onLogout} onRefresh={loadPage} message={message} error={error}>
      {loading ? <div className="screen-message">Đang tải danh sách đơn vị...</div> : null}

      {!loading && mode === "index" && screen ? (
        <section className="panel">
          <div className="toolbar-card">
            <div>
              <strong>Danh sách đơn vị</strong>
              <div className="muted">Theo dõi số món và nguyên liệu đang dùng từng đơn vị.</div>
            </div>
            <button className="ghost" onClick={() => navigate("/Admin/Units/Create")}>Thêm mới</button>
          </div>

          <div className="inline-filter-card admin-filter-card">
            <div>
              <strong>Bộ lọc đơn vị</strong>
              <div className="muted">Tìm theo tên hoặc mô tả đơn vị.</div>
            </div>
            <div className="admin-filter-form">
              <label className="admin-filter-field admin-filter-field-wide">
                <span>Từ khóa</span>
                <input value={search} onChange={(e) => navigate(buildIndexUrl(1, e.target.value), { replace: true })} placeholder="Tên đơn vị..." />
              </label>
              <div className="admin-filter-actions">
                <button className="ghost" onClick={() => navigate("/Admin/Units/Index")}>Xóa bộ lọc</button>
              </div>
            </div>
          </div>

          <table className="data-table">
            <thead>
              <tr>
                <th>Đơn vị</th>
                <th>Mô tả</th>
                <th>Thứ tự</th>
                <th>Đang dùng</th>
                <th>Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {screen.items.length > 0 ? screen.items.map((unit) => (
                <tr key={unit.unitId}>
                  <td><strong>{unit.name}</strong></td>
                  <td>{unit.description || "-"}</td>
                  <td>{unit.displayOrder}</td>
                  <td>{unit.dishCount} món / {unit.ingredientCount} nguyên liệu</td>
                  <td>{unit.isActive ? <span className="status-pill success">Đang dùng</span> : <span className="status-pill danger">Ngừng dùng</span>}</td>
                  <td>
                    <div className="button-row wrap">
                      <button className="ghost" onClick={() => navigate(`/Admin/Units/Edit/${unit.unitId}`)}>Sửa</button>
                      {unit.isActive ? (
                        <button className="danger" onClick={() => void handleSetActive(unit, false)}>Vô hiệu</button>
                      ) : (
                        <button className="ghost" onClick={() => void handleSetActive(unit, true)}>Bật lại</button>
                      )}
                      <button className="danger" onClick={() => void handleDelete(unit)}>Xóa</button>
                    </div>
                  </td>
                </tr>
              )) : <tr><td colSpan={6} className="text-right">Không tìm thấy đơn vị phù hợp.</td></tr>}
            </tbody>
          </table>
          <AdminPagination currentPage={page} totalPages={screen.totalPages} onPageChange={(nextPage) => navigate(buildIndexUrl(nextPage))} keyPrefix="unit" />
        </section>
      ) : null}

      {!loading && mode !== "index" ? (
        <section className="panel">
          <div className="toolbar-card">
            <div>
              <strong>{mode === "create" ? "Thêm mới đơn vị" : "Sửa đơn vị"}</strong>
              <div className="muted">Tên đơn vị sẽ được dùng trong form món ăn và nguyên liệu.</div>
            </div>
            <button className="ghost" onClick={() => navigate("/Admin/Units/Index")}>Quay lại danh sách</button>
          </div>
          <div className="entry-form-card">
            <div className="entry-form-grid">
              <label>Tên đơn vị<input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></label>
              <label>Thứ tự hiển thị<input type="number" value={form.displayOrder} onChange={(e) => setForm({ ...form, displayOrder: e.target.value })} /></label>
              <label className="full-span">Mô tả<textarea rows={3} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></label>
            </div>
            <div className="filter-chip-row">
              <button type="button" className={`ghost ${form.isActive ? "active-toggle" : ""}`} onClick={() => setForm({ ...form, isActive: !form.isActive })}>
                {form.isActive ? "Hoạt động" : "Ngừng hoạt động"}
              </button>
            </div>
            <div className="entry-form-actions">
              <button className="ghost" onClick={() => navigate("/Admin/Units/Index")}>Hủy</button>
              <button onClick={() => void (mode === "create" ? handleCreate() : handleEdit())}>{mode === "create" ? "Thêm mới" : "Lưu thay đổi"}</button>
            </div>
          </div>
        </section>
      ) : null}
      <Dialog />
    </AdminLayout>
  );
}

