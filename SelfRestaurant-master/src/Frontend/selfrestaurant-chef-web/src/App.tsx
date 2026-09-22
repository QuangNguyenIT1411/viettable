import { useEffect, useState } from "react";
import { Navigate, Route, Routes, useNavigate } from "react-router-dom";
import { chefApi } from "./lib/api";
import type { StaffSessionDto } from "./lib/types";
import { CrossAppRedirect } from "./components/CrossAppRedirect";
import { RequireChef } from "./components/RequireChef";
import { DashboardPage } from "./pages/DashboardPage";
import { ForgotPasswordPage } from "./pages/ForgotPasswordPage";
import { LoginPage } from "./pages/LoginPage";
import { ResetPasswordPage } from "./pages/ResetPasswordPage";

function resolveRoleLanding(roleCode?: string | null) {
  const normalized = roleCode?.trim().toUpperCase();
  if (normalized === "CASHIER") return "/Staff/Cashier/Index";
  if (normalized === "ADMIN" || normalized === "MANAGER") return "/Admin/Dashboard/Index";
  return "/Staff/Chef/Index";
}

function isChefShellPath(path: string) {
  return path === "/" || path.startsWith("/Staff/Chef/") || path.startsWith("/Staff/Account/");
}

export default function App() {
  const navigate = useNavigate();
  const [session, setSession] = useState<StaffSessionDto | null>(null);
  const [loading, setLoading] = useState(true);

  async function refreshSession() {
    setLoading(true);
    try {
      setSession(await chefApi.getSession());
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refreshSession();
  }, []);

  async function logout() {
    const result = await chefApi.logout();
    await refreshSession();
    navigate(result.nextPath ?? "/Staff/Account/Login", { replace: true });
  }

  return (
    <Routes>
      <Route path="/Staff/Account/ForgotPassword" element={<ForgotPasswordPage />} />
      <Route path="/Staff/Account/ResetPassword" element={<ResetPasswordPage />} />
      <Route
        path="/login"
        element={loading
          ? <div className="screen-message">Đang tải phiên đăng nhập...</div>
          : session?.authenticated && session.staff
            ? (isChefShellPath(resolveRoleLanding(session.staff.roleCode))
                ? <Navigate to={resolveRoleLanding(session.staff.roleCode)} replace />
                : <CrossAppRedirect to={resolveRoleLanding(session.staff.roleCode)} message="Đang chuyển đến khu vực phù hợp..." />)
            : <LoginPage onLoggedIn={refreshSession} />}
      />
      <Route
        path="/Staff/Account/Login"
        element={loading
          ? <div className="screen-message">Đang tải phiên đăng nhập...</div>
          : session?.authenticated && session.staff
            ? (isChefShellPath(resolveRoleLanding(session.staff.roleCode))
                ? <Navigate to={resolveRoleLanding(session.staff.roleCode)} replace />
                : <CrossAppRedirect to={resolveRoleLanding(session.staff.roleCode)} message="Đang chuyển đến khu vực phù hợp..." />)
            : <LoginPage onLoggedIn={refreshSession} />}
      />
      <Route path="/Staff/Cashier/*" element={<CrossAppRedirect to="/Staff/Cashier/Index" message="Đang chuyển đến khu thu ngân..." />} />
          <Route path="/Admin/*" element={<CrossAppRedirect to="/Admin/Dashboard/Index" message="Đang chuyển đến khu quản trị..." />} />
      <Route element={<RequireChef session={session} loading={loading} />}>
        <Route path="/" element={<DashboardPage onLogout={logout} />} />
        <Route path="/Staff/Chef/Index" element={<DashboardPage onLogout={logout} />} />
        <Route path="/Staff/Chef/History" element={<DashboardPage onLogout={logout} />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
