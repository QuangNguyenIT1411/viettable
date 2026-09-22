import { useEffect, useState } from "react";
import { Navigate, Route, Routes, useNavigate } from "react-router-dom";
import { CrossAppRedirect } from "./components/CrossAppRedirect";
import { RequireAdmin } from "./components/RequireAdmin";
import { adminApi } from "./lib/api";
import type { StaffSessionDto } from "./lib/types";
import { AdminConsolePage } from "./pages/AdminConsolePage";
import { CustomerActivityLogsPage } from "./pages/customers/CustomerActivityLogsPage";
import { CustomerEditPage } from "./pages/customers/CustomerEditPage";
import { CustomersCreatePage } from "./pages/customers/CustomersCreatePage";
import { CustomersIndexPage } from "./pages/customers/CustomersIndexPage";
import { EmployeeEditPage } from "./pages/employees/EmployeeEditPage";
import { EmployeeHistoryPage } from "./pages/employees/EmployeeHistoryPage";
import { EmployeesCreatePage } from "./pages/employees/EmployeesCreatePage";
import { EmployeesIndexPage } from "./pages/employees/EmployeesIndexPage";
import { IngredientsModulePage } from "./pages/ingredients/IngredientsModulePage";
import { InventoryModulePage } from "./pages/inventory/InventoryModulePage";
import { UnitsModulePage } from "./pages/units/UnitsModulePage";
function resolveRoleLanding(roleCode?: string | null) {
  const normalized = roleCode?.trim().toUpperCase();
  if (normalized === "CHEF" || normalized === "KITCHEN_STAFF") return "/Staff/Chef/Index";
  if (normalized === "CASHIER") return "/Staff/Cashier/Index";
  return "/Admin/Dashboard/Index";
}

function isAdminShellPath(path: string) {
  return path === "/" || path.startsWith("/Admin/");
}

const dashboardRoutes = [
  "/",
  "/Admin/Dashboard/Index",
  "/Admin/Categories/Index",
  "/Admin/Categories/Create",
  "/Admin/Categories/Edit/:categoryId",
  "/Admin/Categories/Statuses",
  "/Admin/Dishes/Index",
  "/Admin/Dishes/Create",
  "/Admin/Dishes/Edit",
  "/Admin/Dishes/Delete",
  "/Admin/Dishes/Ingredients",
  "/Admin/TablesQR/Index",
  "/Admin/TablesQR/Edit",
  "/Admin/TablesQR/QR",
  "/Admin/Reports/Revenue",
  "/Admin/Reports/TopDishes",
  "/Admin/Settings/Index",
];

export default function App() {
  const navigate = useNavigate();
  const [session, setSession] = useState<StaffSessionDto | null>(null);
  const [loading, setLoading] = useState(true);

  async function refreshSession() {
    setLoading(true);
    try {
      setSession(await adminApi.getSession());
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refreshSession();
  }, []);

  async function logout() {
    const result = await adminApi.logout();
    await refreshSession();
    const nextPath = result.nextPath ?? "/Staff/Account/Login";
    if (isAdminShellPath(nextPath)) {
      navigate(nextPath, { replace: true });
      return;
    }

    window.location.replace(nextPath);
  }

  return (
    <Routes>
      <Route
        path="/login"
        element={loading
          ? <div className="screen-message">Đang tải phiên đăng nhập...</div>
          : session?.authenticated && session.staff
            ? (isAdminShellPath(resolveRoleLanding(session.staff.roleCode))
                ? <Navigate to={resolveRoleLanding(session.staff.roleCode)} replace />
                : <CrossAppRedirect to={resolveRoleLanding(session.staff.roleCode)} message="Đang chuyển đến khu vực phù hợp..." />)
            : <CrossAppRedirect to="/Staff/Account/Login" message="Đang chuyển đến trang đăng nhập nhân viên..." />}
      />
      <Route
        path="/Admin/Account/Login"
        element={loading
          ? <div className="screen-message">Đang tải phiên đăng nhập...</div>
          : session?.authenticated && session.staff
            ? (isAdminShellPath(resolveRoleLanding(session.staff.roleCode))
                ? <Navigate to={resolveRoleLanding(session.staff.roleCode)} replace />
                : <CrossAppRedirect to={resolveRoleLanding(session.staff.roleCode)} message="Đang chuyển đến khu vực phù hợp..." />)
            : <CrossAppRedirect to="/Staff/Account/Login" message="Đang chuyển đến trang đăng nhập nhân viên..." />}
      />
      <Route
        path="/Staff/Account/Login"
        element={loading
          ? <div className="screen-message">Đang tải phiên đăng nhập...</div>
          : session?.authenticated && session.staff
            ? (isAdminShellPath(resolveRoleLanding(session.staff.roleCode))
                ? <Navigate to={resolveRoleLanding(session.staff.roleCode)} replace />
                : <CrossAppRedirect to={resolveRoleLanding(session.staff.roleCode)} message="Đang chuyển đến khu vực phù hợp..." />)
            : <CrossAppRedirect to="/Staff/Account/Login" message="Đang chuyển đến trang đăng nhập nhân viên..." />}
      />
          <Route path="/Staff/Chef/*" element={<CrossAppRedirect to="/Staff/Chef/Index" message="Đang chuyển đến khu bếp..." />} />
      <Route path="/Staff/Cashier/*" element={<CrossAppRedirect to="/Staff/Cashier/Index" message="Đang chuyển đến khu thu ngân..." />} />
      <Route element={<RequireAdmin session={session} loading={loading} />}>
        {dashboardRoutes.map((path) => (
          <Route key={path} path={path} element={<AdminConsolePage onLogout={logout} />} />
        ))}
        <Route path="/Admin/Categories/Edit" element={<Navigate to="/Admin/Categories/Index" replace />} />
        <Route path="/Admin/Categories/Units" element={<Navigate to="/Admin/Units/Index" replace />} />
        <Route path="/Admin/Units/Index" element={<UnitsModulePage mode="index" onLogout={logout} />} />
        <Route path="/Admin/Units/Create" element={<UnitsModulePage mode="create" onLogout={logout} />} />
        <Route path="/Admin/Units/Edit/:unitId" element={<UnitsModulePage mode="edit" onLogout={logout} />} />
        <Route path="/Admin/Units/Edit" element={<Navigate to="/Admin/Units/Index" replace />} />
        <Route path="/Admin/Ingredients/Index" element={<IngredientsModulePage mode="index" onLogout={logout} />} />
        <Route path="/Admin/Ingredients/Create" element={<IngredientsModulePage mode="create" onLogout={logout} />} />
        <Route path="/Admin/Ingredients/Edit/:ingredientId" element={<IngredientsModulePage mode="edit" onLogout={logout} />} />
        <Route path="/Admin/Ingredients/Edit" element={<Navigate to="/Admin/Ingredients/Index" replace />} />
        <Route path="/Admin/Inventory/Index" element={<InventoryModulePage mode="index" onLogout={logout} />} />
        <Route path="/Admin/Inventory/StockIn" element={<InventoryModulePage mode="stockIn" onLogout={logout} />} />
        <Route path="/Admin/Inventory/StockOut" element={<InventoryModulePage mode="stockOut" onLogout={logout} />} />
        <Route path="/Admin/Inventory/Batches" element={<InventoryModulePage mode="batches" onLogout={logout} />} />
        <Route path="/Admin/Inventory/Movements" element={<InventoryModulePage mode="movements" onLogout={logout} />} />
        <Route path="/Admin/Employees/Index" element={<EmployeesIndexPage onLogout={logout} />} />
        <Route path="/Admin/Employees/Create" element={<EmployeesCreatePage onLogout={logout} />} />
        <Route path="/Admin/Employees/Edit/:employeeId" element={<EmployeeEditPage onLogout={logout} />} />
        <Route path="/Admin/Employees/Edit" element={<Navigate to="/Admin/Employees/Index" replace />} />
        <Route path="/Admin/Employees/History/:employeeId" element={<EmployeeHistoryPage onLogout={logout} />} />
        <Route path="/Admin/Employees/History" element={<Navigate to="/Admin/Employees/Index" replace />} />
        <Route path="/Admin/Customers/Index" element={<CustomersIndexPage onLogout={logout} />} />
        <Route path="/Admin/Customers/Create" element={<CustomersCreatePage onLogout={logout} />} />
        <Route path="/Admin/Customers/Edit/:customerId" element={<CustomerEditPage onLogout={logout} />} />
        <Route path="/Admin/Customers/Edit" element={<Navigate to="/Admin/Customers/Index" replace />} />
        <Route path="/Admin/Customers/:customerId/ActivityLogs" element={<CustomerActivityLogsPage onLogout={logout} />} />
      </Route>
      <Route path="*" element={<Navigate to="/Admin/Dashboard/Index" replace />} />
    </Routes>
  );
}
