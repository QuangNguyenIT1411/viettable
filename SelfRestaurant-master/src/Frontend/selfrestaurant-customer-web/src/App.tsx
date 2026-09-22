import { Routes, Route, Link, Navigate } from "react-router-dom";
import { Layout } from "./components/Layout";
import { RequireAuth } from "./components/RequireAuth";
import { HomePage } from "./pages/HomePage";
import { AboutPage } from "./pages/AboutPage";
import { ContactPage } from "./pages/ContactPage";
import { LoginPage } from "./pages/LoginPage";
import { RegisterPage } from "./pages/RegisterPage";
import { ForgotPasswordPage } from "./pages/ForgotPasswordPage";
import { ResetPasswordPage } from "./pages/ResetPasswordPage";
import { MenuFromQrPage } from "./pages/MenuFromQrPage";
import { MenuIndexPage } from "./pages/MenuIndexPage";
import { OrderIndexPage } from "./pages/OrderIndexPage";
import { DashboardPage } from "./pages/DashboardPage";
import { OrdersPage } from "./pages/OrdersPage";
import { ReservationPage } from "./pages/ReservationPage";
import { ReservationHistoryPage } from "./pages/ReservationHistoryPage";
import { ReservationPreOrderPage } from "./pages/ReservationPreOrderPage";
import { ErrorBoundary } from "./components/ErrorBoundary";

const reservationLoginMessage = "Vui lòng đăng nhập để đặt bàn trước.";

export default function App() {
  return (
    <Layout>
      <ErrorBoundary>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/home" element={<HomePage />} />
          <Route path="/home/index" element={<HomePage />} />
          <Route path="/Home" element={<HomePage />} />
          <Route path="/Home/Index" element={<HomePage />} />
          <Route path="/about" element={<AboutPage />} />
          <Route path="/home/about" element={<AboutPage />} />
          <Route path="/Home/About" element={<AboutPage />} />
          <Route path="/contact" element={<ContactPage />} />
          <Route path="/home/contact" element={<ContactPage />} />
          <Route path="/Home/Contact" element={<ContactPage />} />
          <Route path="/reservation" element={<RequireAuth loginMessage={reservationLoginMessage}><ReservationPage /></RequireAuth>} />
          <Route path="/Reservation" element={<RequireAuth loginMessage={reservationLoginMessage}><ReservationPage /></RequireAuth>} />
          <Route path="/Reservation/Index" element={<RequireAuth loginMessage={reservationLoginMessage}><ReservationPage /></RequireAuth>} />
          <Route path="/reservation/my" element={<RequireAuth loginMessage={reservationLoginMessage}><ReservationHistoryPage /></RequireAuth>} />
          <Route path="/Reservation/My" element={<RequireAuth loginMessage={reservationLoginMessage}><ReservationHistoryPage /></RequireAuth>} />
          <Route path="/Reservation/History" element={<RequireAuth loginMessage={reservationLoginMessage}><ReservationHistoryPage /></RequireAuth>} />
          <Route path="/reservation/:code/preorder" element={<RequireAuth loginMessage={reservationLoginMessage}><ReservationPreOrderPage /></RequireAuth>} />
          <Route path="/Reservation/:code/PreOrder" element={<RequireAuth loginMessage={reservationLoginMessage}><ReservationPreOrderPage /></RequireAuth>} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/customer/login" element={<LoginPage />} />
          <Route path="/Customer/Login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/customer/register" element={<RegisterPage />} />
          <Route path="/Customer/Register" element={<RegisterPage />} />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/customer/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/customer/forgotpassword" element={<ForgotPasswordPage />} />
          <Route path="/Customer/ForgotPassword" element={<ForgotPasswordPage />} />
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route path="/customer/reset-password" element={<ResetPasswordPage />} />
          <Route path="/customer/resetpassword" element={<ResetPasswordPage />} />
          <Route path="/Customer/ResetPassword" element={<ResetPasswordPage />} />
          <Route path="/menu" element={<MenuIndexPage />} />
          <Route path="/menu/index" element={<MenuIndexPage />} />
          <Route path="/menu/fromqr" element={<MenuFromQrPage />} />
          <Route path="/Menu" element={<MenuIndexPage />} />
          <Route path="/Menu/FromQr" element={<MenuFromQrPage />} />
          <Route path="/Menu/Index" element={<MenuIndexPage />} />
          <Route path="/order" element={<OrderIndexPage />} />
          <Route path="/order/index" element={<OrderIndexPage />} />
          <Route path="/Order" element={<OrderIndexPage />} />
          <Route path="/Order/Index" element={<OrderIndexPage />} />
          <Route path="/dashboard" element={<RequireAuth><DashboardPage initialTab="profile" /></RequireAuth>} />
          <Route path="/customer/dashboard" element={<RequireAuth><DashboardPage initialTab="profile" /></RequireAuth>} />
          <Route path="/Customer/Dashboard" element={<RequireAuth><DashboardPage initialTab="profile" /></RequireAuth>} />
          <Route path="/orders" element={<RequireAuth><OrdersPage /></RequireAuth>} />
          <Route path="/customer/orders" element={<RequireAuth><OrdersPage /></RequireAuth>} />
          <Route path="/Customer/Orders" element={<RequireAuth><OrdersPage /></RequireAuth>} />
          <Route path="/profile" element={<Navigate to="/customer/dashboard" replace />} />
          <Route path="/customer/profile" element={<Navigate to="/customer/dashboard" replace />} />
          <Route path="/Customer/Profile" element={<Navigate to="/Customer/Dashboard" replace />} />
          <Route
            path="*"
            element={
              <div className="card">
                <p>{"Kh\u00f4ng t\u00ecm th\u1ea5y trang."}</p>
                <Link to="/Home/Index">{ "Quay v\u1ec1 trang ch\u1ee7" }</Link>
              </div>
            }
          />
        </Routes>
      </ErrorBoundary>
    </Layout>
  );
}
