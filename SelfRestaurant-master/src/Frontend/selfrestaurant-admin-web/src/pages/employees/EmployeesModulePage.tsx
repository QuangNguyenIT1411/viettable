import { useEffect, useState } from "react";
import { useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { AdminLayout } from "../../components/AdminLayout";
import { AdminPagination } from "../../components/AdminPagination";
import { useAppDialog } from "../../components/AppDialog";
import { adminApi } from "../../lib/api";
import type { AdminEmployeeDto, AdminEmployeeHistoryResponse, AdminEmployeesScreenDto, StaffSessionUserDto } from "../../lib/types";
import { useAutoDismissMessage } from "../../lib/useAutoDismissMessage";

type Props = {
  mode: "index" | "create" | "edit" | "history";
  onLogout: () => Promise<void>;
};

const emptyEmployeeForm = {
  name: "",
  username: "",
  password: "",
  phone: "",
  email: "",
  salary: "",
  shift: "",
  branchId: "",
  roleId: "",
  isActive: true,
};

function formatDateTime(value?: string | null) {
  if (!value) return "-";
  return new Date(value).toLocaleString("vi-VN");
}

function getStaffActionLabel(actionType: string) {
  const labels: Record<string, string> = {
    "staff.login": "Đăng nhập",
    "staff.password.change": "Đổi mật khẩu",
    "staff.password.forgot": "Quên mật khẩu",
    "staff.password.reset": "Đặt lại mật khẩu",
    "staff.profile.update": "Cập nhật hồ sơ",
  };
  return labels[actionType] ?? actionType;
}

function getStaffActionBadge(actionType: string) {
  if (actionType.includes("login")) return "badge bg-success";
  if (actionType.includes("password")) return "badge bg-warning text-dark";
  if (actionType.includes("profile")) return "badge bg-info";
  return "badge bg-secondary";
}

function normalizeRoleText(value?: string | null) {
  return (value ?? "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toUpperCase();
}

function getEmployeeHistoryVisibility(employee?: AdminEmployeeHistoryResponse["employee"] | null) {
  const roleCode = normalizeRoleText(employee?.roleCode);
  const roleName = normalizeRoleText(employee?.roleName);
  const roleText = `${roleCode} ${roleName}`;
  const isChefRole = roleCode === "CHEF"
    || roleCode === "KITCHEN_STAFF"
    || roleText.includes("CHEF")
    || roleText.includes("KITCHEN")
    || roleText.includes("DAU BEP")
    || roleText.includes("BEP");
  const isCashierRole = roleCode === "CASHIER"
    || roleText.includes("CASHIER")
    || roleText.includes("THU NGAN");

  // Unknown/admin/other roles keep the historical admin fallback: show both sections.
  if (!isChefRole && !isCashierRole) {
    return { showChefHistory: true, showCashierHistory: true };
  }

  return { showChefHistory: isChefRole, showCashierHistory: isCashierRole };
}

export function EmployeesModulePage({ mode, onLogout }: Props) {
  const location = useLocation();
  const navigate = useNavigate();
  const { employeeId } = useParams();
  const [searchParams] = useSearchParams();
  const [staff, setStaff] = useState<StaffSessionUserDto | null>(null);
  const [screen, setScreen] = useState<AdminEmployeesScreenDto | null>(null);
  const [history, setHistory] = useState<AdminEmployeeHistoryResponse | null>(null);
  const [activityPage, setActivityPage] = useState(1);
  const [cookingPage, setCookingPage] = useState(1);
  const [form, setForm] = useState(emptyEmployeeForm);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useAutoDismissMessage(5000);
  const { confirm, Dialog } = useAppDialog();

  const branchId = searchParams.get("branchId") ?? "ALL";
  const roleId = searchParams.get("roleId") ?? "ALL";
  const page = Math.max(1, Number.parseInt(searchParams.get("page") ?? "1", 10) || 1);
  const employeeIdValue = employeeId ? Number.parseInt(employeeId, 10) : 0;
  
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
    setHistory(null); // Reset history when loading new employee
    try {
      const session = await adminApi.getSession();
      const currentStaff = session.staff ?? null;
      setStaff(currentStaff);
      const scopedBranchId = currentStaff?.branchId;

      if (mode === "index") {
        setScreen(await adminApi.getEmployees(
          searchQuery,
          scopedBranchId,
          roleId !== "ALL" ? Number(roleId) : undefined,
          page,
          10,
        ));
      } else if (mode === "create") {
        const next = await adminApi.getEmployees("", scopedBranchId, undefined, 1, 10);
        setScreen(next);
        setForm((current) => ({
          ...current,
          branchId: String(scopedBranchId ?? next.branches[0]?.branchId ?? ""),
          roleId: current.roleId || String(next.roles[0]?.roleId ?? ""),
        }));
      } else if (mode === "edit") {
        if (!employeeIdValue) {
          navigate("/Admin/Employees/Index", { replace: true });
          return;
        }
        const [next, employee] = await Promise.all([
          adminApi.getEmployees("", scopedBranchId, undefined, 1, 10),
          adminApi.getEmployeeById(employeeIdValue),
        ]);
        setScreen(next);
        setForm({
          name: employee.name,
          username: employee.username,
          password: "",
          phone: employee.phone ?? "",
          email: employee.email ?? "",
          salary: employee.salary != null ? String(employee.salary) : "",
          shift: employee.shift ?? "",
          branchId: String(scopedBranchId ?? employee.branchId),
          roleId: String(employee.roleId),
          isActive: employee.isActive,
        });
      } else {
        if (!employeeIdValue) {
          navigate("/Admin/Employees/Index", { replace: true });
          return;
        }
        setHistory(await adminApi.getEmployeeHistory(employeeIdValue, activityPage, cookingPage, 50, 90));
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể tải dữ liệu nhân viên.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadPage();
  }, [mode, searchQuery, branchId, roleId, page, employeeIdValue, activityPage, cookingPage]);

  // Reset pagination when employee changes
  useEffect(() => {
    setActivityPage(1);
    setCookingPage(1);
  }, [employeeIdValue]);

  function handleSearchSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSearchQuery(searchInput);
  }

  function buildIndexUrl(nextPage = page, nextBranchId = branchId, nextRoleId = roleId) {
    const params = new URLSearchParams();
    if (nextBranchId !== "ALL") params.set("branchId", nextBranchId);
    if (nextRoleId !== "ALL") params.set("roleId", nextRoleId);
    if (nextPage > 1) params.set("page", String(nextPage));
    return `/Admin/Employees/Index${params.toString() ? `?${params.toString()}` : ""}`;
  }

  async function handleDeactivate(employee: AdminEmployeeDto) {
    const approved = await confirm({
      title: "Xác nhận vô hiệu",
      message: "Bạn có chắc muốn khóa nhân viên này không?",
      confirmLabel: "Vô hiệu",
      cancelLabel: "Hủy",
      variant: "danger",
    });
    if (!approved) return;
    try {
      const response = await adminApi.deactivateEmployee(employee.employeeId);
      setMessage(response.message);
      await loadPage();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể khóa nhân viên.");
    }
  }
  async function handleSetActive(employee: AdminEmployeeDto, isActive: boolean) {
    try {
      await adminApi.updateEmployee(employee.employeeId, {
        name: employee.name,
        username: employee.username,
        password: null,
        phone: employee.phone ?? null,
        email: employee.email ?? null,
        salary: employee.salary ?? null,
        shift: employee.shift ?? null,
        isActive,
        branchId: Number(staff?.branchId ?? employee.branchId),
        roleId: employee.roleId,
      });
      setMessage(isActive ? "Đã bật lại nhân viên." : "Đã khóa nhân viên.");
      await loadPage();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể cập nhật trạng thái nhân viên.");
    }
  }

  async function handleCreate() {
    if (!form.name.trim() || !form.username.trim() || !form.password.trim() || !form.branchId || !form.roleId) {
      setError("Vui lòng nhập đầy đủ họ tên, tên đăng nhập, mật khẩu, chi nhánh và vai trò.");
      return;
    }

    try {
      const response = await adminApi.createEmployee({
        name: form.name.trim(),
        username: form.username.trim(),
        password: form.password.trim(),
        phone: form.phone.trim() || null,
        email: form.email.trim() || null,
        salary: form.salary ? Number(form.salary) : null,
        shift: form.shift.trim() || null,
        isActive: form.isActive,
        branchId: Number(staff?.branchId ?? form.branchId),
        roleId: Number(form.roleId),
      });
      navigate("/Admin/Employees/Index", { replace: true, state: { message: response.message } });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể thêm nhân viên.");
    }
  }

  async function handleEdit() {
    if (!employeeIdValue) return;
    if (!form.name.trim() || !form.username.trim() || !form.branchId || !form.roleId) {
      setError("Vui lòng nhập đầy đủ họ tên, tên đăng nhập, chi nhánh và vai trò.");
      return;
    }

    try {
      const response = await adminApi.updateEmployee(employeeIdValue, {
        name: form.name.trim(),
        username: form.username.trim(),
        password: form.password.trim() || null,
        phone: form.phone.trim() || null,
        email: form.email.trim() || null,
        salary: form.salary ? Number(form.salary) : null,
        shift: form.shift.trim() || null,
        isActive: form.isActive,
        branchId: Number(staff?.branchId ?? form.branchId),
        roleId: Number(form.roleId),
      });
      navigate("/Admin/Employees/Index", { replace: true, state: { message: response.message } });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể cập nhật nhân viên.");
    }
  }

  const title = mode === "create"
    ? "Thêm nhân viên"
    : mode === "edit"
      ? "Cập nhật nhân viên"
      : mode === "history"
        ? "Nhật ký nhân viên"
        : "Quản lý nhân viên";
  const description = mode === "create"
    ? "Tạo mới tài khoản nhân viên."
    : mode === "edit"
      ? "Cập nhật thông tin nhân viên."
      : mode === "history"
        ? "Xem nhật ký hoạt động của nhân viên."
        : "Quản lý nhân sự, vai trò và chi nhánh.";

  return (
    <AdminLayout title={title} description={description} staff={staff} onLogout={onLogout} onRefresh={loadPage} message={message} error={error}>
      {loading ? <div className="screen-message">Đang tải dữ liệu nhân viên...</div> : null}

      {!loading && mode === "index" && screen ? (
        <section className="panel">
          <div className="toolbar-card">
            <div>
              <strong>Danh sách nhân viên</strong>
              <div className="muted">Tìm kiếm, lọc, chỉnh sửa, xem nhật ký và khóa tài khoản.</div>
            </div>
            <button className="ghost" onClick={() => navigate("/Admin/Employees/Create")}>Thêm nhân viên</button>
          </div>

          <div className="inline-filter-card admin-filter-card">
            <div>
              <strong>Bộ lọc tìm kiếm</strong>
              <div className="muted">Tìm theo tên, tên đăng nhập, số điện thoại, email, chi nhánh hoặc vai trò.</div>
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
              <label className="admin-filter-field">
                <span>Chi nhánh</span>
                <select value={String(staff?.branchId ?? branchId)} onChange={(e) => navigate(buildIndexUrl(1, e.target.value, roleId))} disabled>
                  {screen.branches.map((branch) => (
                    <option key={branch.branchId} value={branch.branchId}>{branch.name}</option>
                  ))}
                </select>
              </label>
              <label className="admin-filter-field">
                <span>Vai trò</span>
                <select value={roleId} onChange={(e) => navigate(buildIndexUrl(1, branchId, e.target.value))}>
                  <option value="ALL">Tất cả vai trò</option>
                  {screen.roles.map((role) => (
                    <option key={role.roleId} value={role.roleId}>{role.roleName}</option>
                  ))}
                </select>
              </label>
              <div className="admin-filter-actions">
                <button type="submit" className="ghost">Tìm kiếm</button>
                <button type="button" className="ghost" onClick={() => { setSearchInput(""); setSearchQuery(""); navigate(buildIndexUrl(1, String(staff?.branchId ?? screen.branches[0]?.branchId ?? ""), "ALL")); }}>Xóa bộ lọc</button>
              </div>
            </form>
          </div>

          <table className="data-table">
            <thead>
              <tr>
                <th>Nhân viên</th>
                <th>Tài khoản</th>
                <th>Vai trò</th>
                <th>Chi nhánh</th>
                <th>Liên hệ</th>
                <th>Ca làm</th>
                <th>Lương</th>
                <th>Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {screen.employees.items.length === 0 ? (
                <tr>
                  <td colSpan={9}>
                    <div className="empty-report compact-empty">Không tìm thấy nhân viên phù hợp.</div>
                  </td>
                </tr>
              ) : screen.employees.items.map((employee) => (
                <tr key={employee.employeeId}>
                  <td><strong>{employee.name}</strong></td>
                  <td>{employee.username}</td>
                  <td>{employee.roleName}</td>
                  <td>{employee.branchName}</td>
                  <td>
                    <div className="contact-stack">
                      <span>{employee.phone || "-"}</span>
                      <span className="muted-caption">{employee.email || "-"}</span>
                    </div>
                  </td>
                  <td>{employee.shift || "-"}</td>
                  <td>{employee.salary != null ? `${employee.salary.toLocaleString("vi-VN")} ` : "-"}</td>
                  <td>{employee.isActive ? <span className="status-pill success">Hoạt động</span> : <span className="status-pill danger">Khóa</span>}</td>
                  <td>
                    <div className="button-row wrap">
                      <button className="ghost" onClick={() => navigate(`/Admin/Employees/Edit/${employee.employeeId}`)}>Sửa</button>
                      <button className="ghost" onClick={() => navigate(`/Admin/Employees/History/${employee.employeeId}`)}>Nhật ký</button>
                      {employee.isActive ? (
                        <button className="danger" onClick={() => void handleDeactivate(employee)}>Khóa</button>
                      ) : (
                        <button className="ghost" onClick={() => void handleSetActive(employee, true)}>Bật lại</button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {screen.employees.totalPages > 1 ? (
            <AdminPagination
              currentPage={screen.employees.page}
              totalPages={screen.employees.totalPages}
              onPageChange={(pageNumber) => navigate(buildIndexUrl(pageNumber))}
              keyPrefix="employee"
            />
          ) : null}
        </section>
      ) : null}

      {!loading && (mode === "create" || mode === "edit") && screen ? (
        <section className="panel">
          <article className={`entry-form-card ${mode === "edit" ? "edit-form-card" : ""}`}>
            <div className="entry-form-header">
              <div>
                <strong>{mode === "create" ? "Thêm nhân viên mới" : "Cập nhật nhân viên"}</strong>
                <div className="muted">{mode === "create" ? "Nhập đầy đủ thông tin tài khoản, chi nhánh và vai trò." : "Để trống mật khẩu nếu không thay đổi."}</div>
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
                <input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
              </label>
              <label>Email
                <input value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
              </label>
              <label>Lương
                <input type="number" value={form.salary} onChange={(e) => setForm({ ...form, salary: e.target.value })} />
              </label>
              <label>Ca làm
                <input value={form.shift} onChange={(e) => setForm({ ...form, shift: e.target.value })} />
              </label>
              <label>Chi nhánh
                <select value={form.branchId} onChange={(e) => setForm({ ...form, branchId: e.target.value })} disabled>
                  {screen.branches.map((branch) => (
                    <option key={branch.branchId} value={branch.branchId}>{branch.name}</option>
                  ))}
                </select>
              </label>
              <label>Vai trò
                <select value={form.roleId} onChange={(e) => setForm({ ...form, roleId: e.target.value })}>
                  {screen.roles.map((role) => (
                    <option key={role.roleId} value={role.roleId}>{role.roleName}</option>
                  ))}
                </select>
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
              <button className="ghost" onClick={() => navigate("/Admin/Employees/Index")}>Hủy</button>
              <button onClick={() => void (mode === "create" ? handleCreate() : handleEdit())}>{mode === "create" ? "Lưu nhân viên" : "Lưu thay đổi"}</button>
            </div>
          </article>
        </section>
      ) : null}

      {!loading && mode === "history" ? (
        <section className="panel">
          <article className="panel">
            <div className="panel-head">
              <div>
                <h2>Nhật ký nhân viên</h2>
                <p className="muted">Theo dõi nhật ký hoạt động của nhân viên trong 90 ngày gần nhất.</p>
              </div>
              <button className="ghost" onClick={() => navigate("/Admin/Employees/Index")}>Quay lại</button>
            </div>
            {!history ? (
              <div className="empty-report history-empty-card">
                <strong>Chưa có nhật ký nhân viên.</strong>
                <div>Không thể tải dữ liệu nhật ký cho nhân viên này.</div>
              </div>
            ) : (
              <div className="stack">
                {(() => {
                  const { showChefHistory, showCashierHistory } = getEmployeeHistoryVisibility(history.employee);

                  return (
                    <>
                <div className="inline-filter-card">
                  <div>
                    <strong>{history.employee.employeeName}</strong>
                    <div className="muted">{history.employee.roleName} | {history.employee.branchName}</div>
                  </div>
                </div>

                <div className="history-block">
                  <div className="history-block-title">Nhật ký tài khoản nhân viên</div>
                  {history.staffActivityLogs.logs.length === 0 ? (
                    <div className="empty-report compact-empty">Chưa có nhật ký tài khoản nhân viên.</div>
                  ) : (
                    <>
                      <table className="data-table compact-table">
                        <thead>
                          <tr>
                            <th>Thời gian</th>
                            <th>Hành động</th>
                            <th>IP Address</th>
                            <th>Ghi chú</th>
                          </tr>
                        </thead>
                        <tbody>
                          {history.staffActivityLogs.logs.map((log) => (
                            <tr key={`staff-activity-${log.auditId}`}>
                              <td style={{ whiteSpace: "nowrap" }}>{formatDateTime(log.timestampUtc)}</td>
                              <td><span className={getStaffActionBadge(log.actionType)}>{getStaffActionLabel(log.actionType)}</span></td>
                              <td>
                                <div className="contact-stack">
                                  <span>{log.ipAddress || "-"}</span>
                                  {log.userAgent ? <span className="muted-caption">{log.userAgent}</span> : null}
                                </div>
                              </td>
                              <td>{log.notes || "-"}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                      {history.staffActivityLogs.totalPages > 1 && (
                        <AdminPagination
                          currentPage={history.staffActivityLogs.page}
                          totalPages={history.staffActivityLogs.totalPages}
                          onPageChange={setActivityPage}
                          keyPrefix="staff-activity"
                        />
                      )}
                    </>
                  )}
                </div>

                {showChefHistory ? (
                  <>
                    <div className="history-block">
                      <div className="history-block-title">Nhật ký tạm ngưng/tiếp tục món</div>
                      {history.chefActivityLogs.logs.length === 0 ? (
                        <div className="empty-report compact-empty">Chưa có nhật ký tạm ngưng/tiếp tục món.</div>
                      ) : (
                        <>
                          <table className="data-table compact-table">
                            <thead>
                              <tr>
                                <th>Thời gian</th>
                                <th>Hành động</th>
                                <th>Món ăn</th>
                                <th>Trạng thái sau</th>
                              </tr>
                            </thead>
                            <tbody>
                              {history.chefActivityLogs.logs.map((log) => {
                                const actionLabel = log.actionType === "PAUSE_DISH" ? "Tạm ngưng" : 
                                                   log.actionType === "RESUME_DISH" ? "Tiếp tục" : 
                                                   log.actionType;
                                const actionBadge = log.actionType === "PAUSE_DISH" ? "badge bg-warning text-dark" : 
                                                   log.actionType === "RESUME_DISH" ? "badge bg-info" : 
                                                   "badge bg-secondary";
                                
                                return (
                                  <tr key={`chef-activity-${log.auditId}`}>
                                    <td style={{ whiteSpace: "nowrap" }}>{formatDateTime(log.timestampUtc)}</td>
                                    <td>
                                      <span className={actionBadge}>{actionLabel}</span>
                                    </td>
                                    <td>
                                      <strong>Món #{log.dishId}</strong>
                                    </td>
                                    <td>{log.afterState || "-"}</td>
                                  </tr>
                                );
                              })}
                            </tbody>
                          </table>
                          {history.chefActivityLogs.totalPages > 1 && (
                            <div className="pagination-controls">
                              <button 
                                className="ghost" 
                                disabled={activityPage <= 1}
                                onClick={() => setActivityPage(p => Math.max(1, p - 1))}
                              >
                                ← Trang trước
                              </button>
                              <span>
                                Trang {history.chefActivityLogs.page}/{history.chefActivityLogs.totalPages} 
                                ({history.chefActivityLogs.totalItems} hành động)
                              </span>
                              <button 
                                className="ghost"
                                disabled={activityPage >= history.chefActivityLogs.totalPages}
                                onClick={() => setActivityPage(p => p + 1)}
                              >
                                Trang sau →
                              </button>
                            </div>
                          )}
                        </>
                      )}
                    </div>

                    <div className="history-block">
                      <div className="history-block-title">Nhật ký hoàn thành món ăn</div>
                      {history.chefItemCompletions.logs.length === 0 ? (
                        <div className="empty-report compact-empty">Chưa có nhật ký hoàn thành món ăn.</div>
                      ) : (
                        <>
                          <table className="data-table compact-table">
                            <thead>
                              <tr>
                                <th>Thời gian</th>
                                <th>Món ăn</th>
                                <th>Số lượng</th>
                                <th>Đơn hàng</th>
                                <th>Bàn</th>
                              </tr>
                            </thead>
                            <tbody>
                              {history.chefItemCompletions.logs.map((log) => {
                                let details: any = null;
                                try {
                                  details = log.afterState ? JSON.parse(log.afterState) : null;
                                } catch {}
                                
                                return (
                                  <tr key={`chef-completion-${log.auditId}`}>
                                    <td style={{ whiteSpace: "nowrap" }}>{formatDateTime(log.timestampUtc)}</td>
                                    <td>
                                      <strong>{details?.dishName || `Món #${log.dishId}`}</strong>
                                    </td>
                                    <td style={{ textAlign: "center" }}>
                                      <span className="badge bg-success">{details?.quantity || 1}x</span>
                                    </td>
                                    <td>{details?.orderCode || "-"}</td>
                                    <td>{details?.tableName || "-"}</td>
                                  </tr>
                                );
                              })}
                            </tbody>
                          </table>
                          {history.chefItemCompletions.totalPages > 1 && (
                            <div className="pagination-controls">
                              <button 
                                className="ghost" 
                                disabled={activityPage <= 1}
                                onClick={() => setActivityPage(p => Math.max(1, p - 1))}
                              >
                                ← Trang trước
                              </button>
                              <span>
                                Trang {history.chefItemCompletions.page}/{history.chefItemCompletions.totalPages} 
                                ({history.chefItemCompletions.totalItems} món)
                              </span>
                              <button 
                                className="ghost"
                                disabled={activityPage >= history.chefItemCompletions.totalPages}
                                onClick={() => setActivityPage(p => p + 1)}
                              >
                                Trang sau →
                              </button>
                            </div>
                          )}
                        </>
                      )}
                    </div>

                    <div className="history-block">
                      <div className="history-block-title">Nhật ký nấu ăn (Đơn hàng đã hoàn thành)</div>
                      {history.chefCookingHistory.items.length === 0 ? (
                        <div className="empty-report compact-empty">Chưa có nhật ký nấu ăn.</div>
                      ) : (
                        <>
                          <table className="data-table compact-table">
                            <thead>
                              <tr>
                                <th>Mã đơn</th>
                                <th>Thời gian tạo</th>
                                <th>Hoàn tất</th>
                                <th>Bàn</th>
                                <th>Trạng thái</th>
                                <th>Món</th>
                                <th>Ghi chú</th>
                              </tr>
                            </thead>
                            <tbody>
                              {history.chefCookingHistory.items.map((item) => (
                                <tr key={`chef-cook-${item.orderId}`}>
                                  <td>{item.orderCode || `ORDER-${item.orderId}`}</td>
                                  <td>{formatDateTime(item.orderTime)}</td>
                                  <td>{formatDateTime(item.completedTime)}</td>
                                  <td>{item.tableName || "-"}</td>
                                  <td>
                                    <span className={
                                      item.statusCode === "READY" ? "badge bg-success" :
                                      item.statusCode === "PREPARING" ? "badge bg-warning text-dark" :
                                      item.statusCode === "COMPLETED" ? "badge bg-primary" :
                                      "badge bg-secondary"
                                    }>
                                      {item.statusName}
                                    </span>
                                  </td>
                                  <td>{item.dishesSummary || "-"}</td>
                                  <td>
                                    {item.notes ? (
                                      <span className="text-muted" style={{ fontSize: "0.9em" }}>
                                        {item.notes}
                                      </span>
                                    ) : (
                                      "-"
                                    )}
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                          {history.chefCookingHistory.totalPages > 1 && (
                            <div className="pagination-controls">
                              <button 
                                className="ghost" 
                                disabled={cookingPage <= 1}
                                onClick={() => setCookingPage(p => Math.max(1, p - 1))}
                              >
                                ← Trang trước
                              </button>
                              <span>
                                Trang {history.chefCookingHistory.page}/{history.chefCookingHistory.totalPages} 
                                ({history.chefCookingHistory.totalItems} đơn hàng)
                              </span>
                              <button 
                                className="ghost"
                                disabled={cookingPage >= history.chefCookingHistory.totalPages}
                                onClick={() => setCookingPage(p => p + 1)}
                              >
                                Trang sau →
                              </button>
                            </div>
                          )}
                        </>
                      )}
                    </div>
                  </>
                ) : null}

                {showCashierHistory ? (
                  <div className="history-block">
                    <div className="history-block-title">Nhật ký thu ngân</div>
                    {history.cashierHistory.items.length === 0 ? (
                      <div className="empty-report compact-empty">Chưa có nhật ký thu ngân.</div>
                    ) : (
                      <>
                        <table className="data-table compact-table">
                          <thead>
                            <tr>
                              <th>Mã hóa đơn</th>
                              <th>Thời gian</th>
                              <th>Mã đơn</th>
                              <th>Bàn</th>
                              <th>Khách hàng</th>
                              <th>Tổng tiền</th>
                            </tr>
                          </thead>
                          <tbody>
                            {history.cashierHistory.items.map((item) => (
                              <tr key={`cash-${item.billId}`}>
                                <td>{item.billCode}</td>
                                <td>{formatDateTime(item.billTime)}</td>
                                <td>{item.orderCode || "-"}</td>
                                <td>{item.tableName || "-"}</td>
                                <td>{item.customerName || "-"}</td>
                                <td>{item.totalAmount.toLocaleString("vi-VN")} đ</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                        {history.cashierHistory.totalPages > 1 && (
                          <div className="pagination-controls">
                            <button 
                              className="ghost" 
                              disabled={activityPage <= 1}
                              onClick={() => setActivityPage(p => Math.max(1, p - 1))}
                            >
                              ← Trang trước
                            </button>
                            <span>
                              Trang {history.cashierHistory.page}/{history.cashierHistory.totalPages} 
                              ({history.cashierHistory.totalItems} hóa đơn)
                            </span>
                            <button 
                              className="ghost"
                              disabled={activityPage >= history.cashierHistory.totalPages}
                              onClick={() => setActivityPage(p => p + 1)}
                            >
                              Trang sau →
                            </button>
                          </div>
                        )}
                      </>
                    )}
                  </div>
                ) : null}
                    </>
                  );
                })()}
              </div>
            )}
          </article>
        </section>
      ) : null}
      <Dialog />
    </AdminLayout>
  );
}
