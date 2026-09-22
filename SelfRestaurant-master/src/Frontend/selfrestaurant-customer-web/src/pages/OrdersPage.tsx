import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { api } from "../lib/api";
import type { CustomerOrderHistoryDto } from "../lib/types";

const text = {
  loading: "Đang tải lịch sử đơn hàng...",
  errorPrefix: "Không thể tải lịch sử đơn hàng:",
  title: "Lịch Sử Đơn Hàng",
  subtitle: "Theo dõi các đơn hàng bạn đã đặt tại Self Restaurant",
  backDashboard: "Quay lại hồ sơ cá nhân",
  noOrders: "Chưa có đơn hàng nào",
  orderNow: "Đặt Món Ngay",
  orderCountSuffix: "món",
  orderCodePrefix: "Mã đơn",
  noValue: "Chưa có",
  completed: "Hoàn tất",
  ready: "Sẵn sàng",
  preparing: "Đang chuẩn bị",
  pending: "Chờ gửi",
  updating: "Đang cập nhật",
} as const;

function formatCurrency(value: number) {
  return `${value.toLocaleString("vi-VN")} `;
}

function formatDateTime(value?: string | null) {
  if (!value) return text.noValue;
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString("vi-VN");
}

function renderOrderStatus(status?: string | null) {
  const normalized = (status ?? "").toUpperCase();

  if (normalized === "COMPLETED" || normalized === "SERVED") {
    return <span className="badge rounded-pill bg-success">{text.completed}</span>;
  }

  if (normalized === "READY") {
    return <span className="badge rounded-pill bg-success">{text.ready}</span>;
  }

  if (normalized === "PREPARING" || normalized === "CONFIRMED") {
    return <span className="badge rounded-pill bg-warning text-dark">{text.preparing}</span>;
  }

  if (normalized === "PENDING") {
    return <span className="badge rounded-pill bg-primary">{text.pending}</span>;
  }

  return <span className="badge rounded-pill bg-secondary">{text.updating}</span>;
}

export function OrdersPage() {
  const history = useQuery({ queryKey: ["orderHistory", "full"], queryFn: () => api.getOrderHistory(100) });

  if (history.isLoading) {
    return <div className="card">{text.loading}</div>;
  }

  if (history.error) {
    return (
      <div className="card error">
        {text.errorPrefix} {(history.error as Error).message}
      </div>
    );
  }

  const orders = history.data ?? [];

  return (
    <div className="container p-3 orders-page-shell">
      <header className="p-3 mb-4 bg-white shadow-sm rounded d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3 customer-premium-header orders-premium-header">
        <div>
          <h1 className="bill-title mb-1">
            <i className="fas fa-receipt me-2" />
            {text.title}
          </h1>
          <p className="text-muted mb-0">{text.subtitle}</p>
        </div>
        <Link to="/Customer/Dashboard" className="btn btn-outline-secondary">
          <i className="fas fa-chevron-left me-2" />
          {text.backDashboard}
        </Link>
      </header>

      <section className="orders-section">
        {orders.length > 0 ? (
          orders.map((orderItem: CustomerOrderHistoryDto) => (
            <div key={orderItem.orderId} className="order-item order-history-card">
              <div className="row align-items-center">
                <div className="col-md-5">
                  <h5 className="mb-1">
                    <i className="fas fa-file-invoice me-2" />
                    {orderItem.orderCode ?? `${text.orderCodePrefix} #${orderItem.orderId}`}
                  </h5>
                  <small className="text-muted">
                    <i className="fas fa-calendar me-1" />
                    {formatDateTime(orderItem.orderTime)}
                  </small>
                  <div className="mt-1">
                    <small className="text-muted">
                      <i className="fas fa-utensils me-1" />
                      {orderItem.itemCount} {text.orderCountSuffix}
                    </small>
                  </div>
                </div>
                <div className="col-md-4 order-history-status">{renderOrderStatus(orderItem.orderStatus ?? orderItem.statusCode)}</div>
                <div className="col-md-3 text-md-end mt-3 mt-md-0">
                  <strong className="text-danger order-history-total">{formatCurrency(orderItem.totalAmount)}</strong>
                </div>
              </div>
            </div>
          ))
        ) : (
          <div className="empty-state">
            <i className="fas fa-inbox fs-1 mb-3" />
            <p>{text.noOrders}</p>
            <Link to="/Home/Index" className="btn btn-primary mt-3">
              <i className="fas fa-utensils me-2" />
              {text.orderNow}
            </Link>
          </div>
        )}
      </section>
    </div>
  );
}
