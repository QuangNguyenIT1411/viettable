import { FormEvent, useEffect, useMemo, useState } from "react";
import { useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { AdminLayout } from "../../components/AdminLayout";
import { AdminPagination } from "../../components/AdminPagination";
import { adminApi } from "../../lib/api";
import type {
  AdminIngredientDto,
  InventoryBatchDto,
  InventoryMovementDto,
  InventorySummaryDto,
  Paged,
  StaffSessionUserDto,
} from "../../lib/types";
import { useAutoDismissMessage } from "../../lib/useAutoDismissMessage";

type Props = {
  mode: "index" | "stockIn" | "stockOut" | "batches" | "movements";
  onLogout: () => Promise<void>;
};

type StockInRow = {
  id: string;
  ingredientId: string;
  quantity: string;
  receivedDate: string;
  expiryDate: string;
  batchCode: string;
  supplierName: string;
  note: string;
};

const todayInputValue = () => {
  const today = new Date();
  const offset = today.getTimezoneOffset() * 60000;
  return new Date(today.getTime() - offset).toISOString().slice(0, 10);
};

const createStockInRow = (ingredientId = "", seed?: Partial<StockInRow>): StockInRow => ({
  id: `${Date.now()}-${Math.random().toString(36).slice(2)}`,
  ingredientId,
  quantity: "",
  receivedDate: todayInputValue(),
  expiryDate: "",
  batchCode: "",
  supplierName: "",
  note: "",
  ...seed,
});

const stockOutReasons = [
  { value: "EXPIRED_DISPOSAL", label: "Hủy do hết hạn" },
  { value: "WASTE", label: "Hao hụt / hư hỏng" },
  { value: "ADJUST", label: "Điều chỉnh tồn kho" },
  { value: "OTHER", label: "Khác" },
];

const batchStatuses = [
  { value: "all", label: "Tất cả" },
  { value: "expired", label: "Đã hết hạn" },
  { value: "near-expiry", label: "Sắp hết hạn" },
  { value: "valid", label: "Còn hạn" },
  { value: "empty", label: "Đã hết" },
  { value: "inactive", label: "Đã vô hiệu" },
];

function formatDate(value?: string | null) {
  if (!value) return "-";
  const [year, month, day] = value.slice(0, 10).split("-");
  return year && month && day ? `${day}/${month}/${year}` : value;
}

function formatDateTime(value?: string | null) {
  if (!value) return "-";
  return new Date(value).toLocaleString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

function formatNumber(value?: number | null) {
  return Number.isFinite(value ?? NaN) ? Number(value).toLocaleString("vi-VN", { maximumFractionDigits: 2 }) : "-";
}

function formatQuantity(value: number, unit?: string | null, includeSign = false) {
  const sign = includeSign && value > 0 ? "+" : "";
  return `${sign}${formatNumber(value)}${unit ? ` ${unit}` : ""}`;
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

function statusClass(status: string) {
  if (status === "Đã hết hạn" || status === "Đã vô hiệu") return "danger";
  if (status === "Sắp hết hạn") return "warning";
  if (status === "Đã hết") return "info";
  return "success";
}

function movementClass(type: string) {
  if (type === "RECEIVE") return "success";
  if (type === "CONSUME") return "info";
  if (type === "WASTE") return "danger";
  if (type === "ADJUST") return "warning";
  return "info";
}

function referenceLabel(movement: InventoryMovementDto) {
  if (movement.orderId) return `Đơn #${movement.orderId}`;
  if (movement.referenceType === "ADMIN") return "ADMIN";
  return movement.referenceType || "-";
}

function ingredientOptionLabel(ingredient: AdminIngredientDto) {
  return `${ingredient.name} (${ingredient.unit})`;
}

function normalizeSearchText(value: string) {
  return value
    .toLocaleLowerCase("vi-VN")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "");
}

type IngredientSearchSelectProps = {
  ingredients: AdminIngredientDto[];
  value: string;
  onChange: (ingredientId: string) => void;
  ariaLabel: string;
};

function IngredientSearchSelect({ ingredients, value, onChange, ariaLabel }: IngredientSearchSelectProps) {
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const selectedId = Number(value || 0);
  const selectedIngredient = ingredients.find((ingredient) => ingredient.ingredientId === selectedId);
  const selectedLabel = selectedIngredient ? ingredientOptionLabel(selectedIngredient) : "";
  const normalizedQuery = normalizeSearchText(query.trim());
  const filteredIngredients = useMemo(() => {
    if (!normalizedQuery) return ingredients;
    return ingredients.filter((ingredient) => normalizeSearchText(`${ingredient.name} ${ingredient.unit}`).includes(normalizedQuery));
  }, [ingredients, normalizedQuery]);
  const visibleIngredients = filteredIngredients.slice(0, 40);

  useEffect(() => {
    if (!open) {
      setQuery(selectedLabel);
    }
  }, [open, selectedLabel]);

  function selectIngredient(ingredient: AdminIngredientDto) {
    onChange(String(ingredient.ingredientId));
    setQuery(ingredientOptionLabel(ingredient));
    setOpen(false);
  }

  return (
    <div className="ingredient-combobox">
      <input
        aria-label={ariaLabel}
        autoComplete="off"
        role="combobox"
        aria-expanded={open}
        value={query}
        placeholder="Tìm nguyên liệu..."
        onFocus={(event) => {
          setOpen(true);
          event.currentTarget.select();
        }}
        onChange={(event) => {
          const nextQuery = event.target.value;
          setQuery(nextQuery);
          setOpen(true);
          if (!nextQuery.trim()) {
            onChange("");
          }
        }}
        onKeyDown={(event) => {
          if (event.key === "Escape") {
            setOpen(false);
            setQuery(selectedLabel);
            return;
          }
          if (event.key === "Enter" && open) {
            event.preventDefault();
            const firstIngredient = visibleIngredients[0];
            if (firstIngredient) {
              selectIngredient(firstIngredient);
            }
          }
        }}
        onBlur={() => {
          window.setTimeout(() => {
            setOpen(false);
            setQuery(selectedLabel);
          }, 120);
        }}
      />
      {open ? (
        <div className="ingredient-combobox-menu">
          {visibleIngredients.map((ingredient) => (
            <button
              type="button"
              key={ingredient.ingredientId}
              className={ingredient.ingredientId === selectedId ? "selected" : ""}
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => selectIngredient(ingredient)}
            >
              <strong>{ingredient.name}</strong>
              <span>{ingredient.unit}</span>
            </button>
          ))}
          {filteredIngredients.length > visibleIngredients.length ? <div className="ingredient-combobox-hint">Nhập thêm ký tự để thu hẹp kết quả.</div> : null}
          {filteredIngredients.length === 0 ? <div className="ingredient-combobox-empty">Không tìm thấy nguyên liệu</div> : null}
        </div>
      ) : null}
    </div>
  );
}

export function InventoryModulePage({ mode, onLogout }: Props) {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const [staff, setStaff] = useState<StaffSessionUserDto | null>(null);
  const [summary, setSummary] = useState<InventorySummaryDto | null>(null);
  const [ingredients, setIngredients] = useState<AdminIngredientDto[]>([]);
  const [batches, setBatches] = useState<Paged<InventoryBatchDto> | null>(null);
  const [movements, setMovements] = useState<Paged<InventoryMovementDto> | null>(null);
  const [ingredientBatches, setIngredientBatches] = useState<InventoryBatchDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useAutoDismissMessage(5000);

  const search = searchParams.get("search") ?? "";
  const status = searchParams.get("status") ?? "all";
  const page = Math.max(1, Number.parseInt(searchParams.get("page") ?? "1", 10) || 1);
  const ingredientIdFromQuery = searchParams.get("ingredientId") ?? "";
  const ingredientIdFilter = Number.parseInt(ingredientIdFromQuery, 10) > 0 ? Number.parseInt(ingredientIdFromQuery, 10) : undefined;
  const [stockInRows, setStockInRows] = useState<StockInRow[]>(() => [createStockInRow(ingredientIdFromQuery)]);
  const [stockOutForm, setStockOutForm] = useState({
    ingredientId: ingredientIdFromQuery,
    batchId: "",
    quantity: "",
    reason: "WASTE",
    note: "",
  });

  const selectedStockOutIngredientId = Number(stockOutForm.ingredientId || 0);
  const selectedIngredientName = ingredients.find((item) => item.ingredientId === ingredientIdFilter)?.name;
  const title = useMemo(() => {
    if (mode === "stockIn") return "Nhập kho";
    if (mode === "stockOut") return "Xuất kho";
    if (mode === "batches") return "Lô & hạn sử dụng";
    if (mode === "movements") return "Lịch sử xuất nhập";
    return "Quản lý kho";
  }, [mode]);

  useEffect(() => {
    const flash = (location.state as { message?: string } | null)?.message;
    if (flash) {
      setMessage(flash);
      navigate(location.pathname + location.search, { replace: true, state: null });
    }
  }, [location.pathname, location.search, location.state, navigate, setMessage]);

  useEffect(() => {
    if (!ingredientIdFromQuery) return;
    if (mode === "stockIn") {
      setStockInRows((current) => {
        const rows = current.length ? current : [createStockInRow(ingredientIdFromQuery)];
        return rows.map((row, index) => (index === 0 ? { ...row, ingredientId: ingredientIdFromQuery } : row));
      });
    }
    if (mode === "stockOut") {
      setStockOutForm((current) => ({ ...current, ingredientId: ingredientIdFromQuery }));
    }
  }, [mode, ingredientIdFromQuery]);

  async function loadIngredients() {
    const result = await adminApi.getIngredients("", 1, 1000, false);
    setIngredients(result.ingredients.items);
  }

  async function loadPage() {
    setLoading(true);
    setError(null);
    try {
      const session = await adminApi.getSession();
      setStaff(session.staff ?? null);

      if (mode === "index") {
        setSummary(await adminApi.getInventorySummary());
        return;
      }

      if (mode === "stockIn" || mode === "stockOut") {
        await loadIngredients();
        if (mode === "stockOut" && selectedStockOutIngredientId > 0) {
          const nextBatches = await adminApi.getInventoryBatches("", "all", selectedStockOutIngredientId, 1, 100);
          setIngredientBatches(nextBatches.items);
        } else {
          setIngredientBatches([]);
        }
        return;
      }

      if (mode === "batches") {
        if (ingredientIdFilter) await loadIngredients();
        setBatches(await adminApi.getInventoryBatches(search, status, ingredientIdFilter, page, 12));
        return;
      }

      if (ingredientIdFilter) await loadIngredients();
      setMovements(await adminApi.getInventoryMovements({ ingredientId: ingredientIdFilter, search, page, pageSize: 12 }));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể tải dữ liệu kho.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadPage();
  }, [mode, search, status, page, ingredientIdFilter, selectedStockOutIngredientId]);

  function buildListUrl(nextPage = page, nextSearch = search, nextStatus = status) {
    const params = new URLSearchParams();
    if (ingredientIdFilter) params.set("ingredientId", String(ingredientIdFilter));
    if (nextSearch.trim()) params.set("search", nextSearch.trim());
    if (mode === "batches" && nextStatus !== "all") params.set("status", nextStatus);
    if (nextPage > 1) params.set("page", String(nextPage));
    return `/Admin/Inventory/${mode === "movements" ? "Movements" : "Batches"}${params.toString() ? `?${params.toString()}` : ""}`;
  }

  function clearListFilters() {
    const params = new URLSearchParams();
    if (ingredientIdFilter) params.set("ingredientId", String(ingredientIdFilter));
    navigate(`/Admin/Inventory/${mode === "movements" ? "Movements" : "Batches"}${params.toString() ? `?${params.toString()}` : ""}`);
  }

  function updateStockInRow(rowId: string, patch: Partial<StockInRow>) {
    setStockInRows((current) => current.map((row) => (row.id === rowId ? { ...row, ...patch } : row)));
  }

  function addStockInRow() {
    setStockInRows((current) => {
      const last = current[current.length - 1];
      return [
        ...current,
        createStockInRow("", {
          receivedDate: last?.receivedDate || todayInputValue(),
          supplierName: last?.supplierName || "",
          note: last?.note || "",
        }),
      ];
    });
  }

  function removeStockInRow(rowId: string) {
    setStockInRows((current) => (current.length <= 1 ? current : current.filter((row) => row.id !== rowId)));
  }

  function validateStockInRows() {
    for (const [index, row] of stockInRows.entries()) {
      const line = `Dòng ${index + 1}`;
      if (!Number(row.ingredientId)) return `${line}: vui lòng chọn nguyên liệu.`;
      if (!Number.isFinite(Number(row.quantity)) || Number(row.quantity) <= 0) return `${line}: số lượng nhập phải lớn hơn 0.`;
      if (!row.receivedDate) return `${line}: ngày nhập là bắt buộc.`;
      if (!row.expiryDate) return `${line}: hạn sử dụng là bắt buộc.`;
    }
    return null;
  }

  async function handleStockIn(event: FormEvent) {
    event.preventDefault();
    const validation = validateStockInRows();
    if (validation) {
      setError(validation);
      return;
    }

    setSaving(true);
    setError(null);
    let completed = 0;
    try {
      for (const [index, row] of stockInRows.entries()) {
        try {
          await adminApi.stockIn({
            ingredientId: Number(row.ingredientId),
            quantity: Number(row.quantity),
            receivedDate: row.receivedDate,
            expiryDate: row.expiryDate,
            batchCode: row.batchCode || null,
            supplierName: row.supplierName || null,
            note: row.note || null,
          });
          completed += 1;
        } catch (err) {
          const ingredientName = ingredients.find((item) => item.ingredientId === Number(row.ingredientId))?.name ?? "chưa chọn";
          const detail = err instanceof Error ? err.message : "Không thể nhập kho nguyên liệu.";
          setError(`Đã nhập kho ${completed} lô. Dòng ${index + 1} (${ingredientName}) thất bại: ${detail}`);
          return;
        }
      }

      navigate("/Admin/Inventory/Index", {
        replace: true,
        state: { message: stockInRows.length > 1 ? `Đã nhập kho ${stockInRows.length} lô nguyên liệu.` : "Đã nhập kho nguyên liệu." },
      });
    } finally {
      setSaving(false);
    }
  }

  async function handleStockOut(event: FormEvent) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    try {
      const response = await adminApi.stockOut({
        ingredientId: Number(stockOutForm.ingredientId),
        quantity: Number(stockOutForm.quantity),
        batchId: stockOutForm.batchId ? Number(stockOutForm.batchId) : null,
        reason: stockOutForm.reason,
        note: stockOutForm.note || null,
      });
      navigate("/Admin/Inventory/Index", { replace: true, state: { message: response.message } });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể xuất kho nguyên liệu.");
    } finally {
      setSaving(false);
    }
  }

  function renderIndex() {
    return (
      <>
        <section className="stats-grid">
          <div className="stat-card"><span>Lô hết hạn</span><strong>{summary?.expiredBatchCount ?? 0}</strong></div>
          <div className="stat-card"><span>Lô sắp hết hạn</span><strong>{summary?.nearExpiryBatchCount ?? 0}</strong></div>
          <div className="stat-card"><span>Nguyên liệu tồn thấp</span><strong>{summary?.lowStockIngredientCount ?? 0}</strong></div>
          <div className="stat-card"><span>Tổng lô còn tồn</span><strong>{summary?.totalBatchesWithStock ?? 0}</strong></div>
        </section>
        <section className="module-card">
          <div className="section-title-row">
            <div>
              <h2>Thao tác kho</h2>
              <p className="muted-line">Tồn dùng được: {formatNumber(summary?.totalUsableBatchStock)} theo lô còn hạn.</p>
            </div>
          </div>
          <div className="button-row wrap">
            <button onClick={() => navigate("/Admin/Inventory/StockIn")}>Nhập kho</button>
            <button className="ghost" onClick={() => navigate("/Admin/Inventory/StockOut")}>Xuất kho</button>
            <button className="ghost" onClick={() => navigate("/Admin/Inventory/Batches")}>Lô & hạn sử dụng</button>
            <button className="ghost" onClick={() => navigate("/Admin/Inventory/Movements")}>Lịch sử xuất nhập</button>
          </div>
        </section>
      </>
    );
  }

  function renderStockIn() {
    return (
      <section className="module-card inventory-stock-card">
        <div className="section-title-row">
          <div>
            <h2>Nhập kho nguyên liệu</h2>
            <p className="muted-line">Mỗi dòng tạo một lô nhập riêng và mỗi lô chỉ áp dụng cho một nguyên liệu.</p>
          </div>
          <button type="button" className="ghost" onClick={addStockInRow}>Thêm dòng</button>
        </div>

        <form className="inventory-stockin-form" onSubmit={handleStockIn}>
          <div className="inventory-stockin-grid">
            {stockInRows.map((row, index) => (
              <article className="stockin-row-card" key={row.id}>
                <div className="stockin-row-head">
                  <div>
                    <strong>Dòng {index + 1}</strong>
                    <span>Một nguyên liệu, một lô nhập.</span>
                  </div>
                  <button type="button" className="ghost" onClick={() => removeStockInRow(row.id)} disabled={stockInRows.length <= 1}>Xóa dòng</button>
                </div>
                <div className="stockin-container">
                  <div className="stockin-header" aria-hidden="true">
                    <span>Nguyên liệu</span>
                    <span>Số lượng</span>
                    <span>Ngày nhập</span>
                    <span>Hạn sử dụng</span>
                    <span>Mã lô</span>
                    <span>Nhà cung cấp</span>
                  </div>
                  <div className="stockin-row">
                    <IngredientSearchSelect
                      ingredients={ingredients}
                      value={row.ingredientId}
                      onChange={(ingredientId) => updateStockInRow(row.id, { ingredientId })}
                      ariaLabel={`Nguyên liệu dòng ${index + 1}`}
                    />
                    <input aria-label={`Số lượng dòng ${index + 1}`} type="number" min="0.01" step="0.01" value={row.quantity} onChange={(event) => updateStockInRow(row.id, { quantity: event.target.value })} required />
                    <input aria-label={`Ngày nhập dòng ${index + 1}`} type="date" value={row.receivedDate} onChange={(event) => updateStockInRow(row.id, { receivedDate: event.target.value })} required />
                    <input aria-label={`Hạn sử dụng dòng ${index + 1}`} type="date" value={row.expiryDate} onChange={(event) => updateStockInRow(row.id, { expiryDate: event.target.value })} required />
                    <input aria-label={`Mã lô dòng ${index + 1}`} value={row.batchCode} onChange={(event) => updateStockInRow(row.id, { batchCode: event.target.value })} placeholder="Có thể để trống" />
                    <input aria-label={`Nhà cung cấp dòng ${index + 1}`} value={row.supplierName} onChange={(event) => updateStockInRow(row.id, { supplierName: event.target.value })} placeholder="Có thể để trống" />
                  </div>
                  <label className="stockin-note-row">Ghi chú
                    <textarea value={row.note} onChange={(event) => updateStockInRow(row.id, { note: event.target.value })} placeholder="Ghi chú cho lô nhập này" />
                  </label>
                </div>
              </article>
            ))}
          </div>

          <div className="entry-form-actions">
            <button type="button" className="ghost" onClick={() => navigate("/Admin/Inventory/Index")}>Quay lại</button>
            <div className="button-row">
              <button type="button" className="ghost" onClick={addStockInRow}>Thêm dòng</button>
              <button type="submit" disabled={saving}>{saving ? "Đang lưu..." : stockInRows.length > 1 ? `Nhập ${stockInRows.length} lô` : "Nhập kho"}</button>
            </div>
          </div>
        </form>
      </section>
    );
  }

  function renderStockOut() {
    return (
      <section className="module-card">
        <form className="admin-form" onSubmit={handleStockOut}>
          <label>Nguyên liệu
            <IngredientSearchSelect
              ingredients={ingredients}
              value={stockOutForm.ingredientId}
              onChange={(ingredientId) => setStockOutForm({ ...stockOutForm, ingredientId, batchId: "" })}
              ariaLabel="Nguyên liệu xuất kho"
            />
          </label>
          <label>Lô nguyên liệu
            <select value={stockOutForm.batchId} onChange={(event) => setStockOutForm({ ...stockOutForm, batchId: event.target.value })}>
              <option value="">Tự động theo FEFO</option>
              {ingredientBatches.map((batch) => (
                <option key={batch.batchId} value={batch.batchId}>
                  {batch.batchCode || `Lô #${batch.batchId}`} - {formatNumber(batch.quantityRemaining)} {batch.unit} - {batch.status}
                </option>
              ))}
            </select>
          </label>
          <label>Số lượng
            <input type="number" min="0.01" step="0.01" value={stockOutForm.quantity} onChange={(event) => setStockOutForm({ ...stockOutForm, quantity: event.target.value })} required />
          </label>
          <label>Lý do
            <select value={stockOutForm.reason} onChange={(event) => setStockOutForm({ ...stockOutForm, reason: event.target.value })}>
              {stockOutReasons.map((reason) => <option key={reason.value} value={reason.value}>{reason.label}</option>)}
            </select>
          </label>
          <label>Ghi chú
            <textarea value={stockOutForm.note} onChange={(event) => setStockOutForm({ ...stockOutForm, note: event.target.value })} />
          </label>
          <div className="button-row">
            <button type="button" className="ghost" onClick={() => navigate("/Admin/Inventory/Index")}>Quay lại</button>
            <button type="submit" disabled={saving}>{saving ? "Đang lưu..." : "Xuất kho"}</button>
          </div>
        </form>
      </section>
    );
  }

  function renderBatches() {
    const items = batches?.items ?? [];
    return (
      <section className="module-card inventory-list-card">
        <div className="section-title-row">
          <div>
            <h2>Lô & hạn sử dụng</h2>
            {ingredientIdFilter ? <p className="muted-line">Đang lọc theo nguyên liệu: {selectedIngredientName ?? `#${ingredientIdFilter}`}</p> : null}
          </div>
          <button onClick={() => navigate(ingredientIdFilter ? `/Admin/Inventory/StockIn?ingredientId=${ingredientIdFilter}` : "/Admin/Inventory/StockIn")}>Thêm lô nhập</button>
        </div>
        <div className="filter-row inventory-filter-row">
          <input defaultValue={search} placeholder="Tìm nguyên liệu, mã lô hoặc nhà cung cấp..." onKeyDown={(event) => {
            if (event.key === "Enter") navigate(buildListUrl(1, event.currentTarget.value, status));
          }} />
          <select value={status} onChange={(event) => navigate(buildListUrl(1, search, event.target.value))}>
            {batchStatuses.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
          </select>
          <button className="ghost" onClick={clearListFilters}>Xóa bộ lọc</button>
        </div>
        <div className="table-wrap inventory-table-wrap">
          <table className="data-table inventory-table inventory-batches-table">
            <colgroup>
              <col className="inventory-col-ingredient" />
              <col className="inventory-col-batch" />
              <col className="inventory-col-quantity" />
              <col className="inventory-col-date" />
              <col className="inventory-col-date" />
              <col className="inventory-col-status" />
              <col className="inventory-col-supplier" />
            </colgroup>
            <thead>
              <tr>
                <th>Nguyên liệu</th>
                <th>Mã lô</th>
                <th className="text-right">Số lượng còn lại</th>
                <th>Ngày nhập</th>
                <th>Hạn sử dụng</th>
                <th>Trạng thái</th>
                <th>Nhà cung cấp</th>
              </tr>
            </thead>
            <tbody>
              {items.map((batch) => (
                <tr key={batch.batchId}>
                  <td>
                    <div className="inventory-primary-cell">
                      <strong title={batch.ingredientName}>{batch.ingredientName}</strong>
                      <span>#{batch.ingredientId} · {batch.unit}</span>
                    </div>
                  </td>
                  <td>
                    <code className="inventory-code" title={batch.batchCode || `Lô #${batch.batchId}`}>{batch.batchCode || `Lô #${batch.batchId}`}</code>
                  </td>
                  <td className="text-right inventory-quantity">{formatQuantity(batch.quantityRemaining, batch.unit)}</td>
                  <td className="inventory-date">{formatDate(batch.receivedDate)}</td>
                  <td className="inventory-date">{formatDate(batch.expiryDate)}</td>
                  <td><span className={`status-pill ${statusClass(batch.status)}`}>{batch.status}</span></td>
                  <td><span className="inventory-secondary-text" title={batch.supplierName || "-"}>{batch.supplierName || "-"}</span></td>
                </tr>
              ))}
              {items.length === 0 ? (
                <tr>
                  <td colSpan={7}>
                    <div className="empty-report compact-empty">Chưa có lô nguyên liệu phù hợp với bộ lọc hiện tại.</div>
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
        <AdminPagination currentPage={page} totalPages={batches?.totalPages ?? 0} keyPrefix="inventory-batches" onPageChange={(nextPage) => navigate(buildListUrl(nextPage, search, status))} />
      </section>
    );
  }

  function renderMovements() {
    const items = movements?.items ?? [];
    return (
      <section className="module-card inventory-list-card">
        <div className="section-title-row">
          <div>
            <h2>Lịch sử xuất nhập kho</h2>
            {ingredientIdFilter ? <p className="muted-line">Đang lọc theo nguyên liệu: {selectedIngredientName ?? `#${ingredientIdFilter}`}</p> : null}
          </div>
        </div>
        <div className="filter-row inventory-filter-row">
          <input defaultValue={search} placeholder="Tìm nguyên liệu, mã lô hoặc ghi chú..." onKeyDown={(event) => {
            if (event.key === "Enter") navigate(buildListUrl(1, event.currentTarget.value, "all"));
          }} />
          <button className="ghost" onClick={clearListFilters}>Xóa bộ lọc</button>
        </div>
        <div className="table-wrap inventory-table-wrap">
          <table className="data-table inventory-table inventory-movements-table">
            <colgroup>
              <col className="inventory-col-time" />
              <col className="inventory-col-ingredient-wide" />
              <col className="inventory-col-type" />
              <col className="inventory-col-change" />
              <col className="inventory-col-note" />
              <col className="inventory-col-reference" />
            </colgroup>
            <thead>
              <tr>
                <th>Thời gian</th>
                <th>Nguyên liệu / Lô</th>
                <th>Loại</th>
                <th className="text-right">Thay đổi</th>
                <th>Ghi chú</th>
                <th>Tham chiếu</th>
              </tr>
            </thead>
            <tbody>
              {items.map((movement) => (
                <tr key={movement.movementId}>
                  <td className="inventory-date">{formatDateTime(movement.createdAt)}</td>
                  <td>
                    <div className="inventory-primary-cell">
                      <strong title={movement.ingredientName}>{movement.ingredientName}</strong>
                      <span title={movement.batchCode || undefined}>{movement.batchCode || (movement.batchId ? `Lô #${movement.batchId}` : "Không gắn lô")}</span>
                    </div>
                  </td>
                  <td><span className={`status-pill ${movementClass(movement.movementType)}`}>{movementLabel(movement.movementType)}</span></td>
                  <td className={`text-right inventory-change ${movement.quantityChange >= 0 ? "positive" : "negative"}`}>
                    {formatQuantity(movement.quantityChange, movement.unit, true)}
                  </td>
                  <td><span className="inventory-note">{movement.note || "-"}</span></td>
                  <td>
                    <div className="inventory-reference">
                      <strong>{referenceLabel(movement)}</strong>
                      {movement.orderItemId ? <span>Món trong đơn #{movement.orderItemId}</span> : null}
                      {movement.dishId ? <span>Món #{movement.dishId}</span> : null}
                    </div>
                  </td>
                </tr>
              ))}
              {items.length === 0 ? (
                <tr>
                  <td colSpan={6}>
                    <div className="empty-report compact-empty">Chưa có lịch sử xuất nhập phù hợp với bộ lọc hiện tại.</div>
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
        <AdminPagination currentPage={page} totalPages={movements?.totalPages ?? 0} keyPrefix="inventory-movements" onPageChange={(nextPage) => navigate(buildListUrl(nextPage, search, "all"))} />
      </section>
    );
  }

  return (
    <AdminLayout
      title={title}
      description="Quản lý nhập kho, xuất kho, hạn sử dụng và lịch sử tồn kho nguyên liệu."
      staff={staff}
      onLogout={onLogout}
      onRefresh={loadPage}
      message={message}
      error={error}
    >
      {loading ? <div className="loading-card">Đang tải dữ liệu kho...</div> : null}
      {!loading && mode === "index" ? renderIndex() : null}
      {!loading && mode === "stockIn" ? renderStockIn() : null}
      {!loading && mode === "stockOut" ? renderStockOut() : null}
      {!loading && mode === "batches" ? renderBatches() : null}
      {!loading && mode === "movements" ? renderMovements() : null}
    </AdminLayout>
  );
}
