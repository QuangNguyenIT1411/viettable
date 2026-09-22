import { useEffect, useState } from "react";
import { useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { AdminLayout } from "../../components/AdminLayout";
import { AdminPagination } from "../../components/AdminPagination";
import { useAppDialog } from "../../components/AppDialog";
import { adminApi } from "../../lib/api";
import type { AdminCustomerDto, AdminCustomersScreenDto, StaffSessionUserDto } from "../../lib/types";
import { useAutoDismissMessage } from "../../lib/useAutoDismissMessage";

type Props = {
  mode: "index" | "create" | "edit";
  onLogout: () => Promise<void>;
};

const emptyCustomerForm = {
  name: "",
  username: "",
  password: "",
  phoneNumber: "",
  email: "",
  address: "",
  gender: "",
  dateOfBirth: "",
  loyaltyPoints: "0",
  isActive: true,
};

export function CustomersModulePage({ mode, onLogout }: Props) {
  const location = useLocation();
  const navigate = useNavigate();
  const { customerId } = useParams();
  const [searchParams] = useSearchParams();
  const [staff, setStaff] = useState<StaffSessionUserDto | null>(null);
  const [screen, setScreen] = useState<AdminCustomersScreenDto | null>(null);
  const [form, setForm] = useState(emptyCustomerForm);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useAutoDismissMessage(5000);
  const { confirm, Dialog } = useAppDialog();

  const page = Math.max(1, Number.parseInt(searchParams.get("page") ?? "1", 10) || 1);
  const customerIdValue = customerId ? Number.parseInt(customerId, 10) : 0;
  
  const [searchInput, setSearchInput] = useState("");
  const [searchQuery, setSearchQuery] = useState("");

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

      if (mode === "index") {
        setScreen(await adminApi.getCustomers(searchQuery, page, 10));
      } else if (mode === "create") {
        setScreen(await adminApi.getCustomers("", 1, 10));
      } else {
        if (!customerIdValue) {
          navigate("/Admin/Customers/Index", { replace: true });
          return;
        }
        const [next, customer] = await Promise.all([
          adminApi.getCustomers("", 1, 10),
          adminApi.getCustomerById(customerIdValue),
        ]);
        setScreen(next);
        setForm({
          name: customer.name,
          username: customer.username,
          password: "",
          phoneNumber: customer.phoneNumber ?? "",
          email: customer.email ?? "",
          address: customer.address ?? "",
          gender: customer.gender ?? "",
          dateOfBirth: customer.dateOfBirth ?? "",
          loyaltyPoints: String(customer.loyaltyPoints),
          isActive: customer.isActive,
        });
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể tải dữ liệu khách hàng.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadPage();
  }, [mode, searchQuery, page, customerIdValue]);

  function handleSearchSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSearchQuery(searchInput);
  }

  function buildIndexUrl(nextPage = page) {
    const params = new URLSearchParams();
    if (nextPage > 1) params.set("page", String(nextPage));
    return `/Admin/Customers/Index${params.toString() ? `?${params.toString()}` : ""}`;
  }

  async function handleCreate() {
    if (!form.name.trim() || !form.username.trim() || !form.password.trim()) {
      setError("Vui lòng nhập đầy đủ họ tên, tên đăng nhập và mật khẩu.");
      return;
    }

    try {
      const response = await adminApi.createCustomer({
        name: form.name.trim(),
        username: form.username.trim(),
        password: form.password.trim(),
        phoneNumber: form.phoneNumber.trim() || null,
        email: form.email.trim() || null,
        gender: form.gender || null,
        dateOfBirth: form.dateOfBirth || null,
        address: form.address.trim() || null,
        loyaltyPoints: Number(form.loyaltyPoints || "0"),
        isActive: form.isActive,
      });
      navigate("/Admin/Customers/Index", { replace: true, state: { message: response.message } });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể thêm khách hàng.");
    }
  }

  async function handleEdit() {
    if (!customerIdValue) return;
    if (!form.name.trim() || !form.username.trim()) {
      setError("Vui lòng nhập đầy đủ họ tên và tên đăng nhập.");
      return;
    }

    try {
      const response = await adminApi.updateCustomer(customerIdValue, {
        name: form.name.trim(),
        username: form.username.trim(),
        password: form.password.trim() || null,
        phoneNumber: form.phoneNumber.trim() || null,
        email: form.email.trim() || null,
        gender: form.gender || null,
        dateOfBirth: form.dateOfBirth || null,
        address: form.address.trim() || null,
        loyaltyPoints: Number(form.loyaltyPoints || "0"),
        isActive: form.isActive,
      });
      navigate("/Admin/Customers/Index", { replace: true, state: { message: response.message } });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể cập nhật khách hàng.");
    }
  }

  async function handleDeactivate(customerIdToDeactivate: number) {
    const approved = await confirm({
      title: "Xác nhận vô hiệu",
      message: "Bạn có chắc muốn khóa khách hàng này không?",
      confirmLabel: "Vô hiệu",
      cancelLabel: "Hủy",
      variant: "danger",
    });
    if (!approved) return;
    try {
      const response = await adminApi.deactivateCustomer(customerIdToDeactivate);
      setMessage(response.message);
      await loadPage();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể khóa khách hàng.");
    }
  }
  async function handleSetActive(customer: AdminCustomerDto, isActive: boolean) {
    try {
      await adminApi.updateCustomer(customer.customerId, {
        name: customer.name,
        username: customer.username,
        password: null,
        phoneNumber: customer.phoneNumber ?? null,
        email: customer.email ?? null,
        gender: customer.gender ?? null,
        dateOfBirth: customer.dateOfBirth ?? null,
        address: customer.address ?? null,
        loyaltyPoints: customer.loyaltyPoints,
        isActive,
      });
      setMessage(isActive ? "Đã bật lại khách hàng." : "Đã khóa khách hàng.");
      await loadPage();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể cập nhật trạng thái khách hàng.");
    }
  }

  const title = mode === "create" ? "Thêm khách hàng" : mode === "edit" ? "Cập nhật khách hàng" : "Quản lý khách hàng";
  const description = mode === "create" ? "Tạo mới tài khoản khách hàng." : mode === "edit" ? "Cập nhật thông tin khách hàng." : "Quản lý tài khoản khách hàng.";

  return (
    <AdminLayout title={title} description={description} staff={staff} onLogout={onLogout} onRefresh={loadPage} message={message} error={error}>
      {loading ? <div className="screen-message">Đang tải dữ liệu khách hàng...</div> : null}

      {!loading && mode === "index" && screen ? (
        <section className="panel">
          <div className="toolbar-card">
            <div>
              <strong>Danh sách khách hàng</strong>
              <div className="muted">Tìm kiếm, chỉnh sửa, khóa tài khoản và theo dõi điểm thưởng.</div>
            </div>
            <button className="ghost" onClick={() => navigate("/Admin/Customers/Create")}>Thêm khách hàng</button>
          </div>

          <div className="inline-filter-card customer-filter-card">
            <div>
              <strong>Bộ lọc tìm kiếm</strong>
              <div className="muted">Tìm theo tên, tên đăng nhập, số điện thoại hoặc email.</div>
            </div>
            <form className="admin-filter-form" onSubmit={handleSearchSubmit}>
              <label className="admin-filter-field admin-filter-field-wide">
                <span>Từ khóa</span>
                <input 
                  value={searchInput} 
                  onChange={(e) => setSearchInput(e.target.value)} 
                  placeholder="Tên, tài khoản, số điện thoại... (Enter để tìm)" 
                />
              </label>
              <div className="admin-filter-actions">
                <button type="submit" className="ghost">Tìm kiếm</button>
                <button type="button" className="ghost" onClick={() => { setSearchInput(""); setSearchQuery(""); }}>Xóa bộ lọc</button>
              </div>
            </form>
          </div>

          <table className="data-table">
            <thead>
              <tr>
                <th>Khách hàng</th>
                <th>Tài khoản</th>
                <th>Liên hệ</th>
                <th>Điểm</th>
                <th>Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {screen.customers.items.length === 0 ? (
                <tr>
                  <td colSpan={6}>
                    <div className="empty-report compact-empty">Không tìm thấy khách hàng phù hợp.</div>
                  </td>
                </tr>
              ) : screen.customers.items.map((customer) => (
                <tr key={customer.customerId}>
                  <td><strong>{customer.name}</strong></td>
                  <td>{customer.username}</td>
                  <td>
                    <div className="contact-stack">
                      <span>{customer.phoneNumber || "-"}</span>
                      <span className="muted-caption">{customer.email || "-"}</span>
                    </div>
                  </td>
                  <td>{customer.loyaltyPoints.toLocaleString("vi-VN")} điểm</td>
                  <td>{customer.isActive ? <span className="status-pill success">Hoạt động</span> : <span className="status-pill danger">Khóa</span>}</td>
                  <td>
                    <div className="button-row wrap">
                      <button className="ghost" onClick={() => navigate(`/Admin/Customers/Edit/${customer.customerId}`)}>Sửa</button>
                      <button className="ghost" onClick={() => navigate(`/Admin/Customers/${customer.customerId}/ActivityLogs`)}>Nhật ký</button>
                      {customer.isActive ? (
                        <button className="danger" onClick={() => void handleDeactivate(customer.customerId)}>Khóa</button>
                      ) : (
                        <button className="ghost" onClick={() => void handleSetActive(customer, true)}>Bật lại</button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {screen.customers.totalPages > 1 ? (
            <AdminPagination
              currentPage={screen.customers.page}
              totalPages={screen.customers.totalPages}
              onPageChange={(pageNumber) => navigate(buildIndexUrl(pageNumber))}
              keyPrefix="customer"
            />
          ) : null}
        </section>
      ) : null}

      {!loading && (mode === "create" || mode === "edit") ? (
        <section className="panel">
          <article className={`entry-form-card ${mode === "edit" ? "edit-form-card" : ""}`}>
            <div className="entry-form-header">
              <div>
                <strong>{mode === "create" ? "Thêm khách hàng" : "Cập nhật khách hàng"}</strong>
                <div className="muted">{mode === "edit" ? "Để trống mật khẩu nếu không đổi." : "Tạo mới tài khoản khách hàng."}</div>
              </div>
            </div>
            <div className="entry-form-grid">
              <label>Họ tên
                <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
              </label>
              <label>Tên đăng nhập
                <input value={form.username} onChange={(e) => setForm({ ...form, username: e.target.value })} />
              </label>
              <label>{mode === "create" ? "Mật khẩu" : "Mật khẩu mới"}
                <input type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} placeholder={mode === "edit" ? "Để trống nếu không đổi" : ""} />
              </label>
              <label>Số điện thoại
                <input value={form.phoneNumber} onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })} />
              </label>
              <label>Email
                <input value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
              </label>
              <label>Địa chỉ
                <input value={form.address} onChange={(e) => setForm({ ...form, address: e.target.value })} />
              </label>
              <label>Giới tính
                <select value={form.gender} onChange={(e) => setForm({ ...form, gender: e.target.value })}>
                  <option value="">Chọn giới tính</option>
                  <option value="Nam">Nam</option>
                  <option value="Nữ">Nữ</option>
                  <option value="Khác">Khác</option>
                </select>
              </label>
              <label>Ngày sinh
                <input type="date" value={form.dateOfBirth} onChange={(e) => setForm({ ...form, dateOfBirth: e.target.value })} />
              </label>
              <label>Điểm thưởng
                <input type="number" value={form.loyaltyPoints} onChange={(e) => setForm({ ...form, loyaltyPoints: e.target.value })} />
              </label>
              <label className="admin-checkbox-field">
                <span>Trạng thái</span>
                <div className="checkbox-inline">
                  <input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />
                  <span>Hoạt động</span>
                </div>
              </label>
            </div>
            <div className="entry-form-actions">
              <button className="ghost" onClick={() => navigate("/Admin/Customers/Index")}>Hủy</button>
              <button onClick={() => void (mode === "create" ? handleCreate() : handleEdit())}>{mode === "create" ? "Lưu khách hàng" : "Lưu thay đổi"}</button>
            </div>
          </article>
        </section>
      ) : null}
      <Dialog />
    </AdminLayout>
  );
}
