const pathMap: Record<string, string> = {
  "/": "/Home/Index",
  "/app/customer": "/Home/Index",
  "/home": "/Home/Index",
  "/home/index": "/Home/Index",
  "/about": "/Home/About",
  "/home/about": "/Home/About",
  "/contact": "/Home/Contact",
  "/home/contact": "/Home/Contact",
  "/login": "/Customer/Login",
  "/app/customer/login": "/Customer/Login",
  "/customer/login": "/Customer/Login",
  "/register": "/Customer/Register",
  "/app/customer/register": "/Customer/Register",
  "/customer/register": "/Customer/Register",
  "/forgot-password": "/Customer/ForgotPassword",
  "/customer/forgot-password": "/Customer/ForgotPassword",
  "/customer/forgotpassword": "/Customer/ForgotPassword",
  "/reset-password": "/Customer/ResetPassword",
  "/app/customer/reset-password": "/Customer/ResetPassword",
  "/customer/reset-password": "/Customer/ResetPassword",
  "/customer/resetpassword": "/Customer/ResetPassword",
  "/menu": "/Menu/Index",
  "/app/customer/menu": "/Menu/Index",
  "/menu/index": "/Menu/Index",
  "/menu/fromqr": "/Menu/FromQr",
  "/order": "/Order/Index",
  "/app/customer/order": "/Order/Index",
  "/order/index": "/Order/Index",
  "/dashboard": "/Customer/Dashboard",
  "/app/customer/dashboard": "/Customer/Dashboard",
  "/customer/dashboard": "/Customer/Dashboard",
  "/orders": "/Customer/Orders",
  "/app/customer/orders": "/Customer/Orders",
  "/customer/orders": "/Customer/Orders",
  "/profile": "/Customer/Profile",
  "/app/customer/profile": "/Customer/Profile",
  "/customer/profile": "/Customer/Profile",
};

export function toMvcPath(path?: string | null) {
  if (!path) return "/Home/Index";

  const [pathname, search = ""] = path.split("?");
  const normalized = pathname.trim();
  const mapped = pathMap[normalized] ?? pathMap[normalized.toLowerCase()] ?? normalized;

  return search ? `${mapped}?${search}` : mapped;
}
