import { Navigate, Outlet, useLocation } from "react-router-dom";
import type { StaffSessionDto } from "../lib/types";
import { CrossAppRedirect } from "./CrossAppRedirect";

type Props = {
  session: StaffSessionDto | null;
  loading: boolean;
};

export function RequireChef({ session, loading }: Props) {
  const location = useLocation();
  const normalizedRole = session?.staff?.roleCode?.trim().toUpperCase();
  const isChefLane = normalizedRole === "CHEF" || normalizedRole === "KITCHEN_STAFF";
  if (loading) {
    return <div className="screen-message">Đang tải phiên làm việc bếp...</div>;
  }

  if (!session?.authenticated || !session.staff) {
    return <Navigate to="/Staff/Account/Login" replace state={{ from: `${location.pathname}${location.search}` }} />;
  }

  if (!isChefLane) {
    if (normalizedRole === "CASHIER") {
      return <CrossAppRedirect to="/Staff/Cashier/Index" message="Đang chuyển đến khu thu ngân..." />;
    }
    if (normalizedRole === "ADMIN" || normalizedRole === "MANAGER") {
    return <CrossAppRedirect to="/Admin/Dashboard/Index" message="Đang chuyển đến khu quản trị..." />;
    }
    return <Navigate to="/Staff/Account/Login" replace />;
  }

  return <Outlet />;
}
