import type { ReactNode } from "react";
import { useLocation } from "react-router-dom";

export function Layout({ children }: { children: ReactNode }) {
  const location = useLocation();
  const path = location.pathname.toLowerCase();
  const isMenuRoute = path === "/menu" || path === "/menu/index";
  const isOrderRoute =
    path === "/order" ||
    path === "/order/index" ||
    path === "/orders" ||
    path === "/customer/orders";
  const isAuthRoute =
    path === "/login" ||
    path === "/customer/login" ||
    path === "/register" ||
    path === "/customer/register" ||
    path === "/forgot-password" ||
    path === "/customer/forgot-password" ||
    path === "/customer/forgotpassword" ||
    path === "/reset-password" ||
    path === "/customer/reset-password" ||
    path === "/customer/resetpassword";

  return (
    <div className={`shell${isMenuRoute || isOrderRoute || isAuthRoute ? " shell-system-font" : ""}`}>
      <main className="page">{children}</main>
    </div>
  );
}
