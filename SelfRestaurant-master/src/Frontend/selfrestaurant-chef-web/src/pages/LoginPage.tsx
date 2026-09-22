import { FormEvent, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { chefApi } from "../lib/api";

type Props = {
  onLoggedIn: () => Promise<void>;
};

function resolveStaffNextPath(from: string | undefined, nextPath: string | undefined) {
  const normalizedFrom = from?.trim();
  const normalizedNext = nextPath?.trim();
  const isChefLanding = normalizedNext?.startsWith("/Staff/Chef/") ?? false;
  const blockedRedirects = new Set(["/login", "/staff/account/login", "/Staff/Account/Login", "/"]);

  if (normalizedFrom && !blockedRedirects.has(normalizedFrom) && isChefLanding && normalizedFrom.startsWith("/Staff/Chef/")) {
    return normalizedFrom;
  }

  return nextPath ?? "/Staff/Chef/Index";
}

function isChefShellPath(path: string) {
  return path === "/" || path.startsWith("/Staff/Chef/") || path.startsWith("/Staff/Account/");
}

export function LoginPage({ onLoggedIn }: Props) {
  const navigate = useNavigate();
  const location = useLocation();
  const search = new URLSearchParams(location.search);
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe, setRememberMe] = useState(false);
  const [error, setError] = useState<string | null>(search.get("type") === "danger" ? search.get("message") : null);
  const [success, setSuccess] = useState<string | null>(search.get("type") === "success" ? search.get("message") : null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setLoading(true);
    setError(null);
    setSuccess(null);
    try {
      const result = await chefApi.login(username, password);
      await onLoggedIn();
      setSuccess("Đăng nhập thành công!");
      const nextPath = resolveStaffNextPath((location.state as { from?: string } | null)?.from, result.nextPath);
      window.setTimeout(() => {
        // The shared staff login page is hosted in the chef shell, but a valid
        // login may belong to another staff area. Cross-app destinations must
        // use a full-page redirect so React Router does not trap us in /app/chef.
        if (isChefShellPath(nextPath)) {
          navigate(nextPath, { replace: true });
          return;
        }

        window.location.replace(nextPath);
      }, 1000);
    } catch (err) {
      setError(err instanceof Error ? err.message : "\u0110\u0103ng nh\u1eadp th\u1ea5t b\u1ea1i.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="login-shell">
      <section className="login-card">
        <div className="login-header">
          <h1 className="login-brand">Self Restaurant</h1>
          <p className="login-title">Đăng nhập tài khoản nhân viên</p>
        </div>

        {success ? <div className="success-box">{success}</div> : null}
        {error ? <div className="error-box">{error}</div> : null}

        <form onSubmit={handleSubmit} className="stack">
          <div className="form-floating-field">
            <i className="fas fa-user form-icon" />
            <input
              id="chef-username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoComplete="username"
              placeholder=" "
            />
            <label htmlFor="chef-username">{"T\u00ean \u0111\u0103ng nh\u1eadp"}</label>
          </div>

          <div className="form-floating-field">
            <i className="fas fa-lock form-icon" />
            <input
              id="chef-password"
              type={showPassword ? "text" : "password"}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              placeholder=" "
            />
            <label htmlFor="chef-password">{"M\u1eadt kh\u1ea9u"}</label>
          </div>

          <div className="login-meta">
            <label className="remember-row">
              <input type="checkbox" checked={rememberMe} onChange={(e) => setRememberMe(e.target.checked)} />
              {"Ghi nh\u1edb t\u00f4i"}
            </label>
            <div className="login-links-inline">
              <button type="button" className="ghost" onClick={() => setShowPassword((value) => !value)}>
                {showPassword ? "\u1ea8n m\u1eadt kh\u1ea9u" : "Hi\u1ec7n m\u1eadt kh\u1ea9u"}
              </button>
              <a href="/Staff/Account/ForgotPassword" className="forgot-password-link">Quên mật khẩu?</a>
            </div>
          </div>

          <button className="login-submit" type="submit" disabled={loading || Boolean(success)}>
            <i className={`fas ${loading ? "fa-spinner fa-spin" : "fa-sign-in-alt"}`} /> {loading ? "\u0110ang \u0111\u0103ng nh\u1eadp..." : "\u0110\u0103ng nh\u1eadp"}
          </button>
        </form>

        <div className="login-help">
          <strong>Tài khoản mẫu:</strong>
          <br />
          Bếp: <code>chef_hung</code> / <code>123456</code>
          <br />
          Thu ngân: <code>cashier_lan</code> / <code>123456</code>
          <br />
          Quản trị: <code>admin</code> / <code>123456</code>
        </div>
      </section>
    </main>
  );
}
