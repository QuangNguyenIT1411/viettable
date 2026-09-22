import { useEffect, useState } from "react";
import { useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { AdminLayout } from "../../components/AdminLayout";
import { AdminPagination } from "../../components/AdminPagination";
import { useAppDialog } from "../../components/AppDialog";
import { adminApi } from "../../lib/api";
import type { AdminIngredientDto, AdminIngredientsScreenDto, AdminRelatedIngredientDishDto, IngredientStockMovementDto, StaffSessionUserDto } from "../../lib/types";
import { useAutoDismissMessage } from "../../lib/useAutoDismissMessage";

type Props = {
  mode: "index" | "create" | "edit";
  onLogout: () => Promise<void>;
};

const emptyIngredientForm = { name: "", unit: "kg", currentStock: "0", reorderLevel: "0", isActive: true };
const DELETE_REQUIRES_INACTIVE_MESSAGE = "Vui lòng vô hiệu hóa trước khi xóa.";
const HARD_DELETE_CONFIRM_MESSAGE = "Bạn có chắc muốn xóa dữ liệu này khỏi hệ thống không?";

function ingredientPayload(ingredient: AdminIngredientDto, isActive = ingredient.isActive) {
  return {
    name: ingredient.name,
    unit: ingredient.unit,
    currentStock: ingredient.currentStock,
    reorderLevel: ingredient.reorderLevel,
    isActive,
  };
}

function formatDate(value?: string | null) {
  if (!value) return "-";
  const [year, month, day] = value.slice(0, 10).split("-");
  return year && month && day ? `${day}/${month}/${year}` : value;
}

function formatNumber(value: number) {
  return Number.isFinite(value) ? value.toLocaleString("vi-VN", { maximumFractionDigits: 2 }) : "-";
}

function movementLabel(type: string) {
  return {
    RECEIVE: "Nhập kho",
    CONSUME: "Dùng cho món",
    ADJUST: "Điều chỉnh",
    WASTE: "Hủy / hao hụt",
    DEACTIVATE_BATCH: "Vô hiệu lô",
  }[type] ?? type;
}

export function IngredientsModulePage({ mode, onLogout }: Props) {
  const location = useLocation();
  const navigate = useNavigate();
  const { ingredientId } = useParams();
  const [searchParams] = useSearchParams();
  const [staff, setStaff] = useState<StaffSessionUserDto | null>(null);
  const [screen, setScreen] = useState<AdminIngredientsScreenDto | null>(null);
  const [currentIngredient, setCurrentIngredient] = useState<AdminIngredientDto | null>(null);
  const [form, setForm] = useState(emptyIngredientForm);
  const [movements, setMovements] = useState<IngredientStockMovementDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useAutoDismissMessage(5000);
  const { confirm, Dialog } = useAppDialog();

  const search = searchParams.get("search") ?? "";
  const onlyActive = searchParams.get("onlyActive") === "true";
  const stockStatus = searchParams.get("stockStatus") ?? "ALL";
  const page = Math.max(1, Number.parseInt(searchParams.get("page") ?? "1", 10) || 1);
  const ingredientIdValue = ingredientId ? Number.parseInt(ingredientId, 10) : 0;
  const [searchInput, setSearchInput] = useState(search);
  const [relatedDishIngredient, setRelatedDishIngredient] = useState<AdminIngredientDto | null>(null);
  const [relatedDishes, setRelatedDishes] = useState<AdminRelatedIngredientDishDto[]>([]);
  const [relatedDishesLoading, setRelatedDishesLoading] = useState(false);

  useEffect(() => {
    const flash = (location.state as { message?: string } | null)?.message;
    if (flash) {
      setMessage(flash);
      navigate(location.pathname + location.search, { replace: true, state: null });
    }
  }, [location.pathname, location.search, location.state, navigate, setMessage]);

  async function loadRecentMovements(id: number) {
    const nextMovements = await adminApi.getIngredientStockMovements(id, 1, 5);
    setMovements(nextMovements.items);
  }

  async function loadPage() {
    setLoading(true);
    setError(null);
    try {
      const session = await adminApi.getSession();
      setStaff(session.staff ?? null);

      if (mode === "edit") {
        if (!ingredientIdValue) {
          navigate("/Admin/Ingredients/Index", { replace: true });
          return;
        }

        const ingredient = await adminApi.getIngredientById(ingredientIdValue);
        setCurrentIngredient(ingredient);
        setForm({
          name: ingredient.name,
          unit: ingredient.unit,
          currentStock: String(ingredient.currentStock),
          reorderLevel: String(ingredient.reorderLevel),
          isActive: ingredient.isActive,
        });
        await loadRecentMovements(ingredientIdValue);
        return;
      }

      setCurrentIngredient(null);
      setMovements([]);

      if (mode === "create") {
        setForm(emptyIngredientForm);
        return;
      }

      setScreen(await adminApi.getIngredients(search, page, 10, !onlyActive, stockStatus));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể tải dữ liệu nguyên liệu.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadPage();
  }, [mode, search, onlyActive, stockStatus, page, ingredientIdValue]);

  function buildIndexUrl(nextPage = page, nextSearch = search, nextOnlyActive = onlyActive, nextStockStatus = stockStatus) {
    const params = new URLSearchParams();
    if (nextSearch.trim()) params.set("search", nextSearch.trim());
    if (nextOnlyActive) params.set("onlyActive", "true");
    if (nextStockStatus !== "ALL") params.set("stockStatus", nextStockStatus);
    if (nextPage > 1) params.set("page", String(nextPage));
    return `/Admin/Ingredients/Index${params.toString() ? `?${params.toString()}` : ""}`;
  }

  useEffect(() => {
    if (searchInput !== search) {
      setSearchInput(search);
    }
  }, [search]);

  function applySearchNow(nextSearch = searchInput) {
    navigate(buildIndexUrl(1, nextSearch, onlyActive), { replace: true });
  }

  function inventoryUrl(path: "StockIn" | "StockOut" | "Batches" | "Movements") {
    return `/Admin/Inventory/${path}${ingredientIdValue ? `?ingredientId=${ingredientIdValue}` : ""}`;
  }

  async function handleCreate() {
    if (!form.name.trim()) {
      setError("Tên nguyên liệu không được để trống.");
      return;
    }

    try {
      const response = await adminApi.createIngredient({
        name: form.name.trim(),
        unit: form.unit.trim() || "kg",
        currentStock: Number(form.currentStock || "0"),
        reorderLevel: Number(form.reorderLevel || "0"),
        isActive: true,
      });
      navigate("/Admin/Ingredients/Index", { replace: true, state: { message: response.message } });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể thêm nguyên liệu.");
    }
  }

  async function handleEdit() {
    if (!ingredientIdValue) return;
    if (!form.name.trim()) {
      setError("Tên nguyên liệu không được để trống.");
      return;
    }

    try {
      const response = await adminApi.updateIngredient(ingredientIdValue, {
        name: form.name.trim(),
        unit: form.unit.trim() || "kg",
        currentStock: Number(form.currentStock || "0"),
        reorderLevel: Number(form.reorderLevel || "0"),
        isActive: form.isActive,
      });
      navigate("/Admin/Ingredients/Index", { replace: true, state: { message: response.message } });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể cập nhật nguyên liệu.");
    }
  }


  async function openRelatedDishes(ingredient: AdminIngredientDto) {
    setRelatedDishIngredient(ingredient);
    setRelatedDishes([]);
    setRelatedDishesLoading(true);
    setError(null);
    try {
      setRelatedDishes(await adminApi.getIngredientRelatedDishes(ingredient.ingredientId));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể tải món ăn liên quan.");
    } finally {
      setRelatedDishesLoading(false);
    }
  }

  async function handleDelete(ingredient: AdminIngredientDto) {
    if (ingredient.isActive) {
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
      const response = await adminApi.deleteIngredient(ingredient.ingredientId);
      setMessage(response.message);
      await loadPage();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể xóa nguyên liệu.");
    }
  }

  async function handleSetActive(ingredient: AdminIngredientDto, isActive: boolean) {
    try {
      await adminApi.updateIngredient(ingredient.ingredientId, ingredientPayload(ingredient, isActive));
      setMessage(isActive ? "Đã bật lại nguyên liệu." : "Đã vô hiệu hóa nguyên liệu.");
      await loadPage();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể cập nhật trạng thái nguyên liệu.");
    }
  }

  const title = mode === "create" ? "Thêm nguyên liệu" : mode === "edit" ? "Cập nhật nguyên liệu" : "Quản lý nguyên liệu";
  const description = mode === "index" ? "Quản lý nguyên liệu, tồn hiện tại và thông tin lô nhập." : "Cập nhật thông tin nguyên liệu.";

  return (
    <AdminLayout title={title} description={description} staff={staff} onLogout={onLogout} onRefresh={loadPage} message={message} error={error}>
      {loading ? <div className="screen-message">Đang tải dữ liệu nguyên liệu...</div> : null}

      {mode === "index" && screen ? (
        <section className="panel">
          <div className="toolbar-card">
            <div>
              <strong>Danh sách nguyên liệu</strong>
              <div className="muted">Tìm kiếm, lọc, chỉnh sửa và quản lý trạng thái nguyên liệu.</div>
              <div className="muted">Tồn hiện tại là số tồn hệ thống đang dùng cho đặt món. Tồn theo lô dùng cho quản lý hạn dùng và FEFO.</div>
            </div>
            <button className="ghost" onClick={() => navigate("/Admin/Ingredients/Create")}>Thêm nguyên liệu</button>
          </div>

          <div className="inline-filter-card admin-filter-card">
            <div>
              <strong>Bộ lọc nguyên liệu</strong>
              <div className="muted">Tìm theo tên nguyên liệu, lọc tồn kho và trạng thái để quản lý nhanh hơn.</div>
            </div>
            <div className="admin-filter-form">
              <label className="admin-filter-field admin-filter-field-wide">
                <span>Tìm kiếm</span>
                <input
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      e.preventDefault();
                      applySearchNow(e.currentTarget.value);
                    }
                  }}
                  placeholder="Nhập tên nguyên liệu..."
                />
              </label>
              <label className="admin-filter-field">
                <span>Tồn kho</span>
                <select value={stockStatus} onChange={(e) => navigate(buildIndexUrl(1, search, onlyActive, e.target.value), { replace: true })}>
                  <option value="ALL">Tất cả tồn kho</option>
                  <option value="LOW">Tồn thấp</option>
                  <option value="NORMAL">Tồn bình thường</option>
                </select>
              </label>
              <label className="admin-filter-field">
                <span>Trạng thái</span>
                <select value={onlyActive ? "ACTIVE" : "ALL"} onChange={(e) => navigate(buildIndexUrl(1, search, e.target.value === "ACTIVE", stockStatus), { replace: true })}>
                  <option value="ALL">Tất cả trạng thái</option>
                  <option value="ACTIVE">Đang hoạt động</option>
                </select>
              </label>
            </div>
            <div className="admin-filter-actions">
              <button className="primary-action" onClick={() => applySearchNow()}>Tìm kiếm</button>
              <button
                className="ghost"
                onClick={() => {
                  setSearchInput("");
                  navigate("/Admin/Ingredients/Index");
                }}
              >
                Đặt lại bộ lọc
              </button>
            </div>
          </div>

          <div className="panel-head">
            <h2>Danh sách nguyên liệu</h2>
            <span className="status-pill success">{screen.ingredients.totalItems} nguyên liệu</span>
          </div>
          <table className="data-table">
            <thead>
              <tr>
                <th>Tên nguyên liệu</th>
                <th>Đơn vị</th>
                <th>Tồn hiện tại</th>
                <th>Tồn theo lô</th>
                <th>Hạn gần nhất</th>
                <th>Cảnh báo hạn</th>
                <th>Mức cảnh báo</th>
                <th>Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {screen.ingredients.items.length > 0 ? screen.ingredients.items.map((ingredient) => (
                <tr key={ingredient.ingredientId}>
                  <td><strong>{ingredient.name}</strong></td>
                  <td>{ingredient.unit}</td>
                  <td>
                    <div><strong>{formatNumber(ingredient.currentStock)}</strong></div>
                    <small className="muted-caption">Đang dùng cho đặt món</small>
                  </td>
                  <td>
                    <div><strong>Tổng lô: {formatNumber(ingredient.totalBatchStock)}</strong></div>
                    <small className="muted-caption">Khả dụng theo hạn: {formatNumber(ingredient.usableBatchStock)}</small>
                  </td>
                  <td>{formatDate(ingredient.nearestExpiryDate)}</td>
                  <td>
                    <div className="button-row wrap">
                      {ingredient.expiredBatchCount > 0 ? <span className="status-pill danger">{ingredient.expiredBatchCount} hết hạn</span> : null}
                      {ingredient.nearExpiryBatchCount > 0 ? <span className="status-pill warning">{ingredient.nearExpiryBatchCount} sắp hết hạn</span> : null}
                      {ingredient.expiredBatchCount === 0 && ingredient.nearExpiryBatchCount === 0 ? <span className="muted-caption">-</span> : null}
                    </div>
                  </td>
                  <td>{formatNumber(ingredient.reorderLevel)}</td>
                  <td>{ingredient.isActive ? <span className="status-pill success">Hoạt động</span> : <span className="status-pill danger">Ngừng hoạt động</span>}</td>
                  <td>
                    <div className="button-row wrap">
                      <button className="ghost" onClick={() => navigate(`/Admin/Ingredients/Edit/${ingredient.ingredientId}`)}>Sửa</button>
                      <button className="ghost" onClick={() => void openRelatedDishes(ingredient)}>Món ăn liên quan</button>
                      <button className="danger" onClick={() => void handleDelete(ingredient)}>Xóa</button>
                      {ingredient.isActive ? (
                        <button className="danger" onClick={() => void adminApi.deactivateIngredient(ingredient.ingredientId).then((response) => {
                          setMessage(response.message);
                          return loadPage();
                        }).catch((err) => setError(err instanceof Error ? err.message : "Không thể vô hiệu hóa nguyên liệu."))}>Vô hiệu</button>
                      ) : (
                        <button className="ghost" onClick={() => void handleSetActive(ingredient, true)}>Bật lại</button>
                      )}
                    </div>
                  </td>
                </tr>
              )) : (
                <tr>
                  <td colSpan={9} className="text-right">Chưa có nguyên liệu phù hợp với bộ lọc hiện tại.</td>
                </tr>
              )}
            </tbody>
          </table>
          <AdminPagination currentPage={page} totalPages={screen.ingredients.totalPages} onPageChange={(nextPage) => navigate(buildIndexUrl(nextPage))} keyPrefix="ingredient" />
        </section>
      ) : null}

      {!loading && mode !== "index" ? (
        <section className="panel">
          <div className="toolbar-card">
            <div>
              <strong>{mode === "create" ? "Thêm nguyên liệu mới" : "Chỉnh sửa nguyên liệu"}</strong>
              <div className="muted">{mode === "create" ? "Nhập tên, đơn vị, tồn hiện tại và mức cảnh báo." : "Cập nhật dữ liệu gốc của nguyên liệu. Các thao tác kho được quản lý trong module kho."}</div>
            </div>
            <button className="ghost" onClick={() => navigate("/Admin/Ingredients/Index")}>Quay lại</button>
          </div>

          <div className={`entry-form-card ${mode === "edit" ? "edit-form-card" : ""}`}>
            <div className="entry-form-grid">
              <label>Tên nguyên liệu<input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></label>
              <label>Đơn vị<input value={form.unit} onChange={(e) => setForm({ ...form, unit: e.target.value })} /></label>
              {mode === "create" ? (
                <label>Tồn hiện tại<input type="number" value={form.currentStock} onChange={(e) => setForm({ ...form, currentStock: e.target.value })} /></label>
              ) : (
                <label>Tồn hiện tại<input type="number" value={form.currentStock} readOnly /></label>
              )}
              <label>Mức cảnh báo<input type="number" value={form.reorderLevel} onChange={(e) => setForm({ ...form, reorderLevel: e.target.value })} /></label>
            </div>

            {mode === "edit" ? (
              <div className="filter-chip-row">
                <button type="button" className={`ghost ${form.isActive ? "active-toggle" : ""}`} onClick={() => setForm({ ...form, isActive: !form.isActive })}>
                  {form.isActive ? "Hoạt động" : "Ngừng hoạt động"}
                </button>
              </div>
            ) : null}

            <div className="entry-form-actions">
              <button className="ghost" onClick={() => navigate("/Admin/Ingredients/Index")}>Hủy</button>
              <button onClick={() => void (mode === "create" ? handleCreate() : handleEdit())}>{mode === "create" ? "Thêm nguyên liệu" : "Lưu thay đổi"}</button>
            </div>
          </div>
        </section>
      ) : null}

      {!loading && mode === "edit" && currentIngredient ? (
        <section className="panel">
          <div className="panel-head">
            <div>
              <h2>Tóm tắt tồn kho</h2>
              <p className="muted">Trang nguyên liệu chỉ hiển thị tồn kho ở dạng đọc. Nhập kho, xuất kho và quản lý lô thực hiện trong module kho.</p>
            </div>
          </div>
          <section className="stats-grid">
            <div className="stat-card"><span>Tồn hiện tại</span><strong>{formatNumber(currentIngredient.currentStock)}</strong></div>
            <div className="stat-card"><span>Tồn theo lô</span><strong>{formatNumber(currentIngredient.totalBatchStock)}</strong></div>
            <div className="stat-card"><span>Khả dụng theo hạn</span><strong>{formatNumber(currentIngredient.usableBatchStock)}</strong></div>
            <div className="stat-card"><span>Hạn gần nhất</span><strong>{formatDate(currentIngredient.nearestExpiryDate)}</strong></div>
            <div className="stat-card"><span>Lô hết hạn</span><strong>{currentIngredient.expiredBatchCount}</strong></div>
            <div className="stat-card"><span>Lô sắp hết hạn</span><strong>{currentIngredient.nearExpiryBatchCount}</strong></div>
          </section>
          <div className="button-row wrap">
            <button onClick={() => navigate(inventoryUrl("StockIn"))}>Nhập kho</button>
            <button className="ghost" onClick={() => navigate(inventoryUrl("StockOut"))}>Xuất kho</button>
            <button className="ghost" onClick={() => navigate(inventoryUrl("Batches"))}>Xem lô & hạn sử dụng</button>
            <button className="ghost" onClick={() => navigate(inventoryUrl("Movements"))}>Xem lịch sử đầy đủ</button>
          </div>

          <div className="panel-head">
            <h3>Lịch sử tồn kho gần đây</h3>
          </div>
          <table className="data-table compact-table">
            <thead>
              <tr>
                <th>Thời gian</th>
                <th>Loại</th>
                <th>Lô</th>
                <th>Thay đổi</th>
                <th>Ghi chú</th>
              </tr>
            </thead>
            <tbody>
              {movements.length > 0 ? movements.map((movement) => (
                <tr key={movement.movementId}>
                  <td>{new Date(movement.createdAt).toLocaleString("vi-VN")}</td>
                  <td>{movementLabel(movement.movementType)}</td>
                  <td>{movement.batchId ? `#${movement.batchId}` : "-"}</td>
                  <td>{formatNumber(movement.quantityChange)}</td>
                  <td>{movement.note || "-"}</td>
                </tr>
              )) : (
                <tr>
                  <td colSpan={5} className="text-right">Chưa có lịch sử tồn kho.</td>
                </tr>
              )}
            </tbody>
          </table>
        </section>
      ) : null}

      {relatedDishIngredient ? (
        <section className="modal-backdrop" onClick={() => setRelatedDishIngredient(null)}>
          <div className="modal-card ingredient-related-modal" onClick={(e) => e.stopPropagation()}>
            <div className="panel-head">
              <div>
                <h2>Món ăn liên quan</h2>
                <p className="muted">Nguyên liệu: {relatedDishIngredient.name}</p>
              </div>
              <button className="ghost" onClick={() => setRelatedDishIngredient(null)}>Đóng</button>
            </div>
            {relatedDishesLoading ? <div className="screen-message">Đang tải món ăn liên quan...</div> : null}
            {!relatedDishesLoading && relatedDishes.length === 0 ? (
              <div className="empty-report history-empty-card">
                <i className="bi bi-egg" />
                <strong>Chưa có món ăn sử dụng nguyên liệu này</strong>
                <div>Nguyên liệu chưa được gán vào công thức món ăn nào.</div>
              </div>
            ) : null}
            {!relatedDishesLoading && relatedDishes.length > 0 ? (
              <table className="data-table">
                <thead><tr><th>Món ăn</th><th>Danh mục</th><th>Định lượng/phần</th><th>Trạng thái</th></tr></thead>
                <tbody>
                  {relatedDishes.map((dish) => (
                    <tr key={dish.dishId}>
                      <td><strong>{dish.name}</strong></td>
                      <td>{dish.categoryName || "-"}</td>
                      <td>{formatNumber(dish.quantityPerDish)} {dish.unit}</td>
                      <td>
                        <div className="button-row wrap">
                          <span className={dish.available ? "status-pill success" : "status-pill warning"}>{dish.available ? "Đang bán" : "Tạm ngưng"}</span>
                          <span className={dish.isActive ? "status-pill info" : "status-pill danger"}>{dish.isActive ? "Hoạt động" : "Ngừng hoạt động"}</span>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : null}
          </div>
        </section>
      ) : null}

      <Dialog />
    </AdminLayout>
  );
}
