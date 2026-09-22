import { useEffect, useMemo, useState } from "react";
import { HubConnectionBuilder, HubConnectionState } from "@microsoft/signalr";
import { Link } from "react-router-dom";
import { useAppDialog } from "../components/AppDialog";
import { cashierApi } from "../lib/api";
import { buildCashierTransferReference, buildCashierVietQrUrl, cashierQrBankInfo } from "../lib/vietQr";
import type { CashierCheckoutResultDto, CashierDashboardDto, CashierReservationCheckInResultDto, ReservationDto } from "../lib/types";

type Props = {
  onLogout: () => Promise<void>;
};

type CheckoutResultView = CashierCheckoutResultDto & {
  orderCode: string;
  tableName: string;
  customerName: string;
  cashierName: string;
  cashierRoleName: string;
  branchName: string;
  paidTime: string;
  subtotal: number;
  discount: number;
  pointsDiscount: number;
  items: CashierDashboardDto["orders"][number]["items"];
  paymentMethod: string;
  paymentAmount: number;
};

const CHECKOUT_KEY_PREFIX = "selfrestaurant.cashier.checkoutIntent:";
const reservationStatusLabels: Record<string, string> = {
  Pending: "Chờ xác nhận",
  Confirmed: "Đã xác nhận",
  CheckingIn: "Đang check-in",
  CheckedIn: "Đã check-in",
  Cancelled: "Đã hủy",
  NoShow: "Không đến",
  Completed: "Hoàn tất",
};

const reservationStatuses = [
  { value: "ALL", label: "Tất cả trạng thái" },
  { value: "Pending", label: "Chờ xác nhận" },
  { value: "Confirmed", label: "Đã xác nhận" },
  { value: "CheckingIn", label: "Đang check-in" },
  { value: "CheckedIn", label: "Đã check-in" },
  { value: "Cancelled", label: "Đã hủy" },
  { value: "NoShow", label: "Không đến" },
  { value: "Completed", label: "Hoàn tất" },
];

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

function paymentMethodLabel(value: string) {
  return value === "QR" ? "Chuyển khoản QR" : value === "CASH" ? "Tiền mặt" : value;
}

function itemStatusLabel(statusCode?: string | null) {
  const normalized = (statusCode ?? "").toUpperCase();
  if (normalized === "PREPARING") return "Đang chế biến";
  if (normalized === "READY") return "Sẵn sàng";
  if (normalized === "SERVING") return "Đang phục vụ";
  if (normalized === "CANCELLED") return "Đã hủy";
  if (normalized === "CONFIRMED") return "Đã gửi bếp";
  return "Chờ gửi";
}

function itemStatusBadgeClass(statusCode?: string | null) {
  const normalized = (statusCode ?? "").toUpperCase();
  if (normalized === "PREPARING") return "soft-badge warning";
  if (normalized === "READY") return "soft-badge success";
  if (normalized === "SERVING") return "soft-badge info";
  if (normalized === "CANCELLED") return "soft-badge danger";
  return "soft-badge secondary";
}

function reservationStatusBadgeClass(status?: string | null) {
  const normalized = (status ?? "").toUpperCase();
  if (normalized === "CONFIRMED") return "soft-badge success";
  if (normalized === "CHECKEDIN" || normalized === "COMPLETED") return "soft-badge info";
  if (normalized === "CANCELLED" || normalized === "NOSHOW") return "soft-badge danger";
  return "soft-badge warning";
}

function formatReservationTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat("vi-VN", { hour: "2-digit", minute: "2-digit", day: "2-digit", month: "2-digit" }).format(date);
}

function summarizePreOrderItems(reservation: ReservationDto) {
  const items = reservation.preOrderItems?.filter((item) => item.status !== "Cancelled") ?? [];
  if (items.length === 0) return "Chưa chọn món trước";
  return items
    .slice(0, 3)
    .map((item) => `${item.dishNameSnapshot} x${item.quantity}`)
    .join(", ") + (items.length > 3 ? ` +${items.length - 3} món` : "");
}

function getReservationTableIds(reservation: ReservationDto) {
  const assignedIds = reservation.assignedTables?.map((table) => table.tableId).filter((tableId) => tableId > 0) ?? [];
  if (assignedIds.length > 0) return Array.from(new Set(assignedIds));
  return reservation.tableId ? [reservation.tableId] : [];
}

export function DashboardPage({ onLogout }: Props) {
  const { alert, prompt, Dialog } = useAppDialog();
  const [dashboard, setDashboard] = useState<CashierDashboardDto | null>(null);
  const [reservations, setReservations] = useState<ReservationDto[]>([]);
  const [reservationsLoading, setReservationsLoading] = useState(false);
  const [reservationsError, setReservationsError] = useState<string | null>(null);
  const [reservationSearch, setReservationSearch] = useState("");
  const [reservationStatus, setReservationStatus] = useState("ALL");
  const [checkInTarget, setCheckInTarget] = useState<ReservationDto | null>(null);
  const [selectedCheckInTableIds, setSelectedCheckInTableIds] = useState<number[]>([]);
  const [checkInResult, setCheckInResult] = useState<CashierReservationCheckInResultDto | null>(null);
  const [checkInSubmitting, setCheckInSubmitting] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [tableSearch, setTableSearch] = useState("");
  const [activeTableId, setActiveTableId] = useState<number | null>(null);
  const [discountAmount, setDiscountAmount] = useState("0");
  const [discountPercent, setDiscountPercent] = useState("0");
  const [pointsUsedInput, setPointsUsedInput] = useState("0");
  const [paymentMethod, setPaymentMethod] = useState<"CASH" | "QR">("CASH");
  const [paymentAmount, setPaymentAmount] = useState("");
  const [checkoutResult, setCheckoutResult] = useState<CheckoutResultView | null>(null);
  const [checkoutSubmitting, setCheckoutSubmitting] = useState(false);

  function getCheckoutStorageKey(orderId: number) {
    return `${CHECKOUT_KEY_PREFIX}${orderId}`;
  }

  function buildCheckoutIntentKey() {
    if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
      return crypto.randomUUID();
    }

    return `checkout-${Date.now()}-${Math.random().toString(16).slice(2)}`;
  }

  function getOrCreateCheckoutIntentKey(orderId: number) {
    if (typeof window === "undefined" || typeof window.sessionStorage === "undefined") {
      return buildCheckoutIntentKey();
    }

    const storageKey = getCheckoutStorageKey(orderId);
    const existing = window.sessionStorage.getItem(storageKey);
    if (existing) {
      return existing;
    }

    const created = buildCheckoutIntentKey();
    window.sessionStorage.setItem(storageKey, created);
    return created;
  }

  function clearCheckoutIntentKey(orderId: number) {
    if (typeof window === "undefined" || typeof window.sessionStorage === "undefined") {
      return;
    }

    window.sessionStorage.removeItem(getCheckoutStorageKey(orderId));
  }

  async function loadDashboard(silent = false) {
    if (!silent) {
      setLoading(true);
    }
    setError(null);
    try {
      setDashboard(await cashierApi.getDashboard());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể tải dữ liệu thu ngân.");
    } finally {
      if (!silent) {
        setLoading(false);
      }
    }
  }

  async function loadReservations(branchId: number, status = reservationStatus) {
    setReservationsLoading(true);
    setReservationsError(null);
    try {
      setReservations(await cashierApi.getTodayReservations(branchId, status));
    } catch (err) {
      setReservationsError(err instanceof Error ? err.message : "Không thể tải danh sách đặt bàn hôm nay.");
    } finally {
      setReservationsLoading(false);
    }
  }

  useEffect(() => {
    void loadDashboard();
  }, []);

  useEffect(() => {
    const branchId = dashboard?.staff.branchId;
    if (!branchId) return;
    void loadReservations(branchId, reservationStatus);
  }, [dashboard?.staff.branchId, reservationStatus]);

  useEffect(() => {
    const branchId = dashboard?.staff.branchId;
    if (!branchId) return;

    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/customer-notifications")
      .withAutomaticReconnect()
      .build();
    const refreshDashboard = () => void loadDashboard(true);

    connection.on("cashierDashboardChanged", refreshDashboard);
    connection
      .start()
      .then(() => {
        if (connection.state === HubConnectionState.Connected) {
          return connection.invoke("SubscribeBranch", branchId);
        }
      })
      .catch((err) => {
        setError(err instanceof Error ? err.message : "Không thể kết nối cập nhật realtime thu ngân.");
      });

    return () => {
      connection.off("cashierDashboardChanged", refreshDashboard);
      if (connection.state === HubConnectionState.Connected) {
        void connection.invoke("UnsubscribeBranch", branchId).finally(() => void connection.stop());
        return;
      }
      void connection.stop();
    };
  }, [dashboard?.staff.branchId]);

  useEffect(() => {
    if (!dashboard) return;
    if (activeTableId != null && dashboard.tables.some((table) => table.tableId === activeTableId)) {
      return;
    }
    setActiveTableId(null);
  }, [activeTableId, dashboard]);

  useEffect(() => {
    setSelectedCheckInTableIds(checkInTarget ? getReservationTableIds(checkInTarget) : []);
  }, [checkInTarget]);

  const visibleTables = useMemo(() => {
    const keyword = tableSearch.trim().toLowerCase();
    return dashboard?.tables.filter((table) => keyword.length === 0 || table.number.toLowerCase().includes(keyword)) ?? [];
  }, [dashboard, tableSearch]);

  const selectedTable = useMemo(() => {
    if (!dashboard || activeTableId == null) return null;
    return dashboard.tables.find((table) => table.tableId === activeTableId) ?? null;
  }, [activeTableId, dashboard]);

  const activeOrder = useMemo(() => {
    if (!dashboard || !selectedTable?.orderId) return null;
    return dashboard.orders.find((order) => order.orderId === selectedTable.orderId) ?? null;
  }, [dashboard, selectedTable]);

  const visibleReservations = useMemo(() => {
    const keyword = reservationSearch.trim().toLowerCase();
    return reservations.filter((reservation) => {
      if (!keyword) return true;
      return [
        reservation.reservationCode,
        reservation.customerName,
        reservation.phoneNumber,
        reservation.note ?? "",
      ].some((value) => value.toLowerCase().includes(keyword));
    });
  }, [reservationSearch, reservations]);

  const availableCheckInTables = useMemo(
    () => dashboard?.tables.filter((table) => table.status === "AVAILABLE" || selectedCheckInTableIds.includes(table.tableId)) ?? [],
    [dashboard, selectedCheckInTableIds],
  );

  const primaryCheckInTableId = selectedCheckInTableIds[0] ?? null;

  const subtotal = activeOrder?.subtotal ?? 0;
  const pointsAvailable = activeOrder?.customerCreditPoints ?? 0;

  const checkoutPreview = useMemo(() => {
    const rawDiscount = Number(discountAmount || "0");
    const safeDiscount = Number.isFinite(rawDiscount) ? clamp(rawDiscount, 0, subtotal) : 0;
    const baseTotal = Math.max(0, subtotal - safeDiscount);
    const maxPointsAllowed = Math.floor(Math.min(pointsAvailable, Math.floor(baseTotal * 0.1)) / 1000) * 1000;
    const rawPoints = Number(pointsUsedInput || "0");
    const safePoints = Number.isFinite(rawPoints) ? Math.floor(clamp(rawPoints, 0, maxPointsAllowed) / 1000) * 1000 : 0;
    const total = Math.max(0, subtotal - safeDiscount - safePoints);
    const rawPayment = Number(paymentAmount || "0");
    const safePayment = Number.isFinite(rawPayment) ? Math.max(0, rawPayment) : 0;
    const effectivePayment = paymentMethod === "QR" ? total : safePayment;
    const changeAmount = Math.max(0, effectivePayment - total);

    return {
      discount: safeDiscount,
      maxPointsAllowed,
      pointsUsed: safePoints,
      total,
      paymentAmount: effectivePayment,
      changeAmount,
    };
  }, [discountAmount, paymentAmount, paymentMethod, pointsAvailable, pointsUsedInput, subtotal]);

  useEffect(() => {
    if (!activeOrder) {
      setDiscountAmount("0");
      setDiscountPercent("0");
      setPointsUsedInput("0");
      setPaymentMethod("CASH");
      setPaymentAmount("");
      return;
    }

    setDiscountAmount("0");
    setDiscountPercent("0");
    setPointsUsedInput("0");
    setPaymentMethod("CASH");
    setPaymentAmount("");
  }, [activeOrder?.orderId]);

  useEffect(() => {
    if (paymentMethod === "QR") {
      setPaymentAmount(String(checkoutPreview.total));
    }
  }, [checkoutPreview.total, paymentMethod]);

  const qrTransferReference = useMemo(
    () => buildCashierTransferReference(activeOrder?.orderCode, activeOrder?.orderId),
    [activeOrder?.orderCode, activeOrder?.orderId],
  );

  const qrImageUrl = useMemo(
    () => buildCashierVietQrUrl({ amount: checkoutPreview.total, orderCode: activeOrder?.orderCode, orderId: activeOrder?.orderId }),
    [activeOrder?.orderCode, activeOrder?.orderId, checkoutPreview.total],
  );

  async function copyText(value: string, successMessage: string) {
    try {
      if (typeof navigator !== "undefined" && navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(value);
      } else {
        await prompt({
          title: "Sao chép nội dung",
          message: "Sao chép nội dung bên dưới:",
          defaultValue: value,
          confirmLabel: "Đồng ý",
          cancelLabel: "Hủy",
          multiline: true,
        });
      }

      setMessage(successMessage);
    } catch {
      await prompt({
        title: "Sao chép nội dung",
        message: "Sao chép nội dung bên dưới:",
        defaultValue: value,
        confirmLabel: "Đồng ý",
        cancelLabel: "Hủy",
        multiline: true,
      });
      setMessage(successMessage);
    }
  }

  function handleDiscountAmountChange(value: string) {
    setDiscountAmount(value);
    const numericValue = clamp(Number(value || "0") || 0, 0, subtotal);
    const percent = subtotal > 0 ? (numericValue / subtotal) * 100 : 0;
    setDiscountPercent(percent.toFixed(2).replace(/\.00$/, ""));
  }

  function handleDiscountPercentChange(value: string) {
    const normalizedPercent = clamp(Number(value || "0") || 0, 0, 100);
    setDiscountPercent(value);
    setDiscountAmount(String(Math.floor(subtotal * (normalizedPercent / 100))));
  }

  function useSmartPoints() {
    setPointsUsedInput(String(checkoutPreview.maxPointsAllowed));
  }

  function clearPoints() {
    setPointsUsedInput("0");
  }

  function setQuickAmount(amount: number) {
    setPaymentAmount(String(amount));
  }

  function setExactAmount() {
    setPaymentAmount(String(checkoutPreview.total));
  }

  function clearAmount() {
    setPaymentAmount("");
  }

  async function processCheckout() {
    if (!dashboard || !activeOrder || !selectedTable || checkoutSubmitting) return;
    setError(null);
    setMessage(null);

    if (paymentMethod === "CASH" && checkoutPreview.paymentAmount < checkoutPreview.total) {
      await alert({
        title: "Không đủ tiền",
        message: "Không đủ tiền thanh toán hóa đơn.",
        confirmLabel: "Đồng ý",
        variant: "danger",
      });
      return;
    }

    try {
      setCheckoutSubmitting(true);
      const invoiceSnapshot = {
        orderCode: activeOrder.orderCode,
        tableName: selectedTable.number,
        customerName: activeOrder.customerName,
        cashierName: dashboard.staff.name,
        cashierRoleName: dashboard.staff.roleName,
        branchName: dashboard.staff.branchName,
        paidTime: new Date().toISOString(),
        subtotal: activeOrder.subtotal,
        discount: checkoutPreview.discount,
        pointsDiscount: checkoutPreview.pointsUsed,
        items: activeOrder.items.map((item) => ({ ...item })),
        paymentMethod,
        paymentAmount: checkoutPreview.paymentAmount,
      };
      const result = await cashierApi.checkout(activeOrder.orderId, {
        discount: checkoutPreview.discount,
        pointsUsed: checkoutPreview.pointsUsed,
        paymentMethod,
        paymentAmount: checkoutPreview.paymentAmount,
        idempotencyKey: getOrCreateCheckoutIntentKey(activeOrder.orderId),
      });

      setCheckoutResult({
        ...result,
        ...invoiceSnapshot,
        customerName: result.customerName ?? invoiceSnapshot.customerName,
      });
      setMessage(result.message);
      clearCheckoutIntentKey(activeOrder.orderId);
      await loadDashboard(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể thanh toán hóa đơn.");
    } finally {
      setCheckoutSubmitting(false);
    }
  }

  function toggleCheckInTable(tableId: number) {
    setSelectedCheckInTableIds((current) => current.includes(tableId) ? current.filter((id) => id !== tableId) : [...current, tableId]);
  }

  function removeCheckInTable(tableId: number) {
    setSelectedCheckInTableIds((current) => current.filter((id) => id !== tableId));
  }

  async function confirmReservationCheckIn() {
    if (!checkInTarget || checkInSubmitting) return;
    const primaryTableId = selectedCheckInTableIds[0] ?? null;
    if (!primaryTableId) {
      setError("Vui lòng chọn ít nhất một bàn trước khi check-in.");
      return;
    }
    setCheckInSubmitting(true);
    setError(null);
    setMessage(null);
    try {
      const result = await cashierApi.checkInReservation(checkInTarget.reservationId, { tableId: primaryTableId, tableIds: selectedCheckInTableIds });
      setCheckInResult(result);
      setCheckInTarget(null);
      setMessage(result.message || "Đã check-in và tạo đơn hàng thành công.");
      await loadDashboard(true);
      if (dashboard?.staff.branchId) {
        await loadReservations(dashboard.staff.branchId, reservationStatus);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể check-in đặt bàn.");
    } finally {
      setCheckInSubmitting(false);
    }
  }

  async function closeCheckoutResult() {
    setCheckoutResult(null);
    setActiveTableId(null);
    await loadDashboard(true);
  }

  function printCheckoutResult() {
    window.print();
  }

  if (loading) return <div className="screen-message">Đang tải quầy thu ngân...</div>;
  if (error && !dashboard) return <div className="screen-message error-box">{error}</div>;
  if (!dashboard) return null;

  const occupiedCount = dashboard.tables.filter((table) => table.status === "OCCUPIED").length;
  const selectedTableTitle = selectedTable ? selectedTable.number : "Chưa chọn bàn";
  const canCheckout = !!activeOrder;

  return (
    <main className="cashier-shell cashier-pos-shell">
      <section className="hero-card cashier-hero cashier-pos-hero">
        <header className="cashier-header">
          <div>
            <h1>Quầy Thu Ngân</h1>
            <p className="muted">{dashboard.staff.branchName}</p>
            <p className="muted">Nhân viên thu ngân: {dashboard.staff.name} ({dashboard.staff.roleName})</p>
          </div>
          <div className="header-actions">
            <Link className="cashier-link-button" to="/Staff/Cashier/Report">
              <i className="bi bi-printer me-1" />
              Báo cáo theo ngày
            </Link>
            <Link className="cashier-link-button" to="/Staff/Cashier/History">
              <i className="bi bi-clock-history me-1" />
              Lịch sử & Tài khoản
            </Link>
            <button className="ghost" onClick={() => void onLogout()}>
              <i className="bi bi-box-arrow-right me-1" />
              Đăng xuất
            </button>
          </div>
        </header>

        <section className="stats-grid cashier-pos-stats">
          <article>
            <strong>{dashboard.todayOrders}</strong>
            <span>Đơn hôm nay</span>
          </article>
          <article>
            <strong>{dashboard.todayRevenue.toLocaleString("vi-VN")} đ</strong>
            <span>Doanh thu</span>
          </article>
        </section>
      </section>

      {message ? <div className="success-box">{message}</div> : null}
      {error ? <div className="error-box">{error}</div> : null}

      <section className="panel cashier-panel-card cashier-reservation-panel">
        <div className="cashier-panel-header">
          <div>
            <h2>
              <i className="bi bi-calendar-check me-2" />
              Đặt bàn hôm nay
            </h2>
            <p className="muted">Theo dõi khách đã đặt bàn và món đặt trước trong ngày.</p>
          </div>
          <button
            type="button"
            className="cashier-link-button cashier-link-button-outline"
            onClick={() => void loadReservations(dashboard.staff.branchId, reservationStatus)}
            disabled={reservationsLoading}
          >
            <i className="bi bi-arrow-clockwise me-1" />
            Làm mới
          </button>
        </div>

        <div className="cashier-panel-body">
          <div className="reservation-filter-bar">
            <div className="table-search-group">
              <i className="bi bi-search" />
              <input
                type="text"
                value={reservationSearch}
                onChange={(event) => setReservationSearch(event.target.value)}
                placeholder="Tìm mã đặt bàn, tên khách hoặc số điện thoại..."
              />
            </div>
            <select value={reservationStatus} onChange={(event) => setReservationStatus(event.target.value)}>
              {reservationStatuses.map((status) => (
                <option key={status.value} value={status.value}>{status.label}</option>
              ))}
            </select>
          </div>

          {reservationsLoading ? (
            <div className="cashier-empty-state reservation-empty-state">
              <i className="bi bi-hourglass-split" />
              <p>Đang tải đặt bàn hôm nay...</p>
            </div>
          ) : null}

          {reservationsError ? <div className="error-box">{reservationsError}</div> : null}

          {!reservationsLoading && !reservationsError && visibleReservations.length === 0 ? (
            <div className="cashier-empty-state reservation-empty-state">
              <i className="bi bi-calendar2-x" />
              <p>Chưa có đặt bàn nào hôm nay.</p>
            </div>
          ) : null}

          {!reservationsLoading && !reservationsError && visibleReservations.length > 0 ? (
            <div className="cashier-reservation-grid">
              {visibleReservations.map((reservation) => {
                const itemCount = reservation.preOrderItems?.filter((item) => item.status !== "Cancelled").length ?? 0;
                const tableIds = getReservationTableIds(reservation);
                const primaryAssignedTable = reservation.assignedTables?.find((assignedTable) => assignedTable.isPrimary)?.tableId ?? reservation.tableId ?? tableIds[0];
                const tableText = tableIds.length > 0
                  ? tableIds
                      .map((tableId) => {
                        const table = dashboard.tables.find((candidate) => candidate.tableId === tableId);
                        const label = table?.number ?? `Bàn #${tableId}`;
                        return tableId === primaryAssignedTable ? `${label} (bàn chính)` : label;
                      })
                      .join(", ")
                  : "Chưa gán bàn";

                return (
                  <article key={reservation.reservationId} className="cashier-reservation-card">
                    <div className="reservation-card-topline">
                      <strong>{reservation.reservationCode}</strong>
                      <span className={reservationStatusBadgeClass(reservation.status)}>
                        {reservationStatusLabels[reservation.status] ?? reservation.status}
                      </span>
                    </div>
                    <div className="reservation-main-info">
                      <div>
                        <span>Khách hàng</span>
                        <strong>{reservation.customerName}</strong>
                      </div>
                      <div>
                        <span>Điện thoại</span>
                        <strong>{reservation.phoneNumber}</strong>
                      </div>
                      <div>
                        <span>Số khách</span>
                        <strong>{reservation.partySize} khách</strong>
                      </div>
                      <div>
                        <span>Thời gian</span>
                        <strong>{formatReservationTime(reservation.reservedAt)}</strong>
                      </div>
                    </div>
                    <div className="reservation-meta-row">
                      <span><i className="bi bi-shop me-1" />{dashboard.staff.branchName}</span>
                      <span><i className="bi bi-table me-1" />{tableText}</span>
                      {reservation.partySize > 4 ? <span className="soft-badge warning"><i className="bi bi-people me-1" />Cần sắp xếp/ghép bàn</span> : null}
                    </div>
                    {reservation.note ? <p className="reservation-note">Ghi chú: {reservation.note}</p> : null}
                    <div className="reservation-preorder-summary">
                      <span className="soft-badge info">{itemCount} món đặt trước</span>
                      <p>{summarizePreOrderItems(reservation)}</p>
                    </div>
                    <button
                      type="button"
                      className="cashier-button-primary reservation-checkin-placeholder"
                      disabled={!["Pending", "Confirmed"].includes(reservation.status) || checkInSubmitting}
                      onClick={() => setCheckInTarget(reservation)}
                      title={["Pending", "Confirmed"].includes(reservation.status) ? "Check-in đặt bàn" : "Đặt bàn không còn ở trạng thái nhận khách"}
                    >
                      <i className="bi bi-person-check me-1" />
                      Nhận khách
                    </button>
                    <small className="muted">Check-in sẽ tạo đơn bếp từ món đặt trước nếu có.</small>
                  </article>
                );
              })}
            </div>
          ) : null}
        </div>
      </section>

      <section className="split-grid cash-grid cashier-pos-grid">
        <div className="panel cashier-panel-card cashier-pos-panel cashier-table-panel">
          <div className="cashier-panel-header">
            <h2>
              <i className="bi bi-table me-2" />
              Chọn bàn
            </h2>
          </div>
          <div className="cashier-panel-body">
            <div className="table-search-group">
              <i className="bi bi-search" />
              <input
                type="text"
                value={tableSearch}
                onChange={(event) => setTableSearch(event.target.value)}
                placeholder="Nhập số bàn cần tìm..."
              />
            </div>
            <div className="cashier-status-summary">
              <span className="soft-badge success">{dashboard.tables.length - occupiedCount} bàn trống</span>
              <span className="soft-badge warning">{occupiedCount} bàn đang sử dụng</span>
            </div>

            <div className="table-grid">
              {visibleTables.length > 0 ? (
                visibleTables.map((table) => (
                  <article
                    key={table.tableId}
                    className={`table-card cashier-table-card ${table.status.toLowerCase()} ${selectedTable?.tableId === table.tableId ? "selected" : ""}`}
                    onClick={() => setActiveTableId(table.tableId)}
                  >
                    <div className="table-number">{table.number}</div>
                    <div className={`table-status ${table.status === "AVAILABLE" ? "available" : "occupied"}`}>
                      {table.status === "AVAILABLE" ? "Trống" : "Có khách"}
                    </div>
                    <small className="muted">{table.seats} ghế</small>
                  </article>
                ))
              ) : (
                <div className="cashier-empty-state">
                  <i className="bi bi-search" />
                  <p>Không tìm thấy bàn</p>
                </div>
              )}
            </div>
          </div>
        </div>

        <div className="panel cashier-panel-card cashier-pos-panel cashier-order-panel">
          <div className="cashier-panel-header">
            <h2>
              <i className="bi bi-receipt me-2" />
              Chi tiết đơn hàng
            </h2>
            <span className="soft-badge info">{selectedTableTitle}</span>
          </div>
          <div className="cashier-panel-body cashier-panel-body-scroll">
            {!selectedTable ? (
              <div className="cashier-empty-state">
                <i className="bi bi-cart-x" />
                <p>Chọn bàn để xem đơn hàng</p>
              </div>
            ) : !activeOrder ? (
              <div className="cashier-empty-state">
                <i className="bi bi-info-circle" />
                <p>Bàn này chưa có đơn hàng</p>
              </div>
            ) : (
              <>
                <div className="inline-filter-card cashier-order-summary-card cashier-pos-order-summary">
                  <div>
                    <strong>{activeOrder.orderCode}</strong>
                    <div className="muted">{activeOrder.customerName || "Khách lẻ"}</div>
                  </div>
                  <span className="soft-badge info">{activeOrder.statusName}</span>
                </div>

                <div className="detail-list">
                  {activeOrder.items.map((item, index) => (
                    <article key={`${activeOrder.orderId}-${index}`} className="detail-item-card cashier-pos-item-card">
                      <div className="d-flex" style={{ gap: "1rem" }}>
                        {item.image ? (
                          <img
                            src={item.image}
                            alt={item.dishName}
                            style={{ width: "60px", height: "60px", objectFit: "cover", borderRadius: "8px", border: "1px solid #e5e7eb" }}
                          />
                        ) : null}
                        <div>
                          <strong>{item.dishName}</strong>
                          <p>Số lượng: {item.quantity} x {item.unitPrice.toLocaleString("vi-VN")} đ</p>
                          <span className={itemStatusBadgeClass(item.statusCode)}>{itemStatusLabel(item.statusCode)}</span>
                        </div>
                      </div>
                      <strong>{item.lineTotal.toLocaleString("vi-VN")} đ</strong>
                    </article>
                  ))}
                </div>
              </>
            )}
          </div>
        </div>

        <div className="panel cashier-panel-card cashier-pos-panel cashier-payment-panel">
          <div className="cashier-panel-header">
            <h2>
              <i className="bi bi-credit-card me-2" />
              Thanh toán
            </h2>
          </div>
          <div className="cashier-panel-body">
            <div className="total-section">
              <div className="total-row">
                <span>Tạm tính:</span>
                <strong>{subtotal.toLocaleString("vi-VN")} đ</strong>
              </div>
              <div className="total-row">
                <span>Giảm giá:</span>
                <div className="d-flex align-items-center" style={{ gap: ".5rem" }}>
                  <input
                    type="number"
                    min="0"
                    value={discountAmount}
                    onChange={(event) => handleDiscountAmountChange(event.target.value)}
                    style={{ width: "120px" }}
                  />
                  <span className="muted"></span>
                </div>
              </div>
              <div className="total-row">
                <span>Giảm theo %:</span>
                <div className="d-flex align-items-center" style={{ gap: ".5rem" }}>
                  <input
                    type="number"
                    min="0"
                    max="100"
                    step="0.01"
                    value={discountPercent}
                    onChange={(event) => handleDiscountPercentChange(event.target.value)}
                    style={{ width: "120px" }}
                  />
                  <span className="muted">%</span>
                </div>
              </div>
              {checkoutPreview.pointsUsed > 0 ? (
                <div className="total-row">
                  <span>Gim bng im:</span>
                  <strong>-{checkoutPreview.pointsUsed.toLocaleString("vi-VN")} đ</strong>
                </div>
              ) : null}
              <div className="total-row grand-total">
                <span>TỔNG CỘNG:</span>
                <span>{checkoutPreview.total.toLocaleString("vi-VN")} đ</span>
              </div>
            </div>

            {activeOrder?.customerId ? (
              <div className="credit-points-section">
                <div className="credit-balance">
                  <span>Số dư hiện tại:</span>
                  <span className="credit-amount">{pointsAvailable.toLocaleString("vi-VN")} điểm</span>
                </div>
                <label>
                  Sử dụng điểm (1 điểm = 1 đ):
                  <input
                    className="points-input"
                    type="number"
                    min="0"
                    max={checkoutPreview.maxPointsAllowed}
                    value={pointsUsedInput}
                    onChange={(event) => setPointsUsedInput(event.target.value)}
                  />
                </label>
                <div className="checkout-quick-grid">
                  <button className="cashier-button-primary checkout-quick-button" onClick={useSmartPoints}>
                    Thanh toán thông minh
                  </button>
                  <button className="cashier-button-outline checkout-quick-button" onClick={clearPoints}>
                    Xóa điểm
                  </button>
                </div>
                {checkoutPreview.maxPointsAllowed > 0 ? (
                  <div className="soft-badge warning">
                    Trừ tối đa {checkoutPreview.maxPointsAllowed.toLocaleString("vi-VN")} điểm cho hóa đơn này
                  </div>
                ) : null}
              </div>
            ) : null}

            <div className="stack">
              <label>Phương thức thanh toán:</label>
              <div className="payment-method-grid">
                <button
                  className={`payment-method-tile ${paymentMethod === "CASH" ? "active" : ""}`}
                  onClick={() => setPaymentMethod("CASH")}
                >
                  <i className="bi bi-cash-stack" />
                  <strong>Tiền mặt</strong>
                </button>
                <button
                  className={`payment-method-tile ${paymentMethod === "QR" ? "active" : ""}`}
                  onClick={() => setPaymentMethod("QR")}
                >
                  <i className="bi bi-qr-code" />
                  <strong>QR chuyển khoản</strong>
                  <small>BIDV - in sẵn số tiền</small>
                </button>
              </div>
            </div>

            {paymentMethod === "CASH" ? (
              <div className="stack">
                <label>
                  Tiền khách đưa:
                  <input
                    className="payment-input"
                    type="number"
                    min="0"
                    value={paymentAmount}
                    onChange={(event) => setPaymentAmount(event.target.value)}
                    placeholder="0"
                  />
                </label>
                <div className="checkout-quick-grid">
                  <button className="cashier-button-outline checkout-quick-button" onClick={() => setQuickAmount(50000)}>50k</button>
                  <button className="cashier-button-outline checkout-quick-button" onClick={() => setQuickAmount(100000)}>100k</button>
                  <button className="cashier-button-outline checkout-quick-button" onClick={() => setQuickAmount(200000)}>200k</button>
                  <button className="cashier-button-outline checkout-quick-button" onClick={() => setQuickAmount(500000)}>500k</button>
                  <button className="cashier-button-outline checkout-quick-button" onClick={setExactAmount}>Vừa đủ</button>
                  <button className="cashier-button-outline checkout-quick-button" onClick={clearAmount}>Xóa</button>
                </div>
                {checkoutPreview.changeAmount > 0 ? (
                  <div className="success-box">
                    <strong>Tiền thối lại:</strong> {checkoutPreview.changeAmount.toLocaleString("vi-VN")} đ
                  </div>
                ) : null}
              </div>
            ) : (
              <div className="qr-payment-panel">
                <div className="qr-payment-preview">
                  <img src={qrImageUrl} alt="Mã QR chuyển khoản BIDV" />
                </div>
                <div className="qr-payment-details">
                  <div className="qr-payment-summary">
                    <span>Ngân hàng</span>
                    <strong>{cashierQrBankInfo.bankName}</strong>
                  </div>
                  <div className="qr-payment-summary">
                    <span>Số tài khoản</span>
                    <strong>{cashierQrBankInfo.accountNumber}</strong>
                  </div>
                  <div className="qr-payment-summary">
                    <span>Số tiền</span>
                    <strong>{checkoutPreview.total.toLocaleString("vi-VN")} đ</strong>
                  </div>
                  <div className="qr-payment-summary qr-payment-reference">
                    <span>Nội dung chuyển khoản</span>
                    <strong>{qrTransferReference}</strong>
                  </div>
                  <div className="qr-payment-actions">
                    <button className="cashier-button-outline" onClick={() => void copyText(cashierQrBankInfo.accountNumber, "Đã sao chép số tài khoản.")}>
                      <i className="bi bi-copy me-1" />
                      Sao chép STK
                    </button>
                    <button className="cashier-button-outline" onClick={() => void copyText(qrTransferReference, "Đã sao chép nội dung chuyển khoản.")}>
                      <i className="bi bi-copy me-1" />
                      Sao chép nội dung
                    </button>
                  </div>
                  <div className="cashier-info-note qr-payment-note">
                    Quét mã để mở ứng dụng ngân hàng với BIDV, số tài khoản, số tiền và nội dung chuyển khoản đã điền sẵn. Hóa đơn chỉ được ghi nhận sau khi thu ngân xác nhận đã nhận thanh toán.
                  </div>
                </div>
              </div>
            )}

            <button className="cashier-button-primary wide" disabled={!canCheckout || checkoutSubmitting} onClick={() => void processCheckout()}>
              <i className="bi bi-check-circle me-2" />
              {checkoutSubmitting ? "Đang xử lý..." : "Xác nhận thanh toán"}
            </button>
          </div>
        </div>
      </section>

      {checkInTarget ? (
        <div className="modal-backdrop">
          <div className="modal-card cashier-modal-card reservation-checkin-modal">
            <div className="cashier-panel-header">
              <h2>
                <i className="bi bi-person-check-fill me-2" />
                Xác nhận nhận khách
              </h2>
              <button className="ghost" onClick={() => setCheckInTarget(null)} disabled={checkInSubmitting}>
                Đóng
              </button>
            </div>
            <div className="cashier-panel-body">
              <div className="bill-meta-grid">
                <div className="bill-meta-card">
                  <span>Mã đặt bàn</span>
                  <strong>{checkInTarget.reservationCode}</strong>
                </div>
                <div className="bill-meta-card">
                  <span>Khách hàng</span>
                  <strong>{checkInTarget.customerName}</strong>
                </div>
                <div className="bill-meta-card">
                  <span>Số khách</span>
                  <strong>{checkInTarget.partySize} khách</strong>
                </div>
                <div className="bill-meta-card">
                  <span>Thời gian</span>
                  <strong>{formatReservationTime(checkInTarget.reservedAt)}</strong>
                </div>
                <div className="bill-meta-card">
                  <span>Bàn</span>
                  <strong>
                    {selectedCheckInTableIds.length > 0
                      ? selectedCheckInTableIds.map((tableId) => dashboard.tables.find((table) => table.tableId === tableId)?.number ?? `Bàn #${tableId}`).join(", ")
                      : "Chưa gán bàn"}
                  </strong>
                </div>
                <div className="bill-meta-card">
                  <span>Trạng thái</span>
                  <strong>{reservationStatusLabels[checkInTarget.status] ?? checkInTarget.status}</strong>
                </div>
              </div>

              <div className="reservation-modal-preorder">
                <h3>Món đặt trước</h3>
                {checkInTarget.preOrderItems.filter((item) => item.status === "Pending").length > 0 ? (
                  <div className="order-list compact-list">
                    {checkInTarget.preOrderItems.filter((item) => item.status === "Pending").map((item) => (
                      <div className="order-line" key={item.reservationItemId}>
                        <div>
                          <strong>{item.dishNameSnapshot}</strong>
                          <span>{item.quantity} x {item.unitPriceSnapshot.toLocaleString("vi-VN")} đ{item.note ? ` • ${item.note}` : ""}</span>
                        </div>
                        <strong>{(item.quantity * item.unitPriceSnapshot).toLocaleString("vi-VN")} đ</strong>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="cashier-info-note">Đặt bàn chưa có món đặt trước. Hệ thống sẽ chỉ đánh dấu đã check-in.</div>
                )}
              </div>

              <div className="reservation-table-picker">
                  <h3>Chọn bàn ghép</h3>
                  <p className="cashier-info-note">Nhóm đông người có thể được ghép nhiều bàn. Bàn chính sẽ dùng để tạo đơn và hóa đơn.</p>
                  {selectedCheckInTableIds.length > 0 ? (
                    <div className="reservation-selected-table-row">
                      {selectedCheckInTableIds.map((tableId, index) => (
                        <span className="reservation-selected-table-chip" key={tableId}>
                          {dashboard.tables.find((table) => table.tableId === tableId)?.number ?? `Bàn #${tableId}`}
                          {index === 0 ? <em>Bàn chính</em> : null}
                          <button type="button" onClick={() => removeCheckInTable(tableId)} disabled={checkInSubmitting} aria-label="Bỏ chọn bàn">×</button>
                        </span>
                      ))}
                    </div>
                  ) : null}
                  {availableCheckInTables.length > 0 ? (
                    <div className="reservation-table-grid">
                      {availableCheckInTables.map((table) => (
                        <button
                          type="button"
                          key={table.tableId}
                          className={`reservation-table-option ${selectedCheckInTableIds.includes(table.tableId) ? "selected" : ""}`}
                          onClick={() => toggleCheckInTable(table.tableId)}
                          disabled={checkInSubmitting}
                        >
                          <strong>{table.number}</strong>
                          <span>{table.seats} chỗ</span>
                          {primaryCheckInTableId === table.tableId ? <small>Bàn chính</small> : null}
                        </button>
                      ))}
                    </div>
                  ) : (
                    <div className="cashier-info-note danger">Không bàn nào đang xử lý.</div>
                  )}
                </div>

              <div className="cashier-info-note">
                Sau khi xác nhận, món đặt trước sẽ trở thành đơn hàng thật và hiển thị cho bếp.
              </div>

              <div className="modal-actions">
                <button className="cashier-button-outline" onClick={() => setCheckInTarget(null)} disabled={checkInSubmitting}>
                  Hủy
                </button>
                <button className="cashier-button-primary" onClick={() => void confirmReservationCheckIn()} disabled={checkInSubmitting || selectedCheckInTableIds.length === 0}>
                  {checkInSubmitting ? "Đang check-in..." : "Xác nhận nhận khách"}
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {checkoutResult ? (
        <div className="modal-backdrop">
          <div className="modal-card cashier-modal-card">
            <div className="cashier-panel-header">
              <h2>
                <i className="bi bi-check-circle-fill me-2" />
                Thanh toán thành công
              </h2>
              <button className="ghost" onClick={() => void closeCheckoutResult()}>
                Đóng
              </button>
            </div>
            <div className="cashier-panel-body">
              <div className="cashier-empty-state" style={{ minHeight: "unset", padding: "1rem" }}>
                <i className="bi bi-check-circle" style={{ color: "#198754" }} />
                <p>Đã thanh toán thành công!</p>
              </div>

              <div className="bill-meta-grid">
                <div className="bill-meta-card">
                  <span>Bàn</span>
                  <strong>{checkoutResult.tableName}</strong>
                </div>
                <div className="bill-meta-card">
                  <span>Mã đơn hàng</span>
                  <strong>{checkoutResult.orderCode}</strong>
                </div>
                <div className="bill-meta-card">
                  <span>Mã hóa đơn</span>
                  <strong>{checkoutResult.billCode}</strong>
                </div>
                <div className="bill-meta-card">
                  <span>Phương thức</span>
                  <strong>{paymentMethodLabel(checkoutResult.paymentMethod)}</strong>
                </div>
                <div className="bill-meta-card">
                  <span>Thời gian</span>
                  <strong>{new Date(checkoutResult.paidTime).toLocaleString("vi-VN")}</strong>
                </div>
                <div className="bill-meta-card">
                  <span>Thu ngân</span>
                  <strong>{checkoutResult.cashierName}</strong>
                </div>
              </div>

              <div className="order-list compact-list">
                {checkoutResult.items.map((item, index) => (
                  <div className="order-line" key={`${item.dishName}-${index}`}>
                    <div>
                      <strong>{item.dishName}</strong>
                      <span>
                        {item.quantity} x {item.unitPrice.toLocaleString("vi-VN")} đ
                      </span>
                    </div>
                    <strong>{item.lineTotal.toLocaleString("vi-VN")} đ</strong>
                  </div>
                ))}
              </div>

              <div className="bill-breakdown">
                <div>
                  <span>Tạm tính</span>
                  <strong>{checkoutResult.subtotal.toLocaleString("vi-VN")} đ</strong>
                </div>
                <div>
                  <span>Giảm giá</span>
                  <strong>{checkoutResult.discount.toLocaleString("vi-VN")} đ</strong>
                </div>
                <div>
                  <span>Điểm sử dụng</span>
                  <strong>{checkoutResult.pointsDiscount.toLocaleString("vi-VN")} đ</strong>
                </div>
                <div>
                  <span>Thành tiền</span>
                  <strong>{checkoutResult.totalAmount.toLocaleString("vi-VN")} đ</strong>
                </div>
                <div>
                  <span>Tiền khách đưa</span>
                  <strong>{checkoutResult.paymentAmount.toLocaleString("vi-VN")} đ</strong>
                </div>
                <div>
                  <span>Tiền thừa</span>
                  <strong>{checkoutResult.changeAmount.toLocaleString("vi-VN")} đ</strong>
                </div>
              </div>

              <div className="cashier-modal-footer">
                <button className="cashier-button-primary" onClick={printCheckoutResult}>
                  <i className="bi bi-printer me-2" />
                  In hóa đơn
                </button>
                <button className="cashier-button-outline" onClick={() => void closeCheckoutResult()}>
                  <i className="bi bi-check2-circle me-2" />
                  Hoàn tất
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}
      <Dialog />
    </main>
  );
}

