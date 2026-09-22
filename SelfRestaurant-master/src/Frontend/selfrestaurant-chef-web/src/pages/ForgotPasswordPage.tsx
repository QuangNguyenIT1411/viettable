import { FormEvent, useState } from "react";
import { Link } from "react-router-dom";
import { chefApi } from "../lib/api";

export function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSuccess(null);

    if (!email.trim()) {
      setError("Vui lòng nhập email");
      return;
    }

    setLoading(true);
    try {
      const result = await chefApi.forgotPassword(email.trim());
      setSuccess(result.message);
      setEmail("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Có lỗi xảy ra. Vui lòng thử lại sau.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="staff-auth-wrapper">
      <section className="staff-forgot-container">
        <div className="staff-auth-header">
          <div className="staff-auth-icon">
            <i className="fas fa-lock" />
          </div>
          <h1>Quên Mật Khẩu</h1>
          <p>Nhập email tài khoản nhân viên, hệ thống sẽ gửi link đặt lại mật khẩu.</p>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="staff-form-group">
            <label htmlFor="staff-email">
              <i className="fas fa-envelope me-2" />
              Email
            </label>
            <input
              id="staff-email"
              type="email"
              className="form-control"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="Nhập email đăng ký"
              autoComplete="email"
            />
            {error ? <div className="staff-invalid-feedback">{error}</div> : null}
          </div>

          <button type="submit" className="btn btn-danger staff-submit-button" disabled={loading}>
            <i className="fas fa-paper-plane me-2" />
            <span>{loading ? "Đang gửi..." : "Gửi Link Đặt Lại Mật Khẩu"}</span>
          </button>

          <div className="staff-back-link">
            <i className="fas fa-arrow-left me-2" />
            <Link to="/Staff/Account/Login">Quay lại đăng nhập</Link>
          </div>
        </form>

        {success ? (
          <div className="alert alert-success staff-auth-alert" role="alert">
            <i className="fas fa-info-circle me-2" />
            {success}
          </div>
        ) : null}
      </section>
    </main>
  );
}
