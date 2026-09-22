import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { AdminLayout } from "../../components/AdminLayout";
import { AdminPagination } from "../../components/AdminPagination";
import { adminApi } from "../../lib/api";
import type { StaffSessionUserDto, AdminCustomerOrderHistoryItemDto, Paged } from "../../lib/types";

type Props = { onLogout: () => Promise<void> };

interface ActivityLog {
  auditId: number;
  timestampUtc: string;
  actionType: string;
  entityType: string;
  entityId: string;
  actorType?: string;
  actorId?: number;
  actorName?: string;
  ipAddress?: string;
  userAgent?: string;
  notes?: string;
  beforeState?: string;
  afterState?: string;
}

interface CustomerInfo {
  customerId: number;
  name: string;
  username: string;
  email?: string;
  phoneNumber?: string;
}

interface ActivityLogsResponse {
  customer: CustomerInfo;
  logs: {
    page: number;
    pageSize: number;
    totalItems: number;
    totalPages: number;
    items: ActivityLog[];
  };
}

export function CustomerActivityLogsPage({ onLogout }: Props) {
  const navigate = useNavigate();
  const { customerId } = useParams();
  const [staff, setStaff] = useState<StaffSessionUserDto | null>(null);
  const [data, setData] = useState<ActivityLogsResponse | null>(null);
  const [orderHistory, setOrderHistory] = useState<Paged<AdminCustomerOrderHistoryItemDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activityPage, setActivityPage] = useState(1);
  const [orderPage, setOrderPage] = useState(1);
  const activityPageSize = 50;
  const orderPageSize = 20;

  const customerIdValue = customerId ? Number.parseInt(customerId, 10) : 0;

  async function loadPage() {
    if (!customerIdValue) {
      navigate("/Admin/Customers/Index", { replace: true });
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const session = await adminApi.getSession();
      setStaff(session.staff ?? null);

      const [activityResult, orderResult] = await Promise.all([
        adminApi.getCustomerActivityLogs(customerIdValue, activityPage, activityPageSize),
        adminApi.getCustomerOrderHistory(customerIdValue, orderPage, orderPageSize, 90),
      ]);
      
      setData(activityResult);
      setOrderHistory(orderResult);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể tải dữ liệu.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadPage();
  }, [customerIdValue, activityPage, orderPage]);

  function formatTimestamp(utcString: string): string {
    const date = new Date(utcString);
    return date.toLocaleString("vi-VN", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    });
  }

  function getActionLabel(actionType: string): string {
    const labels: Record<string, string> = {
      "customer.login": "Đăng nhập",
      "customer.login.google": "Đăng nhập Google",
      "customer.register": "Đăng ký tài khoản",
      "customer.password.change": "Đổi mật khẩu",
      "customer.password.forgot": "Quên mật khẩu",
      "customer.password.reset": "Đặt lại mật khẩu",
      "customer.profile.update": "Cập nhật thông tin",
    };
    return labels[actionType] || actionType;
  }

  function getActionColor(actionType: string): string {
    if (actionType.includes("login")) return "success";
    if (actionType.includes("register")) return "info";
    if (actionType.includes("password")) return "warning";
    if (actionType.includes("update")) return "primary";
    return "default";
  }

  function getOrderStatusColor(statusCode: string): string {
    switch (statusCode) {
      case "PENDING":
        return "warning";
      case "PREPARING":
        return "info";
      case "READY":
        return "primary";
      case "COMPLETED":
        return "success";
      case "CANCELLED":
        return "danger";
      default:
        return "default";
    }
  }

  return (
    <AdminLayout
      title={`Nhật ký hoạt động - ${data?.customer.name || "Khách hàng"}`}
      description="Xem lịch sử hoạt động và đơn hàng của khách hàng"
      staff={staff}
      onLogout={onLogout}
      onRefresh={loadPage}
      error={error}
    >
      {loading ? <div className="screen-message">Đang tải dữ liệu...</div> : null}

      {!loading && data ? (
        <>
          <section className="panel">
            <div className="toolbar-card">
              <div>
                <strong>Thông tin khách hàng</strong>
                <div className="muted">
                  <div><strong>Tên:</strong> {data.customer.name}</div>
                  <div><strong>Tài khoản:</strong> {data.customer.username}</div>
                  {data.customer.email && <div><strong>Email:</strong> {data.customer.email}</div>}
                  {data.customer.phoneNumber && <div><strong>SĐT:</strong> {data.customer.phoneNumber}</div>}
                </div>
              </div>
              <button className="ghost" onClick={() => navigate("/Admin/Customers/Index")}>
                ← Quay lại danh sách
              </button>
            </div>

            <div className="toolbar-card">
              <div>
                <strong>Nhật ký hoạt động</strong>
                <div className="muted">
                  Tổng số: {data.logs.totalItems} hoạt động | Trang {data.logs.page}/{data.logs.totalPages}
                </div>
              </div>
            </div>

            {data.logs.items.length === 0 ? (
              <div className="empty-report compact-empty">Chưa có hoạt động nào được ghi nhận.</div>
            ) : (
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Thời gian</th>
                    <th>Hoạt động</th>
                    <th>IP Address</th>
                    <th>Ghi chú</th>
                  </tr>
                </thead>
                <tbody>
                  {data.logs.items.map((log) => (
                    <tr key={log.auditId}>
                      <td style={{ whiteSpace: "nowrap" }}>{formatTimestamp(log.timestampUtc)}</td>
                      <td>
                        <span className={`status-pill ${getActionColor(log.actionType)}`}>
                          {getActionLabel(log.actionType)}
                        </span>
                      </td>
                      <td>
                        <div className="contact-stack">
                          <span>{log.ipAddress || "-"}</span>
                          {log.userAgent && (
                            <span className="muted-caption" style={{ fontSize: "0.75rem", maxWidth: "200px", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                              {log.userAgent}
                            </span>
                          )}
                        </div>
                      </td>
                      <td>
                        <div style={{ maxWidth: "300px" }}>
                          {log.notes || "-"}
                          {log.afterState && (
                            <details style={{ marginTop: "0.5rem" }}>
                              <summary style={{ cursor: "pointer", color: "#666" }}>Chi tiết</summary>
                              <pre style={{ fontSize: "0.75rem", background: "#f5f5f5", padding: "0.5rem", borderRadius: "4px", overflow: "auto", maxHeight: "200px" }}>
                                {JSON.stringify(JSON.parse(log.afterState), null, 2)}
                              </pre>
                            </details>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}

            {data.logs.totalPages > 1 ? (
              <AdminPagination
                currentPage={data.logs.page}
                totalPages={data.logs.totalPages}
                onPageChange={setActivityPage}
                keyPrefix="activity-log"
              />
            ) : null}
          </section>

          {orderHistory && (
            <section className="panel" style={{ marginTop: "2rem" }}>
              <div className="toolbar-card">
                <div>
                  <strong>Nhật ký đặt món</strong>
                  <div className="muted">
                    Tổng số: {orderHistory.totalItems} đơn hàng | Trang {orderHistory.page}/{orderHistory.totalPages}
                  </div>
                </div>
              </div>

              {orderHistory.items.length === 0 ? (
                <div className="empty-report compact-empty">Chưa có đơn hàng nào.</div>
              ) : (
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Mã đơn</th>
                      <th>Thời gian</th>
                      <th>Bàn</th>
                      <th>Trạng thái</th>
                      <th>Món ăn</th>
                      <th style={{ textAlign: "right" }}>Tổng tiền</th>
                    </tr>
                  </thead>
                  <tbody>
                    {orderHistory.items.map((order) => (
                      <tr key={order.orderId}>
                        <td style={{ whiteSpace: "nowrap" }}>{order.orderCode || `#${order.orderId}`}</td>
                        <td style={{ whiteSpace: "nowrap" }}>{formatTimestamp(order.orderTime)}</td>
                        <td>{order.tableName || "-"}</td>
                        <td>
                          <span className={`status-pill ${getOrderStatusColor(order.statusCode)}`}>
                            {order.statusName}
                          </span>
                        </td>
                        <td>
                          <div style={{ maxWidth: "300px", overflow: "hidden", textOverflow: "ellipsis" }}>
                            {order.dishesSummary}
                          </div>
                        </td>
                        <td style={{ textAlign: "right", whiteSpace: "nowrap" }}>
                          {order.totalAmount.toLocaleString("vi-VN")} đ
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}

              {orderHistory.totalPages > 1 ? (
                <AdminPagination
                  currentPage={orderHistory.page}
                  totalPages={orderHistory.totalPages}
                  onPageChange={setOrderPage}
                  keyPrefix="order-history"
                />
              ) : null}
            </section>
          )}
        </>
      ) : null}
    </AdminLayout>
  );
}
