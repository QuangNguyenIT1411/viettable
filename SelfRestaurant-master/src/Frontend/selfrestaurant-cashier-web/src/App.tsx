import { useEffect, useState } from "react";
import { Navigate, Route, Routes, useNavigate } from "react-router-dom";
import { cashierApi } from "./lib/api";
import type { StaffSessionDto } from "./lib/types";
import { CrossAppRedirect } from "./components/CrossAppRedirect";
import { RequireCashier } from "./components/RequireCashier";
import { DashboardPage } from "./pages/DashboardPage";
import { HistoryPage } from "./pages/HistoryPage";
import { ReportPage } from "./pages/ReportPage";

function resolveRoleLanding(roleCode?: string | null) {
  const normalized = roleCode?.trim().toUpperCase();
  if (normalized === "CHEF" || normalized === "KITCHEN_STAFF") return "/Staff/Chef/Index";
  if (normalized === "ADMIN" || normalized === "MANAGER") return "/Admin/Dashboard/Index";
  return "/Staff/Cashier/Index";
}

function isCashierShellPath(path: string) {
  return path === "/" || path.startsWith("/Staff/Cashier/");
}

export default function App() {
  const navigate = useNavigate();
  const [session, setSession] = useState<StaffSessionDto | null>(null);
  const [loading, setLoading] = useState(true);

  async function refreshSession() {
    setLoading(true);
    try {
      setSession(await cashierApi.getSession());
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refreshSession();
  }, []);

  async function logout() {
    const result = await cashierApi.logout();
    await refreshSession();
    const nextPath = result.nextPath ?? "/Staff/Account/Login";
    if (isCashierShellPath(nextPath)) {
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
            ? (isCashierShellPath(resolveRoleLanding(session.staff.roleCode))
                ? <Navigate to={resolveRoleLanding(session.staff.roleCode)} replace />
                : <CrossAppRedirect to={resolveRoleLanding(session.staff.roleCode)} message="Đang chuyển đến khu vực phù hợp..." />)
            : <CrossAppRedirect to="/Staff/Account/Login" message="Đang chuyển đến trang đăng nhập nhân viên..." />}
      />
      <Route
        path="/Staff/Account/Login"
        element={loading
          ? <div className="screen-message">Đang tải phiên đăng nhập...</div>
          : session?.authenticated && session.staff
            ? (isCashierShellPath(resolveRoleLanding(session.staff.roleCode))
                ? <Navigate to={resolveRoleLanding(session.staff.roleCode)} replace />
                : <CrossAppRedirect to={resolveRoleLanding(session.staff.roleCode)} message="Đang chuyển đến khu vực phù hợp..." />)
            : <CrossAppRedirect to="/Staff/Account/Login" message="Đang chuyển đến trang đăng nhập nhân viên..." />}
      />
          <Route path="/Staff/Chef/*" element={<CrossAppRedirect to="/Staff/Chef/Index" message="Đang chuyển đến khu bếp..." />} />
          <Route path="/Admin/*" element={<CrossAppRedirect to="/Admin/Dashboard/Index" message="Đang chuyển đến khu quản trị..." />} />
      <Route element={<RequireCashier session={session} loading={loading} />}>
        <Route path="/" element={<DashboardPage onLogout={logout} />} />
        <Route path="/Staff/Cashier/Index" element={<DashboardPage onLogout={logout} />} />
        <Route path="/Staff/Cashier/History" element={<HistoryPage />} />
        <Route path="/Staff/Cashier/Report" element={<ReportPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
