import { useEffect, useMemo, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { useAppDialog } from "../components/AppDialog";
import { chefApi } from "../lib/api";
import type {
  ChefDashboardDto,
  ChefDishIngredientsDto,
  ChefMenuDishDto,
  ChefOrderDto,
} from "../lib/types";

type Props = {
  onLogout: () => Promise<void>;
};

type IngredientEditorState = ChefDishIngredientsDto & {
  customerNote?: string | null;
  editMode?: boolean;
  editNote?: string;
  draftItems?: { ingredientId: number; ingredientName: string; unit: string; quantity: string }[];
};

const CHEF_TEXT_MAP: Record<string, string> = {
  "Ban can dang nhap bang tai khoan bep.": "Bạn cần đăng nhập bằng tài khoản bếp.",
  "Mi Xao Bo": "Mì Xào Bò",
  "Bun Cha Ha Noi": "Bún Chả Hà Nội",
  "Com Suon Bi Cha": "Cơm Sườn Bì Chả",
  "Bun Bo Hue": "Bún Bò Huế",
  "Hu Tieu Nam Vang": "Hủ Tiếu Nam Vang",
  "Mi xao bo dam da huong vi": "Mì xào bò đậm đà hương vị",
  "Bun cha dac san Ha Noi voi cha nuong than hong": "Bún chả đặc sản Hà Nội với chả nướng than hồng",
  "Com suon bi cha truyen thong Sai Gon": "Cơm sườn bì chả truyền thống Sài Gòn",
  "Bun bo Hue cay nong dam da": "Bún bò Huế cay nồng đậm đà",
  "Hu tieu Nam Vang dac biet": "H tiu Nam Vang c bit",
  "Dang ban": "Đang bán",
  "Tam ngung ban": "Tạm ngưng bán",
  "Tam dung ban": "Tạm dừng bán",
  "Mon noi bat": "Món nổi bật",
  "Mon chinh": "Món chính",
  "Mon phu": "Món phụ",
  "Trang mieng": "Tráng miệng",
  "Do uong": "Đồ uống",
  "Mon chay": "Món chay",
  "Mon dac biet": "Món đặc biệt",
  "Hien thi tren thuc don": "Hiển thị trên thực đơn",
  "Cho che bien": "Chờ chế biến",
  "Dang che bien": "Đang chế biến",
  "San sang": "Sẵn sàng",
  "Nguyen lieu": "Nguyên liệu",
  "Phan": "Phần",
  "To": "Tô",
  "Dia": "Đĩa",
  "Suat": "Suất",
};

function normalizeChefText(value?: string | null): string {
  if (!value) return "";

  let normalized = value.replace(/\\u([0-9a-fA-F]{4})/g, (_, hex) =>
    String.fromCharCode(Number.parseInt(hex, 16)),
  );

  for (const [source, target] of Object.entries(CHEF_TEXT_MAP)) {
    normalized = normalized.split(source).join(target);
  }

  return normalized.trim();
}

function normalizeChefDashboard(data: ChefDashboardDto): ChefDashboardDto {
  return {
    ...data,
    staff: {
      ...data.staff,
      name: normalizeChefText(data.staff.name),
      roleName: normalizeChefText(data.staff.roleName),
      branchName: normalizeChefText(data.staff.branchName),
    },
    pendingOrders: data.pendingOrders.map(normalizeChefOrder),
    preparingOrders: data.preparingOrders.map(normalizeChefOrder),
    readyOrders: data.readyOrders.map(normalizeChefOrder),
    history: data.history.map((item) => ({
      ...item,
      tableName: normalizeChefText(item.tableName),
      statusName: normalizeChefText(item.statusName),
      dishesSummary: normalizeChefText(item.dishesSummary),
    })),
    menu: {
      ...data.menu,
      branchName: normalizeChefText(data.menu.branchName),
      dishes: data.menu.dishes.map((dish) => ({
        ...dish,
        name: normalizeChefText(dish.name),
        unit: normalizeChefText(dish.unit) || "Phần",
        categoryName: normalizeChefText(dish.categoryName),
        description: normalizeChefText(dish.description),
      })),
    },
    ingredients: data.ingredients.map((item) => ({
      ...item,
      name: normalizeChefText(item.name),
      unit: normalizeChefText(item.unit),
    })),
  };
}

function normalizeChefOrder(order: ChefOrderDto): ChefOrderDto {
  return {
    ...order,
    tableName: normalizeChefText(order.tableName),
    statusName: normalizeChefText(order.statusName),
    items: order.items.map((item) => ({
      ...item,
      dishName: normalizeChefText(item.dishName),
      note: normalizeChefText(item.note),
    })),
  };
}

function normalizeItemStatusCode(value?: string | null) {
  return (value ?? "").trim().toUpperCase();
}

function getItemStatusLabel(statusCode?: string | null) {
  const normalized = normalizeItemStatusCode(statusCode);
  if (normalized === "PREPARING") return "Đang chế biến";
  if (normalized === "READY") return "Sẵn sàng";
  if (normalized === "SERVING") return "Đã giao phục vụ";
  if (normalized === "CANCELLED") return "Đã hủy";
  if (normalized === "CONFIRMED") return "Chờ chế biến";
  return "Chờ gửi";
}

function getItemStatusClass(statusCode?: string | null) {
  const normalized = normalizeItemStatusCode(statusCode);
  if (normalized === "PREPARING") return "chef-history-status chef-history-status-warning";
  if (normalized === "READY" || normalized === "SERVING") return "chef-history-status chef-history-status-primary";
  if (normalized === "CANCELLED") return "chef-history-status chef-history-status-danger";
  return "chef-history-status chef-history-status-muted";
}

function normalizeIngredientEditorPayload(payload: ChefDishIngredientsDto): ChefDishIngredientsDto {
  return {
    ...payload,
    dishName: normalizeChefText(payload.dishName),
    items: payload.items.map((item) => ({
      ...item,
      name: normalizeChefText(item.name),
      unit: normalizeChefText(item.unit),
      quantityPerDish: Number(item.quantityPerDish) || 0,
      defaultQuantityPerDish: Number(item.defaultQuantityPerDish ?? item.quantityPerDish) || 0,
      isOverridden: Boolean(item.isOverridden),
    })),
  };
}

export function DashboardPage({ onLogout }: Props) {
  const location = useLocation();
  const { prompt, Dialog } = useAppDialog();
  const [activeTab, setActiveTab] = useState<"orders" | "menu">("orders");
  const [dishSearch, setDishSearch] = useState("");
  const [dishStatusFilter, setDishStatusFilter] = useState<"ALL" | "AVAILABLE" | "PAUSED">("ALL");
  const [dishSpecialFilter, setDishSpecialFilter] = useState<"ALL" | "SPECIAL" | "NORMAL">("ALL");
  const [data, setData] = useState<ChefDashboardDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [accountDraft, setAccountDraft] = useState({ name: "", phone: "", email: "" });
  const [passwordDraft, setPasswordDraft] = useState({ currentPassword: "", newPassword: "", confirmPassword: "" });
  const [ingredientEditor, setIngredientEditor] = useState<IngredientEditorState | null>(null);
  const [ingredientStockOpen, setIngredientStockOpen] = useState(false);
  const [ingredientStockSearch, setIngredientStockSearch] = useState("");
  const [onlyLowStock, setOnlyLowStock] = useState(false);
  const [cancelEditor, setCancelEditor] = useState<{ orderId: number; orderCode: string; reason: string } | null>(null);

  const filteredMenuDishes = useMemo(() => {
    if (!data) return [] as ChefMenuDishDto[];
    const query = dishSearch.trim().toLowerCase();
    return data.menu.dishes.filter((dish) => {
      const matchesSearch = query.length === 0 || dish.name.toLowerCase().includes(query);
      const matchesStatus =
        dishStatusFilter === "ALL" ||
        (dishStatusFilter === "AVAILABLE" ? dish.available : !dish.available);
      const matchesSpecial =
        dishSpecialFilter === "ALL" ||
        (dishSpecialFilter === "SPECIAL" ? dish.isDailySpecial : !dish.isDailySpecial);

      return matchesSearch && matchesStatus && matchesSpecial;
    });
  }, [data, dishSearch, dishSpecialFilter, dishStatusFilter]);

  const pageMode = location.pathname.toLowerCase().includes("/staff/chef/history") ? "history" : "index";

  async function load(options?: { silent?: boolean }) {
    const silent = options?.silent ?? false;
    if (!silent) {
      setLoading(true);
    }
    setError(null);
    try {
      const dashboard = await chefApi.getDashboard();
      setData(normalizeChefDashboard(dashboard));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể tải dữ liệu bếp.");
    } finally {
      if (!silent) {
        setLoading(false);
      }
    }
  }

  useEffect(() => {
    void load();
    const timer = window.setInterval(() => {
      void load({ silent: true });
    }, 4000);
    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    setActiveTab(location.hash.toLowerCase() === "#menu" ? "menu" : "orders");
  }, [location.hash]);

  useEffect(() => {
    if (!data) return;
    setAccountDraft({
      name: data.staff.name,
      phone: data.staff.phone ?? "",
      email: data.staff.email ?? "",
    });
  }, [data]);

  useEffect(() => {
    if (!message) return;
    const timer = window.setTimeout(() => setMessage(null), 5000);
    return () => window.clearTimeout(timer);
  }, [message]);

  function formatShortDateTime(value?: string | null) {
    if (!value) return "Chưa cập nhật";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return value;
    return date.toLocaleString("vi-VN", {
      day: "2-digit",
      month: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  }

  function getHistoryStatusClass(statusCode?: string | null) {
    const normalized = (statusCode ?? "").toUpperCase();
    if (normalized === "COMPLETED") return "chef-history-status chef-history-status-success";
    if (normalized === "READY" || normalized === "SERVING") return "chef-history-status chef-history-status-primary";
    if (normalized === "PREPARING") return "chef-history-status chef-history-status-warning";
    if (normalized === "CANCELLED") return "chef-history-status chef-history-status-danger";
    return "chef-history-status chef-history-status-muted";
  }

  async function act(fn: () => Promise<{ message?: string }>) {
    setMessage(null);
    setError(null);
    try {
      const result = await fn();
      setMessage(result.message ?? "Đã cập nhật.");
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể cập nhật.");
    }
  }

  async function openIngredients(dishId: number, customerNote?: string | null, orderId?: number, itemId?: number) {
    setError(null);
    try {
      const payload = orderId && itemId
        ? await chefApi.getOrderItemIngredients(orderId, itemId)
        : await chefApi.getDishIngredients(dishId);
      const normalized = normalizeIngredientEditorPayload(payload);
      setIngredientEditor({
        ...normalized,
        customerNote: normalizeChefText(customerNote),
        draftItems: normalized.items.map((item) => ({ ingredientId: item.ingredientId, ingredientName: item.name, unit: item.unit, quantity: String(item.quantityPerDish) })),
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể tải nguyên liệu.");
    }
  }

  async function saveOrderItemIngredientOverrides() {
    if (!ingredientEditor?.orderId || !ingredientEditor.itemId || !ingredientEditor.draftItems) return;
    const items = ingredientEditor.draftItems.map((item) => ({
      ingredientId: item.ingredientId,
      ingredientName: item.ingredientName,
      unit: item.unit,
      quantity: Number(item.quantity || "0"),
    }));
    if (items.some((item) => Number.isNaN(item.quantity) || item.quantity < 0)) {
      setError("Số lượng nguyên liệu không được âm.");
      return;
    }
    await act(() => chefApi.saveOrderItemIngredients(ingredientEditor.orderId!, ingredientEditor.itemId!, items, ingredientEditor.editNote));
    setIngredientEditor(null);
  }

  async function saveAccount() {
    setMessage(null);
    setError(null);
    try {
      const updated = await chefApi.updateAccount(accountDraft);
      if (updated) {
        setData((current) => (current ? { ...current, staff: updated } : current));
      }
      setMessage("Cập nhật tài khoản thành công.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể cập nhật tài khoản.");
    }
  }

  async function changePassword() {
    setMessage(null);
    setError(null);
    if (
      passwordDraft.newPassword !== "" &&
      passwordDraft.confirmPassword !== "" &&
      passwordDraft.newPassword !== passwordDraft.confirmPassword
    ) {
      setError("Mật khẩu mới và xác nhận mật khẩu chưa khớp.");
      return;
    }
    try {
      const result = await chefApi.changePassword(passwordDraft);
      setMessage(result.message || "Đổi mật khẩu thành công.");
      setPasswordDraft({ currentPassword: "", newPassword: "", confirmPassword: "" });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể đổi mật khẩu.");
    }
  }

  async function submitCancel() {
    if (!cancelEditor?.reason.trim()) {
      setError("Vui lòng nhập lý do hủy đơn.");
      return;
    }
    await act(() => chefApi.cancelOrder(cancelEditor.orderId, cancelEditor.reason.trim()));
    setCancelEditor(null);
  }

  const ingredientStockRows = useMemo(() => {
    if (!data) return [];

    const query = ingredientStockSearch.trim().toLowerCase();
    return [...data.ingredients]
      .filter((item) => {
        const matchesQuery = query.length === 0 || item.name.toLowerCase().includes(query);
        const isLowStock = item.reorderLevel > 0 && item.currentStock <= item.reorderLevel;
        return matchesQuery && (!onlyLowStock || isLowStock);
      })
      .sort((a, b) => {
        const aLow = a.reorderLevel > 0 && a.currentStock <= a.reorderLevel ? 0 : 1;
        const bLow = b.reorderLevel > 0 && b.currentStock <= b.reorderLevel ? 0 : 1;
        return aLow - bLow || a.name.localeCompare(b.name, "vi");
      });
  }, [data, ingredientStockSearch, onlyLowStock]);
  const passwordMismatch =
    passwordDraft.newPassword !== "" &&
    passwordDraft.confirmPassword !== "" &&
    passwordDraft.newPassword !== passwordDraft.confirmPassword;

  if (loading) return <div className="screen-message">Đang tải bảng bếp...</div>;
  if (error && !data) return <div className="screen-message error-box">{error}</div>;
  async function promptCancelItem(orderId: number, itemId: number, dishName: string) {
    const reason = await prompt({
      title: "Nhập lý do hủy món",
      message: `Nhập lý do hủy món "${dishName}":`,
      confirmLabel: "Đồng ý",
      cancelLabel: "Hủy",
      placeholder: "Lý do hủy",
      multiline: true,
      variant: "danger",
    });
    if (!reason?.trim()) return;
    await act(() => chefApi.cancelItem(orderId, itemId, reason.trim()));
  }

  if (!data) return null;

  if (pageMode === "history") {
    return (
      <main className="chef-shell chef-subpage chef-history-shell">
        <section className="hero-card chef-hero chef-history-hero">
          <header className="chef-header">
            <div>
              <p className="eyebrow">{data.staff.branchName}</p>
              <h1>Tài khoản & Lịch sử bếp</h1>
              <p className="muted">
                {data.staff.name} | {data.staff.roleName}
              </p>
            </div>
            <div className="header-actions">
              <Link className="ghost action-link" to="/Staff/Chef/Index">
                Quay về màn hình bếp
              </Link>
              <button className="ghost" onClick={() => void onLogout()}>
                Đăng xuất
              </button>
            </div>
          </header>
        </section>

        {message ? <div className="success-box">{message}</div> : null}
        {error ? <div className="error-box">{error}</div> : null}

        <section className="split-grid chef-history-grid">
          <div className="panel">
            <div className="panel-head">
              <h2>
                <i className="bi bi-person me-2" />
                Thông tin tài khoản
              </h2>
            </div>
            <div className="chef-panel-body">
              <form className="chef-account-form" onSubmit={(event) => event.preventDefault()}>
                <label>
                  Họ tên
                  <input
                    value={accountDraft.name}
                    onChange={(e) => setAccountDraft((current) => ({ ...current, name: e.target.value }))}
                  />
                </label>
                <label>
                  Tên đăng nhập
                  <input value={data.staff.username} disabled />
                </label>
                <label>
                  Email
                  <input
                    type="email"
                    value={accountDraft.email}
                    onChange={(e) => setAccountDraft((current) => ({ ...current, email: e.target.value }))}
                  />
                </label>
                <label>
                  Số điện thoại
                  <input
                    value={accountDraft.phone}
                    onChange={(e) => setAccountDraft((current) => ({ ...current, phone: e.target.value }))}
                  />
                </label>
                <div className="muted">
                  Chi nhánh: <strong>{data.staff.branchName}</strong> | Vai trò: <strong>{data.staff.roleName}</strong>
                </div>
                <button className="chef-primary-button" onClick={() => void saveAccount()}>
                  Lưu thay đổi
                </button>
              </form>

              <hr />

              <div className="stack">
                <h3 className="subsection-title">Đổi mật khẩu</h3>
                <label>
                  Mật khẩu hiện tại
                  <input
                    type="password"
                    value={passwordDraft.currentPassword}
                    onChange={(e) => setPasswordDraft((current) => ({ ...current, currentPassword: e.target.value }))}
                  />
                </label>
                <label>
                  Mật khẩu mới
                  <input
                    type="password"
                    value={passwordDraft.newPassword}
                    onChange={(e) => setPasswordDraft((current) => ({ ...current, newPassword: e.target.value }))}
                  />
                </label>
                <label>
                  Xác nhận mật khẩu mới
                  <input
                    type="password"
                    value={passwordDraft.confirmPassword}
                    onChange={(e) => setPasswordDraft((current) => ({ ...current, confirmPassword: e.target.value }))}
                  />
                </label>
                {passwordMismatch ? (
                  <div className="field-error">Mật khẩu xác nhận chưa khớp với mật khẩu mới.</div>
                ) : null}
                <button className="chef-outline-button" disabled={passwordMismatch} onClick={() => void changePassword()}>
                  Đổi mật khẩu
                </button>
              </div>
            </div>
          </div>

          <div className="panel">
            <div className="panel-head">
              <h2>
                <i className="bi bi-clock-history me-2" />
                Lịch sử đơn hàng bếp
              </h2>
            </div>
            <div className="table-scroll">
              <table className="staff-table">
                <thead>
                  <tr>
                    <th>Thời gian</th>
                    <th>Mã đơn</th>
                    <th>Bàn</th>
                    <th>Món</th>
                    <th>Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {data.history.length === 0 ? (
                    <tr>
                      <td colSpan={5} className="muted table-empty">
                        Chưa có đơn hàng nào được ghi nhận.
                      </td>
                    </tr>
                  ) : (
                    data.history.slice(0, 100).map((item) => (
                      <tr key={item.orderId}>
                        <td>{formatShortDateTime(item.completedTime ?? item.orderTime)}</td>
                        <td>{item.orderCode || `ORD${item.orderId}`}</td>
                        <td>{item.tableName || "Không rõ bàn"}</td>
                        <td>{item.dishesSummary}</td>
                        <td>
                          <span className={getHistoryStatusClass(item.statusCode)}>{item.statusName}</span>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
            {data.history.length > 0 ? (
              <p className="muted history-footnote">Hiển thị tối đa 100 đơn gần nhất tại chi nhánh của bạn.</p>
            ) : null}
          </div>
        </section>
      </main>
    );
  }

  return (
    <main className="chef-shell chef-index-shell chef-kds-shell">
      <section className="chef-mvc-header chef-kds-header">
        <div className="chef-mvc-header-top">
          <div className="chef-mvc-brand">
            <i className="bi bi-fire chef-mvc-brand-icon" />
            <div>
              <h1>Bếp & Chế Biến</h1>
              <div className="chef-mvc-meta">
                {data.staff.branchName} - Nhân viên bếp: {data.staff.name}
              </div>
            </div>
          </div>
            <div className="chef-mvc-actions">
              <div className="chef-tab-pills" role="tablist" aria-label="Điều hướng bếp">
              <button
                type="button"
                className={activeTab === "orders" ? "tab-pill active" : "tab-pill"}
                onClick={() => setActiveTab("orders")}
              >
                <i className="bi bi-kanban me-2" />
                Đơn Hàng
              </button>
              <button
                type="button"
                className={activeTab === "menu" ? "tab-pill active" : "tab-pill"}
                onClick={() => setActiveTab("menu")}
              >
                <i className="bi bi-journal-text me-2" />
                Thực Đơn
              </button>
              </div>
              <div className="chef-mvc-divider" />
              <button className="ghost chef-header-button" onClick={() => setIngredientStockOpen(true)}>
                <i className="bi bi-box-seam me-2" />
                Kho nguyên liệu
              </button>
              <Link className="ghost action-link chef-header-button" to="/Staff/Chef/History">
                <i className="bi bi-clock-history me-2" />
                Lịch sử & Tài khoản
            </Link>
            <button className="ghost chef-logout-button" onClick={() => void onLogout()}>
              <i className="bi bi-box-arrow-right" />
            </button>
          </div>
        </div>
      </section>

      {message ? <div className="success-box">{message}</div> : null}
      {error ? <div className="error-box">{error}</div> : null}

      {activeTab === "orders" ? (
        <section className="board-grid chef-kds-board">
          <OrderColumn
            title="Chờ chế biến"
            tone="secondary"
            orders={data.pendingOrders}
            actionLabel="BẮT ĐẦU NẤU"
            action={(orderId) => act(() => chefApi.startOrder(orderId))}
            secondaryLabel="HỦY"
            secondaryAction={(orderId, orderCode) => {
              setCancelEditor({ orderId, orderCode, reason: "" });
            }}
            onOpenIngredients={(dishId, customerNote, orderId, itemId) => void openIngredients(dishId, customerNote, orderId, itemId)}
            onStartItem={(orderId, itemId) => act(() => chefApi.startItem(orderId, itemId))}
            onCancelItem={promptCancelItem}
          />
          <OrderColumn
            title="Đang chế biến"
            tone="primary"
            orders={data.preparingOrders}
            actionLabel="Hoàn thành"
            action={(orderId) => act(() => chefApi.readyOrder(orderId))}
            secondaryLabel="HỦY"
            secondaryAction={(orderId, orderCode) => {
              setCancelEditor({ orderId, orderCode, reason: "" });
            }}
            onOpenIngredients={(dishId, customerNote, orderId, itemId) => void openIngredients(dishId, customerNote, orderId, itemId)}
            onReadyItem={(orderId, itemId) => act(() => chefApi.readyItem(orderId, itemId))}
            onCancelItem={promptCancelItem}
          />
          <OrderColumn
            title="Sẵn sàng"
            tone="success"
            orders={data.readyOrders}
            secondaryLabel="HỦY"
            secondaryAction={(orderId, orderCode) => {
              setCancelEditor({ orderId, orderCode, reason: "" });
            }}
            onOpenIngredients={(dishId, customerNote, orderId, itemId) => void openIngredients(dishId, customerNote, orderId, itemId)}
            onCancelItem={promptCancelItem}
          />
        </section>
      ) : null}

      {activeTab === "menu" ? (
        <section className="panel chef-menu-panel">
          <div className="chef-menu-toolbar">
            <div>
              <h2>Thực đơn hôm nay</h2>
              <p className="muted chef-menu-subtitle">
                {data.menu.branchName} | {formatShortDateTime(`${data.menu.menuDate}T00:00:00`)}
              </p>
            </div>
            <div className="chef-chip-row">
              <span className="soft-badge info">{data.menu.branchName}</span>
              <span className="soft-badge primary">{data.menu.dishes.length} món</span>
              <span className="soft-badge success">{data.menu.dishes.filter((dish) => dish.available).length} đang bán</span>
            </div>
          </div>
          <div className="chef-menu-filters">
            <input
              type="text"
              value={dishSearch}
              onChange={(event) => setDishSearch(event.target.value)}
              placeholder="Tìm món theo tên..."
            />
            <select value={dishStatusFilter} onChange={(event) => setDishStatusFilter(event.target.value as "ALL" | "AVAILABLE" | "PAUSED")}>
              <option value="ALL">Tất cả trạng thái</option>
              <option value="AVAILABLE">Đang bán</option>
              <option value="PAUSED">Tạm ngưng bán</option>
            </select>
            <select value={dishSpecialFilter} onChange={(event) => setDishSpecialFilter(event.target.value as "ALL" | "SPECIAL" | "NORMAL")}>
              <option value="ALL">Tất cả loại</option>
              <option value="SPECIAL">Món đặc biệt</option>
              <option value="NORMAL">Món thường</option>
            </select>
          </div>
          <div className="menu-grid">
            {filteredMenuDishes.map((dish) => (
              <article
                key={dish.dishId}
                className="dish-card"
                data-dish-id={dish.dishId}
                data-name={dish.name.toLowerCase()}
                data-available={dish.available ? "true" : "false"}
                data-special={dish.isDailySpecial ? "true" : "false"}
              >
                {dish.image ? (
                  <img className="dish-img" src={dish.image} alt={dish.name} />
                ) : (
                  <div className="dish-img dish-img-placeholder">
                    <i className="bi bi-image" />
                  </div>
                )}
                <div className="dish-body">
                  <h5 className="dish-title" title={dish.name}>
                    {dish.name}
                    {dish.isDailySpecial ? <span className="badge badge-special ms-2">Đặc biệt</span> : null}
                  </h5>
                  <div className="d-flex justify-content-between align-items-center mb-2">
                    <span className="dish-price">{dish.price.toLocaleString("vi-VN")} đ</span>
                    <span className="badge bg-light text-dark border">{dish.unit || "Phần"}</span>
                  </div>
                  <div className="d-flex justify-content-between align-items-center mt-3">
                    <span className={`badge ${dish.available ? "bg-success" : "bg-secondary"}`}>
                      {dish.available ? "Đang bán" : "Tạm ngưng"}
                    </span>
                    <div className="btn-group">
                      <button className="btn btn-sm btn-outline-secondary" onClick={() => void openIngredients(dish.dishId)}>
                        <i className="bi bi-list-ul" /> Thành phần
                      </button>
                      {dish.available ? (
                        <button className="btn btn-sm btn-outline-warning" onClick={() => void act(() => chefApi.setDishAvailability(dish.dishId, false))}>
                          <i className="bi bi-pause-circle" /> Tạm ngưng
                        </button>
                      ) : (
                        <button className="btn btn-sm btn-outline-success" onClick={() => void act(() => chefApi.setDishAvailability(dish.dishId, true))}>
                          <i className="bi bi-play-circle" /> Tiếp tục
                        </button>
                      )}
                    </div>
                  </div>
                </div>
              </article>
            ))}
          </div>
          {filteredMenuDishes.length === 0 ? (
            <div className="kanban-empty-state chef-menu-empty">
              <i className="bi bi-journal-x" />
              <p>Không có món nào phù hợp với bộ lọc hiện tại.</p>
            </div>
          ) : null}
        </section>
      ) : null}

      {ingredientEditor ? (
        <section className="modal-backdrop" onClick={() => setIngredientEditor(null)}>
          <div className="modal-card chef-modal-card chef-compact-modal chef-ingredients-modal" onClick={(e) => e.stopPropagation()}>
            <div className="panel-head chef-modal-head">
              <div>
                <h2>Thành phần: {ingredientEditor.dishName}</h2>
                <p className="muted">Xem định lượng nguyên liệu cho món trong đơn hiện tại.</p>
              </div>
            </div>
            <div className="chef-ingredients-scroll">
              {ingredientEditor.customerNote?.trim() ? (
                <div className="inline-filter-card chef-modal-section">
                  <div>
                    <strong>Ghi chú từ khách hàng</strong>
                    <div className="muted">{ingredientEditor.customerNote}</div>
                  </div>
                  <span className="soft-badge warning">Cần lưu ý khi chế biến</span>
                </div>
              ) : null}
              <div className="inline-filter-card chef-modal-section">
                <div>
                  <strong>Công thức món</strong>
                  <div className="muted">Mặc định lấy từ công thức Catalog. Nếu chỉnh sửa tại đây, thay đổi chỉ áp dụng cho món trong đơn này.</div>
                </div>
                <div className="chef-chip-row">
                  <span className="soft-badge info">{ingredientEditor.items.length} nguyên liệu</span>
                  <span className="soft-badge success">{ingredientEditor.items.filter((item) => item.isActive).length} đang hoạt động</span>
                  {ingredientEditor.items.some((item) => item.isOverridden) ? <span className="soft-badge warning">Có điều chỉnh riêng</span> : null}
                </div>
              </div>
              <div className="ingredient-editor">
                {ingredientEditor.items.map((item) => {
                  const draft = ingredientEditor.draftItems?.find((row) => row.ingredientId === item.ingredientId);
                  return (
                    <label key={item.ingredientId} className={`ingredient-line ${item.isOverridden ? "ingredient-overridden" : ""}`}>
                      <div className="ingredient-meta">
                        <span>{item.name} ({item.unit})</span>
                        <small>Tồn kho: {item.currentStock.toLocaleString("vi-VN")} {item.unit}</small>
                        {item.isOverridden ? <small>Đã chỉnh riêng cho món trong đơn này. Mặc định: {item.defaultQuantityPerDish.toLocaleString("vi-VN")} {item.unit}</small> : null}
                      </div>
                      {ingredientEditor.editMode ? (
                        <input
                          type="number"
                          min="0"
                          step="0.01"
                          value={draft?.quantity ?? String(item.quantityPerDish)}
                          onChange={(event) => setIngredientEditor((current) => current ? {
                            ...current,
                            draftItems: (current.draftItems ?? []).map((row) => row.ingredientId === item.ingredientId ? { ...row, quantity: event.target.value } : row),
                          } : current)}
                        />
                      ) : (
                        <span className={item.isOverridden ? "soft-badge warning" : "soft-badge info"}>
                          {item.quantityPerDish.toLocaleString("vi-VN")} {item.unit}
                        </span>
                      )}
                    </label>
                  );
                })}
              </div>
              {ingredientEditor.editMode ? (
                <label className="stack compact chef-modal-section">
                  <span>Ghi chú lý do chỉnh sửa</span>
                  <textarea rows={3} value={ingredientEditor.editNote ?? ""} onChange={(event) => setIngredientEditor({ ...ingredientEditor, editNote: event.target.value })} placeholder="Ví dụ: Khách yêu cầu ít đường hơn..." />
                </label>
              ) : null}
            </div>
            <div className="header-actions chef-modal-actions">
              {ingredientEditor.orderId && ingredientEditor.itemId && !ingredientEditor.editMode ? (
                <button className="ghost" onClick={() => setIngredientEditor({ ...ingredientEditor, editMode: true })}>Chỉnh sửa thành phần cho đơn này</button>
              ) : null}
              {ingredientEditor.editMode ? (
                <button onClick={() => void saveOrderItemIngredientOverrides()}>Lưu thành phần đơn này</button>
              ) : null}
              <button className="ghost" onClick={() => setIngredientEditor(null)}>{ingredientEditor.editMode ? "Hủy" : "Đóng"}</button>
            </div>
          </div>
        </section>
      ) : null}

      {ingredientStockOpen ? (
        <section className="modal-backdrop" onClick={() => setIngredientStockOpen(false)}>
          <div className="modal-card chef-modal-card chef-stock-modal" onClick={(e) => e.stopPropagation()}>
            <div className="panel-head chef-modal-head">
              <div>
                <h2>Tồn kho nguyên liệu</h2>
                <p className="muted">Thông tin nguyên liệu hiện tại của {data.staff.branchName}</p>
              </div>
              <button className="ghost" onClick={() => setIngredientStockOpen(false)}>Đóng</button>
            </div>
            <div className="chef-modal-section">
              <div className="chef-inline-alert chef-inline-alert-info">
                Thông tin tồn kho hiện tại để bếp cân đối khi nhận đơn. Những nguyên liệu dưới ngưỡng sẽ được tô nổi.
              </div>
              <div className="chef-stock-filter-grid">
                <input
                  value={ingredientStockSearch}
                  onChange={(e) => setIngredientStockSearch(e.target.value)}
                  placeholder="Tìm kiếm theo tên nguyên liệu..."
                />
                <label className="checkbox-inline chef-stock-checkbox">
                  <input
                    type="checkbox"
                    checked={onlyLowStock}
                    onChange={(e) => setOnlyLowStock(e.target.checked)}
                  />
                  <span>Chỉ hiện nguyên liệu sắp hết</span>
                </label>
              </div>
            </div>
            <div className="table-scroll chef-stock-table-scroll">
              <table className="staff-table">
                <thead>
                  <tr>
                    <th>Tên nguyên liệu</th>
                    <th>Đơn vị</th>
                    <th>Tồn hiện tại</th>
                    <th>Ngưỡng cảnh báo</th>
                  </tr>
                </thead>
                <tbody>
                  {ingredientStockRows.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="muted table-empty">
                        Không có nguyên liệu phù hợp với bộ lọc đang chọn.
                      </td>
                    </tr>
                  ) : (
                    ingredientStockRows.map((item) => {
                      const isLowStock = item.reorderLevel > 0 && item.currentStock <= item.reorderLevel;
                      return (
                        <tr key={item.ingredientId} className={isLowStock ? "chef-stock-row-low" : undefined}>
                          <td>{item.name}</td>
                          <td>{item.unit}</td>
                          <td>{item.currentStock.toLocaleString("vi-VN")}</td>
                          <td>{item.reorderLevel.toLocaleString("vi-VN")}</td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </section>
      ) : null}

      {cancelEditor ? (
        <section className="modal-backdrop" onClick={() => setCancelEditor(null)}>
          <div className="modal-card chef-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="panel-head chef-modal-head">
              <div>
                <h2>Hủy đơn hàng</h2>
              </div>
              <button className="ghost" onClick={() => setCancelEditor(null)}>Đóng</button>
            </div>
            <div className="chef-modal-section">
              <div className="alert alert-warning small">
                Vui lòng nhập lý do hủy đơn. Thông tin này sẽ được lưu lại trong lịch sử đơn hàng.
              </div>
              <label className="stack compact">
                <span>Lý do hủy đơn</span>
                <textarea
                  rows={4}
                  maxLength={500}
                  value={cancelEditor.reason}
                  onChange={(e) => setCancelEditor({ ...cancelEditor, reason: e.target.value })}
                  placeholder="Ví dụ: Khách yêu cầu hủy, món hết nguyên liệu..."
                />
              </label>
            </div>
            <div className="header-actions chef-modal-actions">
              <button className="ghost" onClick={() => setCancelEditor(null)}>Đóng</button>
              <button onClick={() => void submitCancel()}>
                <i className="bi bi-x-circle me-1" />
                Xác nhận hủy
              </button>
            </div>
          </div>
        </section>
      ) : null}
      <Dialog />
    </main>
  );
}

type OrderColumnProps = {
  title: string;
  tone: "secondary" | "primary" | "success";
  orders: ChefOrderDto[];
  actionLabel?: string;
  action?: (orderId: number) => Promise<void>;
  secondaryLabel?: string;
  secondaryAction?: (orderId: number, orderCode: string) => void;
  onOpenIngredients: (dishId: number, customerNote?: string | null, orderId?: number, itemId?: number) => void;
  onStartItem?: (orderId: number, itemId: number) => Promise<void>;
  onReadyItem?: (orderId: number, itemId: number) => Promise<void>;
  onCancelItem?: (orderId: number, itemId: number, dishName: string) => Promise<void>;
};

function OrderColumn({
  title,
  tone,
  orders,
  actionLabel,
  action,
  secondaryLabel,
  secondaryAction,
  onOpenIngredients,
  onStartItem,
  onReadyItem,
  onCancelItem,
}: OrderColumnProps) {
  function formatOrderTime(value: string) {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return value;
    return date.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  }

  const iconClass =
    tone === "secondary"
      ? "bi bi-hourglass-split"
      : tone === "primary"
        ? "bi bi-fire"
        : "bi bi-bell";

  const emptyMessage =
    tone === "secondary"
      ? "Không có đơn chờ"
      : tone === "primary"
        ? "Không có đơn đang chế biến"
        : "Không có món chờ phục vụ";

  return (
    <section className={`panel order-column order-column-${tone} chef-kds-column`}>
      <div className={`kanban-header kanban-header-${tone}`}>
        <span className="kanban-title">
          <i className={iconClass} />
          {title}
        </span>
        <span className={`kanban-count kanban-count-${tone}`}>{orders.length}</span>
      </div>
      <div className="order-list chef-kds-order-list">
        {orders.length === 0 ? (
          <div className="kanban-empty-state">
            <i className="bi bi-check2-circle" />
            <p>{emptyMessage}</p>
          </div>
        ) : null}
        {orders.map((order) => (
          <article
            key={order.orderId}
            className={`order-card chef-kds-order-card ${
              tone === "primary" ? "priority-high" : tone === "success" ? "priority-success" : "priority-normal"
            }`}
          >
            <div className="order-header">
              <span className={`table-badge ${tone === "success" ? "table-badge-success" : ""}`}>
                {order.tableName || `Bàn ${order.tableId ?? "?"}`}
              </span>
              <span className="order-time">
                <i className="bi bi-clock me-1" />
                {formatOrderTime(order.orderTime)}
              </span>
            </div>
            <div className="order-code-label">#{order.orderCode || `ORD${order.orderId}`}</div>
            <div className="order-items">
              {order.items.map((item) => (
                <div key={item.itemId} className="order-item">
                  <div className="order-item-main">
                    <span className="item-qty">x{item.quantity}</span>
                    <div className="order-item-content">
                      <strong className="item-name">{item.dishName}</strong>
                      <div className="item-status-row">
                        <span className={getItemStatusClass(item.statusCode)}>{getItemStatusLabel(item.statusCode)}</span>
                      </div>
                      {item.note?.trim() ? (
                        <small className="item-note">
                          <i className="bi bi-chat-left-text" />
                          Ghi chú khách: {item.note}
                        </small>
                      ) : null}
                    </div>
                  </div>
                  <div className="item-action-panel">
                    <button
                      className="ghost note-action-button"
                      onClick={() => onOpenIngredients(item.dishId, item.note || "", order.orderId, item.itemId)}
                    >
                      Xem thành phần
                    </button>
                    <div className="item-status-actions">
                      {onStartItem && ["PENDING", "CONFIRMED"].includes(normalizeItemStatusCode(item.statusCode)) ? (
                        <button className="btn-action-primary" onClick={() => void onStartItem(order.orderId, item.itemId)}>
                          Bắt đầu
                        </button>
                      ) : null}
                      {onReadyItem && normalizeItemStatusCode(item.statusCode) === "PREPARING" ? (
                        <button className="btn-action-primary" onClick={() => void onReadyItem(order.orderId, item.itemId)}>
                          Hoàn thành
                        </button>
                      ) : null}
                      {onCancelItem && ["PENDING", "CONFIRMED", "PREPARING", "READY"].includes(normalizeItemStatusCode(item.statusCode)) ? (
                        <button className="btn-action-danger" onClick={() => void onCancelItem(order.orderId, item.itemId, item.dishName)}>
                          Hủy món
                        </button>
                      ) : null}
                    </div>
                  </div>
                </div>
              ))}
            </div>
            <div className="order-action-stack">
              {actionLabel && action ? (
                <button className="btn-action-primary" onClick={() => void action(order.orderId)}>
                  {actionLabel}
                </button>
              ) : null}
              {tone === "success" ? (
                <div className="order-ready-alert">
                  <i className="bi bi-bell-fill" />
                  <div>
                    <strong>Đã báo phục vụ</strong>
                  </div>
                </div>
              ) : null}
              {secondaryLabel && secondaryAction ? (
                <button
                  className="btn-action-danger"
                  onClick={() => secondaryAction(order.orderId, order.orderCode || `ORD${order.orderId}`)}
                >
                  {secondaryLabel}
                </button>
              ) : null}
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}
