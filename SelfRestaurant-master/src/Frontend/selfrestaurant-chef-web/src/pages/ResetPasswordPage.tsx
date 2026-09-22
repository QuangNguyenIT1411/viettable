import { FormEvent, useEffect, useMemo, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { chefApi } from "../lib/api";

function normalizeValidationMessage(message: string) {
  const normalized = message.trim();
  if (normalized === "Link đặt lại mật khẩu không hợp lệ.") return { redirect: "/Staff/Account/Login", message: normalized };
  if (normalized === "Link này đã được sử dụng.") return { redirect: "/Staff/Account/Login", message: normalized };
  if (normalized === "Link đã hết hạn. Vui lòng yêu cầu link mới.") return { redirect: "/Staff/Account/ForgotPassword", message: normalized };
  return { redirect: null, message: normalized };
}

export function ResetPasswordPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const search = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const token = search.get("token")?.trim() ?? "";
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [newPasswordError, setNewPasswordError] = useState<string | null>(null);
  const [confirmPasswordError, setConfirmPasswordError] = useState<string | null>(null);
  const [alert, setAlert] = useState<{ type: "success" | "danger"; message: string } | null>(null);
  const [validating, setValidating] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    let active = true;

    if (!token) {
      navigate("/Staff/Account/Login?message=Link%20kh%C3%B4ng%20h%E1%BB%A3p%20l%E1%BB%87.&type=danger", { replace: true });
      return undefined;
    }

    void (async () => {
      try {
        await chefApi.validateResetPasswordToken(token);
        if (active) {
          setValidating(false);
        }
      } catch (err) {
        if (!active) return;
        const message = err instanceof Error ? err.message : "Link không hợp lệ hoặc đã hết hạn.";
        const outcome = normalizeValidationMessage(message);
        const separator = outcome.redirect?.includes("?") ? "&" : "?";
        navigate(`${outcome.redirect ?? "/Staff/Account/Login"}${separator}message=${encodeURIComponent(outcome.message)}&type=danger`, { replace: true });
      }
    })();

    return () => {
      active = false;
    };
  }, [navigate, token]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setNewPasswordError(null);
    setConfirmPasswordError(null);
    setAlert(null);

    if (!newPassword.trim()) {
      setNewPasswordError("Vui lòng nhập mật khẩu mới");
      return;
    }

    if (newPassword.trim().length < 6) {
      setNewPasswordError("Mật khẩu phải có ít nhất 6 ký tự");
      return;
    }

    if (!confirmPassword.trim()) {
      setConfirmPasswordError("Vui lòng xác nhận mật khẩu");
      return;
    }

    if (newPassword !== confirmPassword) {
      setConfirmPasswordError("Mật khẩu xác nhận không khớp");
      return;
    }

    setSubmitting(true);
    try {
      const result = await chefApi.resetPassword({ token, newPassword, confirmPassword });
      setAlert({ type: "success", message: result.message });
      window.setTimeout(() => {
        navigate(result.nextPath ?? "/Staff/Account/Login", { replace: true });
      }, 2000);
    } catch (err) {
      setAlert({ type: "danger", message: err instanceof Error ? err.message : "Có lỗi xảy ra. Vui lòng thử lại sau." });
      setSubmitting(false);
    }
  }

  if (validating) {
    return <div className="screen-message">Đang kiểm tra liên kết đặt lại mật khẩu...</div>;
  }

  return (
    <main className="staff-auth-wrapper">
      <section className="staff-reset-container">
        <div className="staff-auth-header">
          <h1>Đặt Lại Mật Khẩu</h1>
          <p>Nhập mật khẩu mới cho tài khoản nhân viên.</p>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="mb-3">
            <label className="form-label" htmlFor="staff-new-password">Mật khẩu mới</label>
            <input
              id="staff-new-password"
              type="password"
              className="form-control"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              placeholder="Nhập mật khẩu mới"
              autoComplete="new-password"
            />
            {newPasswordError ? <div className="staff-invalid-feedback">{newPasswordError}</div> : null}
          </div>

          <div className="mb-3">
            <label className="form-label" htmlFor="staff-confirm-password">Xác nhận mật khẩu</label>
            <input
              id="staff-confirm-password"
              type="password"
              className="form-control"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              placeholder="Nhập lại mật khẩu mới"
              autoComplete="new-password"
            />
            {confirmPasswordError ? <div className="staff-invalid-feedback">{confirmPasswordError}</div> : null}
          </div>

          <button type="submit" className="btn btn-success w-100 staff-submit-button" disabled={submitting}>
            <i className="fas fa-check-circle me-2" />
            <span>{submitting ? "Đang xử lý..." : "Đặt Lại Mật Khẩu"}</span>
          </button>

          <div className="text-center text-muted small mt-3">
            <Link to="/Staff/Account/Login">
              <i className="fas fa-arrow-left me-1" />
              Quay lại đăng nhập
            </Link>
          </div>
        </form>

        {alert ? (
          <div className={`alert alert-${alert.type} staff-auth-alert`} role="alert">
            {alert.message}
          </div>
        ) : null}
      </section>
    </main>
  );
}
