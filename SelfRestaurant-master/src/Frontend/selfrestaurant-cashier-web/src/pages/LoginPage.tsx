import { FormEvent, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { cashierApi } from "../lib/api";

type Props = {
  onLoggedIn: () => Promise<void>;
};

function resolveCashierNextPath(from: string | undefined, nextPath: string | undefined) {
  const normalizedFrom = from?.trim();
  const normalizedNext = nextPath?.trim();
  const isCashierLanding = normalizedNext?.startsWith("/Staff/Cashier/") ?? false;
  const blockedRedirects = new Set(["/login", "/staff/account/login", "/Staff/Account/Login", "/"]);

  if (normalizedFrom && !blockedRedirects.has(normalizedFrom) && isCashierLanding && normalizedFrom.startsWith("/Staff/Cashier/")) {
    return normalizedFrom;
  }

  return nextPath ?? "/Staff/Cashier/Index";
}

function isCashierShellPath(path: string) {
  return path === "/" || path.startsWith("/Staff/Cashier/") || path.startsWith("/Staff/Account/");
}

export function LoginPage({ onLoggedIn }: Props) {
  const navigate = useNavigate();
  const location = useLocation();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe, setRememberMe] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setLoading(true);
    setError(null);
    setSuccess(null);
    try {
      const result = await cashierApi.login(username, password);
      await onLoggedIn();
      setSuccess("Đăng nhập thành công.");
      const nextPath = resolveCashierNextPath((location.state as { from?: string } | null)?.from, result.nextPath);
      window.setTimeout(() => {
        if (isCashierShellPath(nextPath)) {
          navigate(nextPath, { replace: true });
          return;
        }

        window.location.replace(nextPath);
      }, 1000);
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
          <p className="login-title">Đăng nhập tài khoản quản lý</p>
        </div>

        {success ? <div className="success-box">{success}</div> : null}
        {error ? <div className="error-box">{error}</div> : null}

        <form onSubmit={handleSubmit} className="stack">
          <div className="form-floating-field">
            <i className="fas fa-user form-icon" />
            <input
              id="cashier-username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoComplete="username"
              placeholder=" "
            />
            <label htmlFor="cashier-username">Tên đăng nhập</label>
          </div>

          <div className="form-floating-field">
            <i className="fas fa-lock form-icon" />
            <input
              id="cashier-password"
              type={showPassword ? "text" : "password"}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              placeholder=" "
            />
            <label htmlFor="cashier-password">Mật khẩu</label>
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

          <button className="login-submit" type="submit" disabled={loading || Boolean(success)}>
            <i className={`fas ${loading ? "fa-spinner fa-spin" : "fa-sign-in-alt"}`} />{" "}
            {loading ? "Đang đăng nhập..." : "Đăng nhập"}
          </button>
        </form>

        <div className="login-help">
          <strong>Tài khoản test:</strong>
          <br />
          Thu ngân: <code>cashier_lan</code> / <code>123456</code>
          <br />
          Admin: <code>admin</code> / <code>123456</code>
        </div>
      </section>
    </main>
  );
}
