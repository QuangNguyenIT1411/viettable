import { Navigate, Outlet, useLocation } from "react-router-dom";
import type { StaffSessionDto } from "../lib/types";
import { CrossAppRedirect } from "./CrossAppRedirect";

type Props = {
  session: StaffSessionDto | null;
  loading: boolean;
};

export function RequireCashier({ session, loading }: Props) {
  const location = useLocation();
  const normalizedRole = session?.staff?.roleCode?.trim().toUpperCase();
  if (loading) return <div className="screen-message">Đang khởi tạo khu thu ngân...</div>;
  if (!session?.authenticated || !session.staff) {
    return <Navigate to="/Staff/Account/Login" replace state={{ from: `${location.pathname}${location.search}` }} />;
  }
  if (normalizedRole !== "CASHIER") {
    if (normalizedRole === "CHEF" || normalizedRole === "KITCHEN_STAFF") {
    return <CrossAppRedirect to="/Staff/Chef/Index" message="Đang chuyển đến khu bếp..." />;
    }
    if (normalizedRole === "ADMIN" || normalizedRole === "MANAGER") {
    return <CrossAppRedirect to="/Admin/Dashboard/Index" message="Đang chuyển đến khu quản trị..." />;
    }
    return <Navigate to="/Staff/Account/Login" replace />;
  }
  return <Outlet />;
}
