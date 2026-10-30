import { userMessage, cleanText } from "../../../shared/apiError";
import { formatViDateTime } from "../../../shared/dateFormat";
import { orderLabel } from "../../../shared/orderLabel";
import { useEffect, useMemo, useState } from "react";
import { CashierTopbar } from "../components/CashierTopbar";
import { useAppDialog } from "../components/AppDialog";
import { OnsitePaymentsPanel } from "../components/OnsitePaymentsPanel";
import { BankTransferAlertsPanel } from "../components/BankTransferAlertsPanel";
import { DepositsPanel } from "../components/DepositsPanel";
import { ServiceRequestsPanel } from "../components/ServiceRequestsPanel";
import { PendingReservationsPanel, ReservationActions } from "../components/PendingReservationsPanel";
import { cashierApi } from "../lib/api";
import { useControlledRead } from "../lib/useControlledRead";
import { useStaffRealtime } from "../lib/useStaffRealtime";
import { createTopicBus } from "../../../shared/staffRealtime";
import { parseApiUtcTimestamp } from "../lib/dateTime";
import { TransferQrCard } from "../components/TransferQrCard";
import type { CashierCheckoutPreviewDto, TransferQrDto } from "../lib/api";
import { paymentMethodLabel } from "../../../shared/paymentMethod";
import { CASHIER_TABS, badgeText, tabAccessibleName, tabFromHash, type CashierTabKey } from "../../../shared/cashierTabs";
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
  Rejected: "Bị từ chối",
};

const reservationStatuses = [
  { value: "ALL", label: "Tất cả trạng thái" },
  { value: "Pending", label: "Chờ xác nhận" },
  { value: "Confirmed", label: "Đã xác nhận" },
  { value: "CheckingIn", label: "Đang check-in" },
  { value: "CheckedIn", label: "Đã check-in" },
  { value: "Cancelled", label: "Đã hủy" },
  { value: "NoShow", label: "Không đến" },
  { value: "Rejected", label: "Bị từ chối" },
  { value: "Completed", label: "Hoàn tất" },
];

/** Today in restaurant time (UTC+7) as yyyy-MM-dd, for the date picker. */
function restaurantToday() {
  return new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 10);
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
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
  if (normalized === "CANCELLED" || normalized === "NOSHOW" || normalized === "REJECTED") return "soft-badge danger";
  return "soft-badge warning";
}

function formatReservationTime(value: string) {
  const date = parseApiUtcTimestamp(value);
  if (!date) return value;
  return formatViDateTime(date);
}

function summarizePreOrderItems(reservation: ReservationDto) {
  const items = reservation.preOrderItems?.filter((item) => item.status !== "Cancelled") ?? [];
  if (items.length === 0) return "Chưa chọn món trước";
  return items
    .slice(0, 3)
    .map((item) => `${item.dishNameSnapshot} x${item.quantity}`)
    .join(", ") + (items.length > 3 ? ` +${items.length - 3} món` : "");
}

function reservationTableText(reservation: ReservationDto, tables: { tableId: number; number: string }[]) {
  const ids = getReservationTableIds(reservation);
  if (ids.length === 0) return "Chưa gán bàn";
  return ids.map((tableId) => tables.find((table) => table.tableId === tableId)?.number ?? `Bàn #${tableId}`).join(", ");
}

function getReservationTableIds(reservation: ReservationDto) {
  const assignedIds = reservation.assignedTables?.map((table) => table.tableId).filter((tableId) => tableId > 0) ?? [];
  if (assignedIds.length > 0) return Array.from(new Set(assignedIds));
  return reservation.tableId ? [reservation.tableId] : [];
}

export function DashboardPage({ onLogout }: Props) {
  const { alert, confirm, prompt, Dialog } = useAppDialog();
  const [dashboard, setDashboard] = useState<CashierDashboardDto | null>(null);
  const [reservations, setReservations] = useState<ReservationDto[]>([]);
  const [reservationsLoading, setReservationsLoading] = useState(false);
  const [reservationsError, setReservationsError] = useState<string | null>(null);
  const [reservationSearch, setReservationSearch] = useState("");
  const [reservationStatus, setReservationStatus] = useState("ALL");
  const [reservationDate, setReservationDate] = useState(restaurantToday);
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
  const [paymentMethod, setPaymentMethod] = useState<"CASH" | "TRANSFER">("CASH");
  const [transferQr, setTransferQr] = useState<{ qr: TransferQrDto | null; error: string | null; loading: boolean }>({ qr: null, error: null, loading: false });
  const [paymentAmount, setPaymentAmount] = useState("");
  const [checkoutResult, setCheckoutResult] = useState<CheckoutResultView | null>(null);
  const [checkoutSubmitting, setCheckoutSubmitting] = useState(false);
  const [pendingOnsiteCount, setPendingOnsiteCount] = useState(0);
  const [pendingReservationCount, setPendingReservationCount] = useState(0);
  const [depositTodoCount, setDepositTodoCount] = useState(0);
  const [serviceRequestCount, setServiceRequestCount] = useState(0);
  const [transferAlertCount, setTransferAlertCount] = useState(0);
  const [activeTab, setActiveTab] = useState<CashierTabKey>(() => tabFromHash(window.location.hash));
  const tabCounts: Record<CashierTabKey, number> = {
    "quay": 0,
    "cho-thu-tien": pendingOnsiteCount,
    "dat-ban": pendingReservationCount + depositTodoCount,
    "phuc-vu": serviceRequestCount,
    "chuyen-khoan": transferAlertCount,
  };
  function openTab(key: CashierTabKey) {
    setActiveTab(key);
    // replaceState: switching tabs must not fill the Back history.
    window.history.replaceState(window.history.state, "", key === "quay" ? window.location.pathname + window.location.search : `#${key}`);
    window.scrollTo({ top: 0 });
  }

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

  const refreshDashboard = useControlledRead({
    read: signal => cashierApi.getDashboard(signal),
    apply: value => { setDashboard(value); setError(null); },
    error: err => setError(userMessage(err, "Không thể tải dữ liệu thu ngân.")),
    settled: () => setLoading(false),
    interval: 0,
  });
  async function loadDashboard(_silent = false) { await refreshDashboard(); }

  async function loadReservations(branchId: number, status = reservationStatus, date = reservationDate) {
    setReservationsLoading(true);
    setReservationsError(null);
    try {
      setReservations(await cashierApi.getTodayReservations(branchId, status, date));
    } catch (err) {
      setReservationsError(userMessage(err, "Không thể tải danh sách đặt bàn."));
    } finally {
      setReservationsLoading(false);
    }
  }

  useEffect(() => {
    const branchId = dashboard?.staff.branchId;
    if (!branchId) return;
    void loadReservations(branchId, reservationStatus, reservationDate);
  }, [dashboard?.staff.branchId, reservationStatus, reservationDate]);

  // One realtime connection for the whole screen; the panels below listen on the topic bus.
  const staffEvents = useMemo(() => createTopicBus(), []);
  const [realtimeLive, setRealtimeLive] = useState(false);
  useStaffRealtime(dashboard?.staff.branchId, {
    changed: topics => {
      if (topics.includes("dashboard") || topics.includes("orders")) void loadDashboard(true);
      staffEvents.emit(topics);
    },
    live: setRealtimeLive,
    error: err => setError(userMessage(err, "Không thể kết nối cập nhật realtime thu ngân.")),
  });

  // A booking was made, changed, decided or checked in (this screen, another cashier or the app).
  useEffect(() => {
    const branchId = dashboard?.staff.branchId;
    if (!branchId) return;
    return staffEvents.on("reservations", () => void loadReservations(branchId));
  }, [staffEvents, dashboard?.staff.branchId, reservationStatus, reservationDate]);

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
  // Points conversion comes from the backend (same rules as the app); without it nothing can be redeemed.
  const pointValueVnd = activeOrder?.pointValueVnd ?? 0;
  const pointsStep = activeOrder?.pointsStep ?? 0;
  const maxPointsDiscountPercent = activeOrder?.maxPointsDiscountPercent ?? 0;

  // What the cashier typed; the server prices it (owner 29/10: one formula, CheckoutPricing, for app, cashier and counter).
  const rawDiscount = Number(discountAmount || "0");
  const requestedDiscount = Number.isFinite(rawDiscount) ? clamp(Math.floor(rawDiscount), 0, subtotal) : 0;
  const canRedeem = pointValueVnd > 0 && pointsStep > 0 && maxPointsDiscountPercent > 0;
  const rawPoints = Number(pointsUsedInput || "0");
  const requestedPoints = canRedeem && Number.isFinite(rawPoints) ? Math.max(0, Math.floor(rawPoints)) : 0;
  const priceKey = activeOrder ? `${activeOrder.orderId}:${subtotal}:${requestedDiscount}:${requestedPoints}` : "";
  const [serverPrice, setServerPrice] = useState<{ key: string; data: CashierCheckoutPreviewDto | null; error: string | null }>(
    { key: "", data: null, error: null });

  useEffect(() => {
    const orderId = activeOrder?.orderId;
    if (!orderId) { setServerPrice({ key: "", data: null, error: null }); return; }
    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      cashierApi.previewCheckout(orderId, { discount: requestedDiscount, pointsUsed: requestedPoints }, controller.signal)
        .then((data) => setServerPrice({ key: priceKey, data, error: null }))
        .catch((err) => {
          if (!controller.signal.aborted) setServerPrice({ key: priceKey, data: null, error: userMessage(err, "Không tính được số tiền cần thanh toán.") });
        });
    }, 250);
    return () => { controller.abort(); window.clearTimeout(timer); };
    // dashboard: a refresh re-prices (a promotion may have started or ended).
  }, [priceKey, dashboard]);

  const checkoutPreview = useMemo(() => {
    const data = serverPrice.key === priceKey ? serverPrice.data : null;
    const total = data?.totalAmount ?? 0;
    const rawPayment = Number(paymentAmount || "0");
    const safePayment = Number.isFinite(rawPayment) ? Math.max(0, rawPayment) : 0;
    const effectivePayment = paymentMethod === "TRANSFER" ? total : safePayment;
    return {
      priced: data !== null,
      error: serverPrice.key === priceKey ? serverPrice.error : null,
      appSession: data?.appSession ?? false,
      discount: data?.discount ?? 0,
      promotionName: data?.promotionName ?? null,
      promotionOffer: data?.promotionOffer ?? 0,
      promotionApplied: data?.promotionDiscount ?? 0,
      promotionNote: data?.discountNote ?? null,
      maxPointsAllowed: data?.maxPoints ?? 0,
      pointsUsed: data?.pointsUsed ?? 0,
      pointsDiscount: data?.pointsDiscount ?? 0,
      depositApplied: data?.depositApplied ?? 0,
      total,
      paymentAmount: effectivePayment,
      changeAmount: data ? Math.max(0, effectivePayment - total) : 0,
    };
  }, [paymentAmount, paymentMethod, priceKey, serverPrice]);

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
    if (paymentMethod === "TRANSFER") {
      setPaymentAmount(String(checkoutPreview.total));
    }
  }, [checkoutPreview.total, paymentMethod]);

  // VietQR from the server for the order total after discounts; reloaded when the total changes.
  useEffect(() => {
    const orderId = activeOrder?.orderId;
    const amount = checkoutPreview.total;
    if (paymentMethod !== "TRANSFER" || !orderId || amount <= 0) {
      setTransferQr({ qr: null, error: null, loading: false });
      return;
    }
    const controller = new AbortController();
    setTransferQr((current) => ({ ...current, error: null, loading: true }));
    const timer = window.setTimeout(() => {
      cashierApi.getOrderTransferQr(orderId, amount, controller.signal)
        .then((qr) => setTransferQr({ qr, error: null, loading: false }))
        .catch((err) => {
          if (!controller.signal.aborted) setTransferQr({ qr: null, error: userMessage(err, "Không tạo được mã QR chuyển khoản."), loading: false });
        });
    }, 300);
    return () => { controller.abort(); window.clearTimeout(timer); };
  }, [activeOrder?.orderId, checkoutPreview.total, paymentMethod]);

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

  // "Đóng bàn": only when the guest left with nothing to pay; the server re-checks.
  async function closeTable(sessionId: number, tableNumber: string) {
    const ok = await confirm({
      title: `Đóng ${tableNumber}?`,
      message: "Chỉ đóng bàn khi khách đã rời đi. Phiên của khách trên ứng dụng sẽ kết thúc và bàn trở thành trống.",
      confirmLabel: "Đóng bàn",
      cancelLabel: "Không",
      variant: "danger",
    });
    if (!ok) return;
    setError(null);
    try {
      const result = await cashierApi.closeDiningSession(sessionId);
      setMessage(result.message || "Đã đóng bàn.");
      await loadDashboard(true);
    } catch (err) {
      setError(userMessage(err, "Không thể đóng bàn."));
    }
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
        orderCode: orderLabel(activeOrder.orderId, activeOrder.orderCode),
        tableName: selectedTable.number,
        customerName: activeOrder.customerName,
        cashierName: dashboard.staff.name,
        cashierRoleName: dashboard.staff.roleName,
        branchName: dashboard.staff.branchName,
        paidTime: new Date().toISOString(),
        subtotal: activeOrder.subtotal,
        discount: checkoutPreview.discount,
        pointsDiscount: checkoutPreview.pointsDiscount,
        items: activeOrder.items.map((item) => ({ ...item })),
        paymentMethod,
        paymentAmount: checkoutPreview.paymentAmount,
      };
      // What the cashier asked for; the server prices it again and never trusts an amount from this screen.
      const result = await cashierApi.checkout(activeOrder.orderId, {
        discount: checkoutPreview.appSession ? 0 : requestedDiscount,
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
      setError(userMessage(err, "Không thể thanh toán hóa đơn."));
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
      setError(userMessage(err, "Không thể check-in đặt bàn."));
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
  if (cleanText(error) && !dashboard) return <div className="screen-message error-box">{cleanText(error)}</div>;
  if (!dashboard) return null;

  const occupiedCount = dashboard.tables.filter((table) => table.status === "OCCUPIED").length;
  const selectedTableTitle = selectedTable ? selectedTable.number : "Chưa chọn bàn";
  // Only once the server priced exactly what is on screen.
  const canCheckout = !!activeOrder && checkoutPreview.priced;

  return (
    <>
    <CashierTopbar
      active="pos"
      branchName={dashboard.staff.branchName}
      staffName={dashboard.staff.name}
      roleName={dashboard.staff.roleName}
      onLogout={() => void onLogout()}
    />
    <main className="cashier-shell cashier-pos-shell">
      <section className="vt-stats cashier-pos-stats" aria-label="Số liệu hôm nay">
        <div className="vt-stat">
          <span>Đơn hôm nay</span>
          <b>{dashboard.todayOrders}</b>
        </div>
        <div className="vt-stat">
          <span>Doanh thu hôm nay</span>
          <b>{dashboard.todayRevenue.toLocaleString("vi-VN")} đ</b>
        </div>
        <div className="vt-stat">
          <span>Bàn có khách</span>
          <b>{occupiedCount} / {dashboard.tables.length}</b>
        </div>
        <button
          type="button"
          className={`vt-stat ${pendingOnsiteCount > 0 ? "is-alert" : ""}`}
          onClick={() => openTab("cho-thu-tien")}
        >
          <span>Chờ xác nhận thu tiền</span>
          <b>{pendingOnsiteCount} giao dịch</b>
        </button>
      </section>

      <nav className="cashier-tabs" role="tablist" aria-label="Khu vực làm việc của thu ngân">
        {CASHIER_TABS.map((tab) => {
          const count = tabCounts[tab.key];
          const selected = activeTab === tab.key;
          return (
            <button
              key={tab.key}
              type="button"
              role="tab"
              id={`cashier-tab-${tab.key}`}
              aria-controls={`cashier-panel-${tab.key}`}
              aria-selected={selected}
              aria-label={tabAccessibleName(tab, count)}
              className={`cashier-tab${selected ? " is-active" : ""}${count > 0 ? " has-count" : ""}`}
              onClick={() => openTab(tab.key)}
            >
              <i className={`bi ${tab.icon}`} aria-hidden="true" />
              <span className="cashier-tab-label">{tab.label}</span>
              {count > 0 ? <span className="cashier-tab-badge" aria-hidden="true">{badgeText(count)}</span> : null}
            </button>
          );
        })}
      </nav>
      <p className="visually-hidden" role="status" aria-atomic="true">
        {pendingOnsiteCount > 0 ? `${pendingOnsiteCount} giao dịch chờ thu tiền` : ""}
      </p>

      {message ? <div className="success-box">{message}</div> : null}
      {cleanText(error) ? <div className="error-box">{cleanText(error)}</div> : null}

      <div role="tabpanel" id="cashier-panel-quay" aria-labelledby="cashier-tab-quay" hidden={activeTab !== "quay"}>
      <section className="cashier-pos-grid">
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
                {selectedTable.openSessionId ? (
                  <>
                    <p>Khách đang ngồi bàn này (đặt qua ứng dụng) nhưng không còn món nào cần thanh toán.</p>
                    {selectedTable.canCloseSession ? (
                      <button className="cashier-button-outline" onClick={() => void closeTable(selectedTable.openSessionId!, selectedTable.number)}>
                        <i className="bi bi-door-closed me-1" />
                        Đóng bàn
                      </button>
                    ) : (
                      <p className="muted">Khách đang yêu cầu thanh toán nên chưa thể đóng bàn.</p>
                    )}
                  </>
                ) : (
                  <p>Bàn này chưa có đơn hàng</p>
                )}
              </div>
            ) : (
              <>
                <div className="inline-filter-card cashier-order-summary-card cashier-pos-order-summary">
                  <div>
                    <strong>{orderLabel(activeOrder.orderId, activeOrder.orderCode)}</strong>
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
                    disabled={checkoutPreview.appSession}
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
                    disabled={checkoutPreview.appSession}
                    value={discountPercent}
                    onChange={(event) => handleDiscountPercentChange(event.target.value)}
                    style={{ width: "120px" }}
                  />
                  <span className="muted">%</span>
                </div>
              </div>
              {checkoutPreview.appSession ? (
                <div className="muted small" role="note">Khách dùng ứng dụng: giảm giá, voucher và điểm do khách chọn trong ứng dụng.</div>
              ) : null}
              {checkoutPreview.promotionOffer > 0 ? (
                <div className="total-row" data-promotion-row>
                  <span>Khuyến mãi{checkoutPreview.promotionName ? ` (${checkoutPreview.promotionName})` : ""}:</span>
                  <strong>{checkoutPreview.promotionApplied > 0
                    ? `-${checkoutPreview.promotionApplied.toLocaleString("vi-VN")} đ` : "Không áp dụng"}</strong>
                </div>
              ) : null}
              {checkoutPreview.appSession && checkoutPreview.discount > 0 ? (
                <div className="total-row">
                  <span>Voucher:</span>
                  <strong>-{checkoutPreview.discount.toLocaleString("vi-VN")} đ</strong>
                </div>
              ) : null}
              {checkoutPreview.promotionNote ? <div className="muted small" role="note">{checkoutPreview.promotionNote}</div> : null}
              {checkoutPreview.pointsUsed > 0 ? (
                <div className="total-row">
                  <span>Giảm bằng điểm:</span>
                  <strong>-{checkoutPreview.pointsDiscount.toLocaleString("vi-VN")} đ</strong>
                </div>
              ) : null}
              {checkoutPreview.depositApplied > 0 ? (
                <div className="total-row">
                  <span>Đã đặt cọc:</span>
                  <strong>-{checkoutPreview.depositApplied.toLocaleString("vi-VN")} đ</strong>
                </div>
              ) : null}
              <div className="total-row grand-total">
                <span>TỔNG CỘNG:</span>
                <span>{checkoutPreview.priced ? `${checkoutPreview.total.toLocaleString("vi-VN")} đ` : activeOrder ? "Đang tính…" : "0 đ"}</span>
              </div>
              {checkoutPreview.error ? <div className="error-box small" role="alert">{checkoutPreview.error}</div> : null}
            </div>

            {activeOrder?.customerId && !checkoutPreview.appSession ? (
              <div className="credit-points-section">
                <div className="credit-balance">
                  <span>Số dư hiện tại:</span>
                  <span className="credit-amount">{pointsAvailable.toLocaleString("vi-VN")} điểm</span>
                </div>
                <label>
                  Sử dụng điểm (1 điểm = {pointValueVnd.toLocaleString("vi-VN")} đ):
                  <input
                    className="points-input"
                    type="number"
                    min="0"
                    max={checkoutPreview.maxPointsAllowed}
                    step={pointsStep || undefined}
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
                  className={`payment-method-tile ${paymentMethod === "TRANSFER" ? "active" : ""}`}
                  onClick={() => setPaymentMethod("TRANSFER")}
                >
                  <i className="bi bi-qr-code" />
                  <strong>Chuyển khoản</strong>
                  <small>Mã VietQR có sẵn số tiền</small>
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
              <TransferQrCard
                qr={transferQr.qr}
                loading={transferQr.loading}
                error={transferQr.error}
                onCopy={(value, note) => void copyText(value, note)}
              />
            )}

            <button
              className="cashier-button-primary wide"
              disabled={!canCheckout || checkoutSubmitting || (paymentMethod === "TRANSFER" && !transferQr.qr)}
              onClick={() => void processCheckout()}
            >
              <i className="bi bi-check-circle me-2" />
              {checkoutSubmitting ? "Đang xử lý..." : paymentMethod === "TRANSFER" ? "Đã nhận chuyển khoản" : "Xác nhận thanh toán"}
            </button>
          </div>
        </div>
      </section>
      </div>

      {/* Hidden tabs stay mounted: their realtime reads keep the counts on the tab buttons current. */}
      <div role="tabpanel" id="cashier-panel-cho-thu-tien" aria-labelledby="cashier-tab-cho-thu-tien" hidden={activeTab !== "cho-thu-tien"}>
        <OnsitePaymentsPanel onPendingCountChange={setPendingOnsiteCount} events={staffEvents} live={realtimeLive} />
      </div>

      <div role="tabpanel" id="cashier-panel-phuc-vu" aria-labelledby="cashier-tab-phuc-vu" hidden={activeTab !== "phuc-vu"}>
        <ServiceRequestsPanel tables={dashboard.tables} events={staffEvents} live={realtimeLive} onCountChange={setServiceRequestCount} />
      </div>

      <div role="tabpanel" id="cashier-panel-chuyen-khoan" aria-labelledby="cashier-tab-chuyen-khoan" hidden={activeTab !== "chuyen-khoan"}>
        <BankTransferAlertsPanel events={staffEvents} live={realtimeLive} onCountChange={setTransferAlertCount} />
        {transferAlertCount === 0 ? (
          <div className="panel cashier-panel-card cashier-empty-state">
            <i className="bi bi-bank" aria-hidden="true" />
            <p>Không có khoản chuyển nào cần xử lý. Tiền chuyển khoản khớp nội dung được hệ thống tự xác nhận.</p>
          </div>
        ) : null}
      </div>

      <div role="tabpanel" id="cashier-panel-dat-ban" aria-labelledby="cashier-tab-dat-ban" hidden={activeTab !== "dat-ban"}>
      <div className="cashier-secondary-grid">
        <PendingReservationsPanel events={staffEvents} live={realtimeLive}
          tableLabel={(reservation) => reservationTableText(reservation, dashboard.tables)}
          onChanged={() => void loadReservations(dashboard.staff.branchId)}
          onCheckIn={(reservation) => setCheckInTarget(reservation)}
          onCountChange={setPendingReservationCount} />
        <DepositsPanel events={staffEvents} live={realtimeLive} onCountChange={setDepositTodoCount} />
      </div>

      <section className="panel cashier-panel-card cashier-reservation-panel">
        <div className="cashier-panel-header">
          <div>
            <h2>
              <i className="bi bi-calendar-check me-2" />
              {reservationDate === restaurantToday() ? "Đặt bàn hôm nay" : "Đặt bàn theo ngày"}
            </h2>
            <p className="muted">Theo dõi khách đã đặt bàn và món đặt trước trong ngày đã chọn.</p>
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
            <input type="date" aria-label="Ngày đặt bàn" value={reservationDate}
              onChange={(event) => setReservationDate(event.target.value || restaurantToday())} />
            <select value={reservationStatus} onChange={(event) => setReservationStatus(event.target.value)}>
              {reservationStatuses.map((status) => (
                <option key={status.value} value={status.value}>{status.label}</option>
              ))}
            </select>
          </div>

          {reservationsLoading ? (
            <div className="cashier-empty-state reservation-empty-state">
              <i className="bi bi-hourglass-split" />
              <p>Đang tải đặt bàn...</p>
            </div>
          ) : null}

          {cleanText(reservationsError) ? <div className="error-box">{cleanText(reservationsError)}</div> : null}

          {!reservationsLoading && !reservationsError && visibleReservations.length === 0 ? (
            <div className="cashier-empty-state reservation-empty-state">
              <i className="bi bi-calendar2-x" />
              <p>Chưa có đặt bàn nào trong ngày này.</p>
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
                    {reservation.statusReason ? <p className="reservation-note" data-status-reason>
                      {reservation.status === "Rejected" ? "Lý do từ chối: " : ""}{reservation.statusReason}</p> : null}
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
                    <small className="muted">{reservation.status === "Pending"
                      ? "Nhận khách khi còn chờ sẽ tự xác nhận đặt bàn."
                      : "Check-in sẽ tạo đơn bếp từ món đặt trước nếu có."}</small>
                    <ReservationActions reservation={reservation}
                      onChanged={() => staffEvents.emit(["reservations"])} />
                  </article>
                );
              })}
            </div>
          ) : null}
        </div>
      </section>
      </div>

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
                    <div className="cashier-info-note warning">Không bàn nào đang xử lý.</div>
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
              <div className="cashier-checkout-success">
                <i className="bi bi-check-circle" />
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
                  <strong>{formatViDateTime(parseApiUtcTimestamp(checkoutResult.paidTime), checkoutResult.paidTime)}</strong>
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
    </>
  );
}
