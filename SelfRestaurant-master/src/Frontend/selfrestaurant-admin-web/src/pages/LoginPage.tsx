import { FormEvent, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { adminApi } from "../lib/api";

type Props = {
  onLoggedIn: () => Promise<void>;
};

function resolveAdminNextPath(from: string | undefined, nextPath: string | undefined) {
  const normalizedFrom = from?.trim();
  const normalizedNext = nextPath?.trim();
  const isAdminLanding = normalizedNext?.startsWith("/Admin/") ?? false;
  const blockedRedirects = new Set(["/login", "/staff/account/login", "/Staff/Account/Login", "/Admin/Account/Login", "/"]);

  if (normalizedFrom && !blockedRedirects.has(normalizedFrom) && isAdminLanding && normalizedFrom.startsWith("/Admin/")) {
    return normalizedFrom;
  }

  return nextPath ?? "/Admin/Dashboard/Index";
}

function isAdminShellPath(path: string) {
  return path === "/" || path.startsWith("/Admin/");
}

export function LoginPage({ onLoggedIn }: Props) {
  const navigate = useNavigate();
  const location = useLocation();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe, setRememberMe] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setLoading(true);
    setError(null);
    try {
      const result = await adminApi.login(username, password);
      await onLoggedIn();
      const nextPath = resolveAdminNextPath((location.state as { from?: string } | null)?.from, result.nextPath);
      if (isAdminShellPath(nextPath)) {
        navigate(nextPath, { replace: true });
        return;
      }

      window.location.replace(nextPath);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Đăng nhập thất bại.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="login-shell">
      <section className="login-card">
        <div className="login-header">
          <h1 className="login-brand">Self Restaurant</h1>
          <p className="login-title">Đăng nhập tài khoản quản trị</p>
        </div>

        {error ? <div className="error-box">{error}</div> : null}

        <form onSubmit={handleSubmit} className="stack">
          <div className="form-floating-field">
            <i className="fas fa-user form-icon" />
            <input id="admin-username" value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" placeholder=" " />
            <label htmlFor="admin-username">Tên đăng nhập</label>
          </div>

          <div className="form-floating-field">
            <i className="fas fa-lock form-icon" />
            <input id="admin-password" type={showPassword ? "text" : "password"} value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" placeholder=" " />
            <label htmlFor="admin-password">Mật khẩu</label>
          </div>

          <div className="login-meta">
            <label className="remember-row">
              <input type="checkbox" checked={rememberMe} onChange={(e) => setRememberMe(e.target.checked)} />
              Ghi nhớ tôi
            </label>
            <button type="button" className="ghost" onClick={() => setShowPassword((value) => !value)}>
              {showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
            </button>
          </div>

          <button className="login-submit" type="submit" disabled={loading}>
            <i className={`fas ${loading ? "fa-spinner fa-spin" : "fa-sign-in-alt"}`} /> {loading ? "Đang đăng nhập..." : "Đăng nhập"}
          </button>
        </form>

        <div className="login-help">
          <strong>Tài khoản test:</strong><br />
          Admin: <code>admin</code> / <code>123456</code><br />
          Thu ngân: <code>cashier_lan</code> / <code>123456</code><br />
          Đầu bếp: <code>chef_hung</code> / <code>123456</code>
        </div>

        <div className="staff-login-footer">
          <span>Truy cập hệ thống quản trị nội bộ</span>
        </div>
      </section>
    </main>
  );
}
