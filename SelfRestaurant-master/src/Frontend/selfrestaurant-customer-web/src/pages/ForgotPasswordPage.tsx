import { FormEvent, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { Link, useLocation } from "react-router-dom";
import { api } from "../lib/api";

const text = {
  title: "Qu\u00ean M\u1eadt Kh\u1ea9u?",
  subtitle: "\u0110\u1eebng lo! Nh\u1eadp email c\u1ee7a b\u1ea1n v\u00e0 ch\u00fang t\u00f4i s\u1ebd g\u1eedi link \u0111\u1eb7t l\u1ea1i m\u1eadt kh\u1ea9u.",
  info: "Link \u0111\u1eb7t l\u1ea1i m\u1eadt kh\u1ea9u s\u1ebd c\u00f3 hi\u1ec7u l\u1ef1c trong 30 ph\u00fat.",
  email: "Email",
  emailPlaceholder: "Nh\u1eadp email c\u1ee7a b\u1ea1n",
  submit: "G\u1eedi Link \u0110\u1eb7t L\u1ea1i M\u1eadt Kh\u1ea9u",
  submitting: "\u0110ang g\u1eedi...",
  back: "Quay l\u1ea1i \u0111\u0103ng nh\u1eadp",
} as const;

export function ForgotPasswordPage() {
  const location = useLocation();
  const [email, setEmail] = useState("");
  const [emailError, setEmailError] = useState("");
  const [dismissedFlash, setDismissedFlash] = useState(false);
  const [dismissedError, setDismissedError] = useState(false);
  const forgot = useMutation({ mutationFn: api.forgotPassword });
  const flashMessage = new URLSearchParams(location.search).get("message");
  const flashType = new URLSearchParams(location.search).get("type") ?? "success";

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    setEmailError("");
    const trimmed = email.trim();
    if (!trimmed) {
      setEmailError("Vui lòng nhập email");
      return;
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmed)) {
      setEmailError("Email không hợp lệ");
      return;
    }
    forgot.mutate({ usernameOrEmailOrPhone: trimmed });
  };

  return (
    <div className="auth-wrapper">
      <div className="forgot-container">
        <div className="forgot-header">
          <div className="icon">
            <i className="fas fa-lock" />
          </div>
          <h1>{text.title}</h1>
          <p>{text.subtitle}</p>
        </div>

        <form className="auth-form auth-form-compact" onSubmit={onSubmit}>
          <div className="auth-info-box">
            <p>
              <i className="fas fa-info-circle me-2" />
              {text.info}
            </p>
          </div>

          <div className="form-group">
            <label htmlFor="forgot-email">
              <i className="fas fa-envelope me-2" />
              {text.email}
            </label>
            <input
              id="forgot-email"
              className="form-control"
              type="email"
              value={email}
              onChange={(event) => {
                setEmail(event.target.value);
                setEmailError("");
              }}
              placeholder={text.emailPlaceholder}
              required
            />
            {emailError ? <div className="invalid-feedback">{emailError}</div> : null}
          </div>

          <button className="btn btn-danger btn-submit" type="submit" disabled={forgot.isPending}>
            <i className={`fas ${forgot.isPending ? "fa-spinner fa-spin" : "fa-paper-plane"} me-2`} />
            <span>{forgot.isPending ? text.submitting : text.submit}</span>
          </button>

          <div className="back-to-login auth-links auth-back-link">
            <i className="fas fa-arrow-left me-2" />
            <Link className="secondary" to="/Customer/Login">
              {text.back}
            </Link>
          </div>

          <div id="alert-message" className="auth-inline-slot">
            {flashMessage && !dismissedFlash ? (
              <div className={`alert ${flashType === "error" ? "alert-danger" : "alert-success"} alert-dismissible fade show auth-alert`} role="alert">
                <i className={`fas ${flashType === "error" ? "fa-exclamation-circle" : "fa-check-circle"} me-2`} />
                {flashMessage}
                <button type="button" className="btn-close" aria-label="Close" onClick={() => setDismissedFlash(true)} />
              </div>
            ) : null}
            {forgot.error && !dismissedError ? (
              <div className="alert alert-danger alert-dismissible fade show auth-alert" role="alert">
                <i className="fas fa-exclamation-circle me-2" />
                {(forgot.error as Error).message}
                <button type="button" className="btn-close" aria-label="Close" onClick={() => setDismissedError(true)} />
              </div>
            ) : null}
            {forgot.data ? (
              <div className="alert alert-success auth-alert" role="alert">
                <i className="fas fa-check-circle me-2" />
                {forgot.data.message}
              </div>
            ) : null}
          </div>
        </form>
      </div>
    </div>
  );
}
