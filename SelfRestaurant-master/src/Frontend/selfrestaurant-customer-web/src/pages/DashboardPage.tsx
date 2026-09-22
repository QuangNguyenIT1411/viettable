import { FormEvent, useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { api } from "../lib/api";
import { toMvcPath } from "../lib/mvcPaths";

const text = {
  loading: "Đang tải hồ sơ khách hàng...",
  backHome: "Quay Về Trang Chủ",
  greetingPrefix: "Xin chào,",
  greetingSuffix: "Đây là trang quản lý hồ sơ và đơn hàng của bạn",
  points: "Điểm Thưởng",
  orders: "Đơn Hàng",
  email: "Email",
  phone: "SĐT",
  none: "Chưa có",
  profileTab: "Thông Tin Hồ Sơ",
  passwordTab: "Đổi Mật Khẩu",
  name: "Họ Tên",
  gender: "Giới Tính",
  address: "Địa Chỉ",
  dateOfBirth: "Ngày Sinh",
  saveProfile: "Cập Nhật Thông Tin",
  currentPassword: "Mật Khẩu Hiện Tại",
  newPassword: "Mật Khẩu Mới",
  confirmPassword: "Xác Nhận Mật Khẩu Mới",
  passwordHint: "Mật khẩu mới phải có ít nhất 6 ký tự",
  changePassword: "Đổi Mật Khẩu",
  recentOrders: "Lịch Sử Đơn Hàng Gần Đây",
  noOrders: "Chưa có đơn hàng nào",
  orderNow: "Đặt Món Ngay",
  orderCountSuffix: "món",
  viewAllOrders: "Xem Tất Cả Đơn Hàng",
  orderCodePrefix: "Mã đơn",
  quickActions: "Hành Động Nhanh",
  newOrder: "Đặt Món Mới",
  logout: "Đăng Xuất",
  accountInfo: "Thông Tin Tài Khoản",
  authenticated: "Đã Xác Thực",
  status: "Trạng Thái",
  updatedProfile: "Đã cập nhật thông tin thành công.",
  updatedPassword: "Đã đổi mật khẩu thành công.",
  saving: "Đang lưu...",
  updatingPassword: "Đang cập nhật...",
  profileRequired: "Họ Tên",
  emailRequired: "Email",
  phoneRequired: "Số Điện Thoại",
  passwordRequired: "Mật Khẩu",
} as const;

function formatCurrency(value: number) {
  return `${value.toLocaleString("vi-VN")} `;
}

function formatDateTime(value?: string | null) {
  if (!value) return text.none;
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString("vi-VN");
}

function normalizeGender(value?: string | null) {
  const trimmed = (value ?? "").trim();
  const normalized = trimmed.toLowerCase();
  if (!trimmed) return "";
  if (normalized === "nam") return "Nam";
  if (normalized === "nu" || normalized === "nữ") return "Nữ";
  if (normalized === "khac" || normalized === "khác") return "Khác";
  return trimmed;
}

function renderOrderStatus(status?: string | null) {
  const normalized = (status ?? "").toUpperCase();

  if (normalized === "COMPLETED" || normalized === "SERVED") {
    return <span className="badge rounded-pill bg-success">Hoàn tất</span>;
  }

  if (normalized === "READY") {
    return <span className="badge rounded-pill bg-success">Sẵn sàng</span>;
  }

  if (normalized === "PREPARING" || normalized === "CONFIRMED") {
    return <span className="badge rounded-pill bg-warning text-dark">Đang chuẩn bị</span>;
  }

  if (normalized === "PENDING") {
    return <span className="badge rounded-pill bg-primary">Chờ gửi</span>;
  }

  return <span className="badge rounded-pill bg-secondary">Đang cập nhật</span>;
}

export function DashboardPage({ initialTab = "profile" }: { initialTab?: "profile" | "password" }) {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const dashboard = useQuery({ queryKey: ["dashboard"], queryFn: api.getDashboard });
  const profile = useQuery({ queryKey: ["profile"], queryFn: api.getProfile });

  const [activeTab, setActiveTab] = useState<"profile" | "password">(initialTab);
  const [profileForm, setProfileForm] = useState({
    username: "",
    name: "",
    phoneNumber: "",
    email: "",
    gender: "",
    dateOfBirth: "",
    address: "",
  });
  const [passwordForm, setPasswordForm] = useState({
    currentPassword: "",
    newPassword: "",
    confirmPassword: "",
  });
  const [dismissedFlash, setDismissedFlash] = useState(false);
  const [dismissedUpdateError, setDismissedUpdateError] = useState(false);
  const [dismissedPasswordError, setDismissedPasswordError] = useState(false);
  const flashMessage = searchParams.get("message");
  const flashType = searchParams.get("type") ?? "success";
  const flashTab = searchParams.get("tab");

  useEffect(() => {
    if (!profile.data) return;
    setProfileForm({
      username: profile.data.username,
      name: profile.data.name,
      phoneNumber: profile.data.phoneNumber,
      email: profile.data.email ?? "",
      gender: normalizeGender(profile.data.gender),
      dateOfBirth: profile.data.dateOfBirth ?? "",
      address: profile.data.address ?? "",
    });
  }, [profile.data]);

  useEffect(() => {
    if (flashTab === "password" || flashTab === "profile") {
      setActiveTab(flashTab);
    }
  }, [flashTab]);

  const updateProfile = useMutation({
    mutationFn: api.updateProfile,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["profile"] });
      await queryClient.invalidateQueries({ queryKey: ["dashboard"] });
      await queryClient.invalidateQueries({ queryKey: ["session"] });
      setDismissedFlash(false);
      setSearchParams({ message: text.updatedProfile, type: "success", tab: "profile" }, { replace: true });
    },
  });

  const changePassword = useMutation({
    mutationFn: api.changePassword,
    onSuccess: () => {
      setPasswordForm({ currentPassword: "", newPassword: "", confirmPassword: "" });
      setDismissedFlash(false);
      setSearchParams({ message: text.updatedPassword, type: "success", tab: "password" }, { replace: true });
    },
  });

  const logout = useMutation({
    mutationFn: api.logout,
    onSuccess: async (result) => {
      await queryClient.invalidateQueries();
      navigate(toMvcPath(result.nextPath));
    },
  });

  if (dashboard.isLoading || profile.isLoading) {
    return (
      <div className="container py-4">
        <div className="alert alert-info mb-0" role="alert">
          <i className="fas fa-info-circle me-2" />
          {text.loading}
        </div>
      </div>
    );
  }

  if (dashboard.error) {
    return (
      <div className="container py-4">
        <div className="alert alert-danger mb-0" role="alert">
          <i className="fas fa-exclamation-circle me-2" />
          {(dashboard.error as Error).message}
        </div>
      </div>
    );
  }

  const data = dashboard.data!;
  const recentOrders = data.recentOrders ?? [];
  const hasPasswordMismatch =
    passwordForm.newPassword !== "" &&
    passwordForm.confirmPassword !== "" &&
    passwordForm.newPassword !== passwordForm.confirmPassword;

  const onSubmitProfile = (event: FormEvent) => {
    event.preventDefault();
    setDismissedUpdateError(false);
    updateProfile.mutate({
      username: profileForm.username,
      name: profileForm.name,
      phoneNumber: profileForm.phoneNumber,
      email: profileForm.email,
      gender: profileForm.gender || null,
      dateOfBirth: profileForm.dateOfBirth || null,
      address: profileForm.address || null,
    });
  };

  const onSubmitPassword = (event: FormEvent) => {
    event.preventDefault();
    setDismissedPasswordError(false);
    if (hasPasswordMismatch) return;
    changePassword.mutate(passwordForm);
  };

  return (
    <div className="customer-dashboard-page">
      <div className="dashboard-header">
        <div className="container">
          <Link to="/Home/Index" className="dashboard-back-link">
            <i className="fas fa-arrow-left" />
            {text.backHome}
          </Link>
          <h1 className="mb-1">
            {text.greetingPrefix} {data.customer.name}!
          </h1>
          <p className="mb-0">{text.greetingSuffix}</p>
        </div>
      </div>

      <div className="container">
        {flashMessage && !dismissedFlash ? (
          <div className={`alert ${flashType === "error" ? "alert-danger" : "alert-success"} alert-dismissible fade show`} role="alert">
            <i className={`fas ${flashType === "error" ? "fa-exclamation-circle" : "fa-check-circle"} me-2`} />
            {flashMessage}
            <button
              type="button"
              className="btn-close"
              aria-label="Close"
              onClick={() => {
                setDismissedFlash(true);
                setSearchParams({}, { replace: true });
              }}
            />
          </div>
        ) : null}
        {updateProfile.error && !dismissedUpdateError ? (
          <div className="alert alert-danger alert-dismissible fade show" role="alert">
            <i className="fas fa-exclamation-circle me-2" />
            {(updateProfile.error as Error).message}
            <button type="button" className="btn-close" aria-label="Close" onClick={() => setDismissedUpdateError(true)} />
          </div>
        ) : null}
        {changePassword.error && !dismissedPasswordError ? (
          <div className="alert alert-danger alert-dismissible fade show" role="alert">
            <i className="fas fa-exclamation-circle me-2" />
            {(changePassword.error as Error).message}
            <button type="button" className="btn-close" aria-label="Close" onClick={() => setDismissedPasswordError(true)} />
          </div>
        ) : null}

        <div className="row g-4 mb-4">
          <div className="col-md-3">
            <div className="stat-box">
              <div className="stat-icon">
                <i className="fas fa-star text-warning" />
              </div>
              <div className="stat-value">{data.customer.loyaltyPoints}</div>
              <div className="stat-label">{text.points}</div>
            </div>
          </div>
          <div className="col-md-3">
            <div className="stat-box">
              <div className="stat-icon">
                <i className="fas fa-receipt text-info" />
              </div>
              <div className="stat-value">{data.summary.totalOrders}</div>
              <div className="stat-label">{text.orders}</div>
            </div>
          </div>
          <div className="col-md-3">
            <div className="stat-box">
              <div className="stat-icon">
                <i className="fas fa-envelope text-success" />
              </div>
              <div className="stat-value dashboard-stat-text">{data.customer.email || text.none}</div>
              <div className="stat-label">{text.email}</div>
            </div>
          </div>
          <div className="col-md-3">
            <div className="stat-box">
              <div className="stat-icon">
                <i className="fas fa-phone text-primary" />
              </div>
              <div className="stat-value dashboard-stat-text">{data.customer.phoneNumber || text.none}</div>
              <div className="stat-label">{text.phone}</div>
            </div>
          </div>
        </div>

        <div className="row">
          <div className="col-lg-8">
            <div className="profile-card">
              <ul className="nav nav-tabs mvc-dashboard-tabs" role="tablist">
                <li className="nav-item" role="presentation">
                  <button
                    className={`nav-link ${activeTab === "profile" ? "active" : ""}`}
                    type="button"
                    onClick={() => setActiveTab("profile")}
                  >
                    <i className="fas fa-user me-2" />
                    {text.profileTab}
                  </button>
                </li>
                <li className="nav-item" role="presentation">
                  <button
                    className={`nav-link ${activeTab === "password" ? "active" : ""}`}
                    type="button"
                    onClick={() => setActiveTab("password")}
                  >
                    <i className="fas fa-lock me-2" />
                    {text.passwordTab}
                  </button>
                </li>
              </ul>

              <div className="dashboard-tab-content">
                {activeTab === "profile" ? (
                  <form onSubmit={onSubmitProfile}>
                    <div className="row">
                      <div className="col-md-6">
                        <div className="form-group">
                          <label htmlFor="dashboard-name">
                            <i className="fas fa-user me-2" />
                            {text.name} <span className="text-danger">*</span>
                          </label>
                          <input
                            id="dashboard-name"
                            className="form-control"
                            value={profileForm.name}
                            onChange={(event) => setProfileForm({ ...profileForm, name: event.target.value })}
                            required
                          />
                        </div>
                      </div>
                      <div className="col-md-6">
                        <div className="form-group">
                          <label htmlFor="dashboard-email">
                            <i className="fas fa-envelope me-2" />
                            {text.email} <span className="text-danger">*</span>
                          </label>
                          <input
                            id="dashboard-email"
                            className="form-control"
                            type="email"
                            value={profileForm.email}
                            onChange={(event) => setProfileForm({ ...profileForm, email: event.target.value })}
                            required
                          />
                        </div>
                      </div>
                    </div>

                    <div className="row">
                      <div className="col-md-6">
                        <div className="form-group">
                          <label htmlFor="dashboard-phone">
                            <i className="fas fa-phone me-2" />
                            {text.phone} <span className="text-danger">*</span>
                          </label>
                          <input
                            id="dashboard-phone"
                            className="form-control"
                            value={profileForm.phoneNumber}
                            onChange={(event) => setProfileForm({ ...profileForm, phoneNumber: event.target.value })}
                            required
                          />
                        </div>
                      </div>
                      <div className="col-md-6">
                        <div className="form-group">
                          <label htmlFor="dashboard-gender">
                            <i className="fas fa-venus-mars me-2" />
                            {text.gender}
                          </label>
                          <select
                            id="dashboard-gender"
                            className="form-control"
                            value={profileForm.gender}
                            onChange={(event) => setProfileForm({ ...profileForm, gender: event.target.value })}
                          >
                            <option value="">-- Chọn --</option>
                            <option value="Nam">Nam</option>
                            <option value="Nữ">Nữ</option>
                            <option value="Khác">Khác</option>
                          </select>
                        </div>
                      </div>
                    </div>

                    <div className="form-group">
                      <label htmlFor="dashboard-address">
                        <i className="fas fa-map-marker-alt me-2" />
                        {text.address}
                      </label>
                      <input
                        id="dashboard-address"
                        className="form-control"
                        value={profileForm.address}
                        onChange={(event) => setProfileForm({ ...profileForm, address: event.target.value })}
                      />
                    </div>

                    <div className="form-group">
                      <label htmlFor="dashboard-dob">
                        <i className="fas fa-birthday-cake me-2" />
                        {text.dateOfBirth}
                      </label>
                      <input
                        id="dashboard-dob"
                        className="form-control"
                        type="date"
                        value={profileForm.dateOfBirth}
                        onChange={(event) => setProfileForm({ ...profileForm, dateOfBirth: event.target.value })}
                      />
                    </div>

                    <button type="submit" className="btn btn-primary" disabled={updateProfile.isPending}>
                      <i className="fas fa-save me-2" />
                      {updateProfile.isPending ? text.saving : text.saveProfile}
                    </button>
                  </form>
                ) : (
                  <form onSubmit={onSubmitPassword}>
                    <div className="alert alert-info">
                      <i className="fas fa-info-circle me-2" />
                      {text.passwordHint}
                    </div>

                    <div className="form-group">
                      <label htmlFor="dashboard-current-password">
                        <i className="fas fa-lock me-2" />
                        {text.currentPassword} <span className="text-danger">*</span>
                      </label>
                      <input
                        id="dashboard-current-password"
                        className="form-control"
                        type="password"
                        value={passwordForm.currentPassword}
                        onChange={(event) => setPasswordForm({ ...passwordForm, currentPassword: event.target.value })}
                        required
                      />
                    </div>

                    <div className="form-group">
                      <label htmlFor="dashboard-new-password">
                        <i className="fas fa-key me-2" />
                        {text.newPassword} <span className="text-danger">*</span>
                      </label>
                      <input
                        id="dashboard-new-password"
                        className="form-control"
                        type="password"
                        value={passwordForm.newPassword}
                        onChange={(event) => setPasswordForm({ ...passwordForm, newPassword: event.target.value })}
                        required
                        minLength={6}
                      />
                    </div>

                    <div className="form-group">
                      <label htmlFor="dashboard-confirm-password">
                        <i className="fas fa-check-circle me-2" />
                        {text.confirmPassword} <span className="text-danger">*</span>
                      </label>
                      <input
                        id="dashboard-confirm-password"
                        className="form-control"
                        type="password"
                        value={passwordForm.confirmPassword}
                        onChange={(event) => setPasswordForm({ ...passwordForm, confirmPassword: event.target.value })}
                        required
                        minLength={6}
                      />
                    </div>

                    {hasPasswordMismatch ? <div className="field-error mb-3">Mật khẩu xác nhận không khớp</div> : null}

                    <button type="submit" className="btn btn-warning" disabled={changePassword.isPending || hasPasswordMismatch}>
                      <i className="fas fa-key me-2" />
                      {changePassword.isPending ? text.updatingPassword : text.changePassword}
                    </button>
                  </form>
                )}
              </div>
            </div>

            <div className="orders-section" id="order-history">
              <h3 className="mb-4">
                <i className="fas fa-receipt me-2" />
                {text.recentOrders}
              </h3>

              {recentOrders.length > 0 ? (
                <>
                  {recentOrders.map((orderItem) => (
                    <div key={orderItem.orderId} className="order-item">
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
                        <div className="col-md-4">{renderOrderStatus(orderItem.statusCode ?? orderItem.statusName)}</div>
                        <div className="col-md-3 text-md-end mt-3 mt-md-0">
                          <strong className="text-danger">{formatCurrency(orderItem.totalAmount)}</strong>
                        </div>
                      </div>
                    </div>
                  ))}

                  <div className="text-center mt-3">
                    <Link to="/Customer/Orders" className="btn btn-outline-primary">
                      <i className="fas fa-list me-2" />
                      {text.viewAllOrders}
                    </Link>
                  </div>
                </>
              ) : (
                <div className="empty-state">
                  <i className="fas fa-inbox fs-1 mb-3" />
                  <p>{text.noOrders}</p>
                  <Link to="/Home/Index?flow=new-order" className="btn btn-primary mt-3">
                    <i className="fas fa-utensils me-2" />
                    {text.orderNow}
                  </Link>
                </div>
              )}
            </div>
          </div>

          <div className="col-lg-4">
            <div className="profile-card">
              <h4 className="mb-3">
                <i className="fas fa-lightning-bolt me-2" />
                {text.quickActions}
              </h4>
              <Link to="/Home/Index?flow=new-order" className="btn btn-primary w-100 mb-2">
                <i className="fas fa-plus me-2" />
                {text.newOrder}
              </Link>
              <button
                type="button"
                className="btn btn-outline-danger w-100"
                onClick={() => logout.mutate()}
                disabled={logout.isPending}
              >
                <i className="fas fa-sign-out-alt me-2" />
                {logout.isPending ? "ang ng xut..." : text.logout}
              </button>
            </div>

            <div className="profile-card">
              <h4 className="mb-3">
                <i className="fas fa-info-circle me-2" />
                {text.accountInfo}
              </h4>

              <div className="info-box">
                <p className="info-box-label">
                  <i className="fas fa-star me-1" />
                  <strong>{text.points}</strong>
                </p>
                <p className="info-box-value">
                  {data.customer.loyaltyPoints} <span className="info-box-unit">im</span>
                </p>
              </div>

              <div className="info-box">
                <p className="info-box-label">
                  <i className="fas fa-user-check me-1" />
                  <strong>{text.status}</strong>
                </p>
                <p className="info-box-status">
                  <i className="fas fa-check-circle me-1" />
                  {text.authenticated}
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}


