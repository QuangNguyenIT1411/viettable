import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { useAppDialog } from "../components/AppDialog";
import { api } from "../lib/api";
import type { ActiveOrderItemDto, CustomerSessionUserDto, LoyaltyScanResponse } from "../lib/types";

const text = {
  loading: "Đang tải hóa đơn...",
  errorPrefix: "Không thể tải hóa đơn:",
  billTitle: "Hóa Đơn Tạm Tính",
  tablePrefix: "Bàn",
  backMenu: "Quay lại Thực Đơn",
  orderedDishes: "Các món đã gọi",
  emptyOrder: "Chưa có món nào được gọi",
  emptyOrderHint: "Vui lòng quay lại thực đơn để chọn món",
  quantityPrefix: "Số lượng:",
  pending: "Chờ gửi",
  preparing: "Đang chuẩn bị",
  ready: "Đã sẵn sàng",
  serving: "Đang phục vụ",
  cancelled: "Đã hủy",
  summaryTitle: "Tạm Tính",
  subtotalPrefix: (count: number) => `Tạm tính (${count} món)`,
  pointsEstimate: "Điểm thưởng (dự kiến)",
  pointsSuffix: "im",
  total: "Tổng cộng",
  sendKitchen: "Gửi Yêu Cầu Bếp",
  sentKitchen: "Đã gửi bếp",
  scanLoyalty: "Quét Thẻ Tích Điểm",
  paymentHint: "Vui lòng thanh toán tại",
  cashierDesk: "Quầy Thu Ngân",
  afterMeal: "khi dùng bữa xong.",
  promptPhone: "Nhập số điện thoại khách hàng:",
  removeConfirm: "Bạn có chắc muốn xóa món này?",
  sendConfirm: "Gửi yêu cầu đến bếp?",
  deleted: "Đã xóa món thành công",
  submitted: "Đã gửi yêu cầu đến bếp",
  scanError: "Có lỗi xảy ra khi quét thẻ",
  sendError: "Có lỗi xảy ra khi gửi yêu cầu",
  removeError: "Có lỗi xảy ra khi xóa món",
} as const;

function formatCurrency(value: number) {
  return `${value.toLocaleString("vi-VN")} `;
}

function getOrderSubmitStorageKey(tableId: number) {
  return `selfrestaurant.customer.orderSubmitIntent:${tableId}`;
}

function buildOrderSubmitIntentKey() {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }

  return `order-submit-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function getOrCreateOrderSubmitIntentKey(tableId: number) {
  if (typeof window === "undefined" || typeof window.sessionStorage === "undefined") {
    return buildOrderSubmitIntentKey();
  }

  const storageKey = getOrderSubmitStorageKey(tableId);
  const existing = window.sessionStorage.getItem(storageKey);
  if (existing) {
    return existing;
  }

  const created = buildOrderSubmitIntentKey();
  window.sessionStorage.setItem(storageKey, created);
  return created;
}

function clearOrderSubmitIntentKey(tableId: number) {
  if (typeof window === "undefined" || typeof window.sessionStorage === "undefined") {
    return;
  }

  window.sessionStorage.removeItem(getOrderSubmitStorageKey(tableId));
}

function normalizeStatus(value?: string | null) {
  return (value ?? "").toUpperCase();
}

function getDisplayStatus(item: ActiveOrderItemDto, orderStatus: string) {
  const status = normalizeStatus(item.status) || normalizeStatus(orderStatus);
  if (status === "CANCELLED") return "cancelled";
  if (status === "SERVING") return "serving";
  if (["READY", "SERVED", "COMPLETED"].includes(status)) return "ready";
  if (["CONFIRMED", "PREPARING"].includes(status)) return "preparing";
  return "pending";
}

function renderStatusBadge(status: "pending" | "preparing" | "ready" | "serving" | "cancelled") {
  if (status === "cancelled") {
    return (
      <span className="badge rounded-pill bg-danger-subtle text-danger-emphasis p-2 order-status-badge">
        <i className="fas fa-ban me-1" />
        {text.cancelled}
      </span>
    );
  }

  if (status === "serving") {
    return (
      <span className="badge rounded-pill bg-info-subtle text-info-emphasis p-2 order-status-badge">
        <i className="fas fa-concierge-bell me-1" />
        {text.serving}
      </span>
    );
  }

  if (status === "ready") {
    return (
      <span className="badge rounded-pill bg-success-subtle text-success-emphasis p-2 order-status-badge">
        <i className="fas fa-check-circle me-1" />
        {text.ready}
      </span>
    );
  }

  if (status === "preparing") {
    return (
      <span className="badge rounded-pill bg-warning-subtle text-warning-emphasis p-2 order-status-badge">
        <i className="fas fa-clock me-1" />
        {text.preparing}
      </span>
    );
  }

  return (
    <span className="badge rounded-pill bg-primary-subtle text-primary-emphasis p-2 order-status-badge" data-status="pending">
      <i className="fas fa-paper-plane me-1" />
      {text.pending}
    </span>
  );
}

function getOrderProgress(status: string) {
  if (status === "CANCELLED") return 0;
  if (["READY", "SERVED", "COMPLETED", "SERVING"].includes(status)) return 3;
  if (["CONFIRMED", "PREPARING"].includes(status)) return 2;
  return 1;
}

function getActiveCustomer(
  sessionCustomer?: CustomerSessionUserDto | null,
  loyaltyCustomer?: LoyaltyScanResponse["customer"] | null,
) {
  if (loyaltyCustomer) {
    return {
      name: loyaltyCustomer.name,
      phoneNumber: loyaltyCustomer.phone,
      loyaltyPoints: loyaltyCustomer.currentPoints,
    };
  }

  if (sessionCustomer) {
    return {
      name: sessionCustomer.name,
      phoneNumber: sessionCustomer.phoneNumber,
      loyaltyPoints: sessionCustomer.loyaltyPoints,
    };
  }

  return null;
}

export function OrderPage() {
  const queryClient = useQueryClient();
  const [toast, setToast] = useState<{ type: "success" | "error"; message: string } | null>(null);
  const { confirm, prompt, Dialog } = useAppDialog();

  const order = useQuery({ queryKey: ["order"], queryFn: api.getOrder });
  const orderItems = useQuery({ queryKey: ["orderItems"], queryFn: api.getOrderItems });
  const session = useQuery({ queryKey: ["session"], queryFn: api.getSession });

  const submitOrder = useMutation({
    mutationFn: api.submitOrder,
    onSuccess: async (result) => {
      await queryClient.invalidateQueries();
      setToast({ type: "success", message: result.message || text.submitted });
    },
    onError: (error) => {
      setToast({ type: "error", message: (error as Error).message || text.sendError });
    },
  });

  const removeItem = useMutation({
    mutationFn: api.removeItem,
    onSuccess: async () => {
      await queryClient.invalidateQueries();
      setToast({ type: "success", message: text.deleted });
    },
    onError: (error) => {
      setToast({ type: "error", message: (error as Error).message || text.removeError });
    },
  });

  const scanLoyalty = useMutation({
    mutationFn: api.scanLoyalty,
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ["session"] });
      await queryClient.invalidateQueries({ queryKey: ["order"] });
      await queryClient.invalidateQueries({ queryKey: ["orderItems"] });
      if (result.success) {
        const customer = result.customer;
        const customerMessage = customer
          ? `Đã quét thẻ: ${customer.name} - ${customer.currentPoints} điểm`
          : result.message;
        setToast({ type: "success", message: customerMessage });
      } else {
        setToast({ type: "error", message: result.message });
      }
    },
    onError: (error) => {
      setToast({ type: "error", message: (error as Error).message || text.scanError });
    },
  });

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 3500);
    return () => window.clearTimeout(timer);
  }, [toast]);

  if (order.isLoading || orderItems.isLoading || session.isLoading) {
    return <div className="card order-loading-card">{text.loading}</div>;
  }

  if (order.error) {
    return (
      <div className="card error order-loading-card">
        {text.errorPrefix} {(order.error as Error).message}
      </div>
    );
  }

  const currentOrder = order.data;
  const items = orderItems.data?.items ?? [];
  const subtotal = orderItems.data?.subtotal ?? 0;
  const totalItemCount = items
    .filter((item) => normalizeStatus(item.status) !== "CANCELLED")
    .reduce((sum, item) => sum + item.quantity, 0);
  const normalizedOrderStatus = normalizeStatus(currentOrder?.statusCode || currentOrder?.orderStatus);
  const estimatedPoints = Math.floor(subtotal * 0.05);
  const currentTableId = session.data?.tableContext?.tableId ?? currentOrder?.tableId ?? 0;
  const tableNumber = session.data?.tableContext?.tableNumber ?? currentOrder?.tableId ?? "-";
  const activeCustomer = getActiveCustomer(session.data?.customer, scanLoyalty.data?.customer);
  const canRemove = normalizedOrderStatus === "PENDING";
  const canSubmit = items.length > 0 && canRemove;
  const progressStep = getOrderProgress(normalizedOrderStatus);

  return (
    <div className="container p-3 order-page-shell">
      {toast ? (
        <div className="position-fixed bottom-0 end-0 p-3" style={{ zIndex: 1080 }}>
          <div className={`toast show align-items-center text-white border-0 ${toast.type === "success" ? "bg-success" : "bg-danger"}`} role="alert">
            <div className="d-flex">
              <div className="toast-body">
                <i className={`fas ${toast.type === "success" ? "fa-check-circle" : "fa-exclamation-triangle"} me-2`} />
                {toast.message}
              </div>
              <button type="button" className="btn-close btn-close-white me-2 m-auto" aria-label="Close" onClick={() => setToast(null)} />
            </div>
          </div>
        </div>
      ) : null}

      <header className="p-3 mb-3 bg-white shadow-sm rounded d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3 order-header-card customer-premium-header">
        <div className="bill-title">
          <i className="fas fa-receipt me-2" />
          {text.billTitle} - <span id="table-number">{text.tablePrefix} {tableNumber}</span>
        </div>
        <Link to="/Menu/Index" className="btn btn-outline-secondary">
          <i className="fas fa-chevron-left me-2" />
          {text.backMenu}
        </Link>
      </header>

      <section className="order-tracking-card" aria-label="Theo dõi trạng thái đơn hàng">
        <div className="order-tracking-copy">
          <span className="order-eyebrow">Trạng thái đơn</span>
          <strong>{items.length > 0 ? (canRemove ? text.pending : text.sentKitchen) : text.emptyOrder}</strong>
          <small>{text.paymentHint} <b>{text.cashierDesk}</b> {text.afterMeal}</small>
        </div>
        <div className="order-progress-track" data-step={progressStep}>
          {[text.pending, text.preparing, text.ready].map((label, index) => (
            <div key={label} className={`order-progress-step ${progressStep >= index + 1 ? "is-active" : ""}`}>
              <span>{index + 1}</span>
              <small>{label}</small>
            </div>
          ))}
        </div>
      </section>

      <main>
        <div className="row g-4">
          <div className="col-lg-8">
            <h2 className="h4 mb-3 customer-section-title">{text.orderedDishes}</h2>
            <div className="bg-white rounded shadow-sm order-items-card">
              <div className="list-group list-group-flush order-list-group" id="item-list">
                {items.length > 0 ? (
                  items.map((item) => {
                    const status = getDisplayStatus(item, normalizedOrderStatus);

                    return (
                      <div
                        key={item.itemId}
                        className="list-group-item list-group-item-custom"
                        data-item-id={item.itemId}
                        data-quantity={item.quantity}
                        data-price={item.unitPrice}
                      >
                        <div className="d-flex w-100 justify-content-between align-items-start gap-3">
                          <div className="flex-grow-1">
                            <h5 className="mb-1">{item.dishName}</h5>
                            <small className="text-muted">
                              {text.quantityPrefix} {item.quantity} {"\u00d7"} {formatCurrency(item.unitPrice)}
                            </small>
                            {item.note ? (
                              <div>
                                <small className="text-info">
                                  <i className="fas fa-sticky-note me-1" />
                                  {item.note}
                                </small>
                              </div>
                            ) : null}
                          </div>
                          <span className="price-col">{formatCurrency(item.lineTotal)}</span>
                        </div>
                        <div className={`order-status-row ${canRemove ? "is-pending" : ""}`}>
                          {renderStatusBadge(status)}
                          {canRemove ? (
                            <button
                              type="button"
                              className="btn-remove-item text-danger"
                              onClick={async () => {
                                const approved = await confirm({
                                  title: "Xác nhận xóa",
                                  message: text.removeConfirm,
                                  confirmLabel: "Xóa",
                                  cancelLabel: "Hủy",
                                  variant: "danger",
                                });
                                if (!approved) return;
                                removeItem.mutate(item.itemId);
                              }}
                            >
                              <i className="fas fa-trash-alt" />
                            </button>
                          ) : null}
                        </div>
                      </div>
                    );
                  })
                ) : (
                  <div className="empty-state order-empty-state">
                    <i className="fas fa-utensils" />
                    <p className="mb-0">{text.emptyOrder}</p>
                    <small className="text-muted">{text.emptyOrderHint}</small>
                  </div>
                )}
              </div>
            </div>
          </div>

          <div className="col-lg-4">
            <h2 className="h4 mb-3 customer-section-title">{text.summaryTitle}</h2>
            <div className="card shadow-sm border-0 order-summary-card">
              <div className="card-body p-4">
                <div className="order-summary-row">
                  <span>{text.subtotalPrefix(totalItemCount)}</span>
                  <span>{formatCurrency(subtotal)}</span>
                </div>
                <div className="order-summary-row">
                  <span>{text.pointsEstimate}</span>
                  <span>+ {estimatedPoints.toLocaleString("vi-VN")} {text.pointsSuffix}</span>
                </div>

                <hr className="my-3" />

                <div className="order-total-row">
                  <span className="h5 mb-0">{text.total}</span>
                  <span className="total-amount">{formatCurrency(subtotal)}</span>
                </div>

                <div className="d-grid gap-2">
                  <button
                    className="btn btn-success btn-lg order-action-btn"
                    type="button"
                    onClick={async () => {
                      if (!canSubmit) return;
                      const approved = await confirm({
                        title: "Xác nhận gửi bếp",
                        message: text.sendConfirm,
                        confirmLabel: "Đồng ý",
                        cancelLabel: "Hủy",
                      });
                      if (!approved) return;
                      submitOrder.mutate(
                        {
                          idempotencyKey: getOrCreateOrderSubmitIntentKey(currentTableId),
                          expectedDiningSessionCode: currentOrder?.diningSessionCode ?? null,
                        },
                        {
                          onSuccess: () => {
                            clearOrderSubmitIntentKey(currentTableId);
                          },
                        },
                      );
                    }}
                    disabled={!canSubmit || submitOrder.isPending}
                    id="btn-send-to-kitchen"
                  >
                    <i className="fas fa-paper-plane me-2" />
                    <span>{items.length > 0 && !canRemove ? text.sentKitchen : text.sendKitchen}</span>
                  </button>

                  <button
                    className="btn btn-primary btn-lg order-action-btn"
                    type="button"
                    id="btn-scan-card"
                    onClick={async () => {
                      const phoneNumber = await prompt({
                        title: "Nhập số điện thoại",
                        message: text.promptPhone,
                        confirmLabel: "Đồng ý",
                        cancelLabel: "Hủy",
                        placeholder: "S in thoi",
                      });
                      if (!phoneNumber || !phoneNumber.trim()) return;
                      scanLoyalty.mutate(phoneNumber.trim());
                    }}
                  >
                    <i className="fas fa-credit-card me-2" />
                    {text.scanLoyalty}
                  </button>
                </div>

                {activeCustomer ? (
                  <div className="alert alert-info mt-3 mb-0 order-customer-alert">
                    <small>
                      <i className="fas fa-user me-1" />
                      <strong>{activeCustomer.name}</strong>
                      <br />
                      Điểm hiện tại: {activeCustomer.loyaltyPoints.toLocaleString("vi-VN")} {text.pointsSuffix}
                    </small>
                  </div>
                ) : null}
              </div>
            </div>

            <div className="alert alert-secondary d-flex align-items-center mt-3 order-help-alert" role="alert">
              <i className="fa-solid fa-money-bill-1 me-3" />
              <div>
                {text.paymentHint} <strong>{text.cashierDesk}</strong> {text.afterMeal}
              </div>
            </div>
          </div>
        </div>
      </main>
      <Dialog />
    </div>
  );
}
