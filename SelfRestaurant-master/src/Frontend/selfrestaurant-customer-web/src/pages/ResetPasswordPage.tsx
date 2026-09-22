import { FormEvent, useEffect, useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { api } from "../lib/api";
import { toMvcPath } from "../lib/mvcPaths";

const text = {
  title: "\u0110\u1eb7t L\u1ea1i M\u1eadt Kh\u1ea9u",
  subtitle: "Vui l\u00f2ng nh\u1eadp m\u1eadt kh\u1ea9u m\u1edbi c\u1ee7a b\u1ea1n",
  requirementsTitle: "Y\u00eau c\u1ea7u m\u1eadt kh\u1ea9u:",
  requirementOne: "\u00cdt nh\u1ea5t 6 k\u00fd t\u1ef1",
  requirementTwo: "N\u00ean s\u1eed d\u1ee5ng k\u1ebft h\u1ee3p ch\u1eef hoa, ch\u1eef th\u01b0\u1eddng v\u00e0 s\u1ed1",
  requirementThree: "Tr\u00e1nh s\u1eed d\u1ee5ng th\u00f4ng tin c\u00e1 nh\u00e2n d\u1ec5 \u0111o\u00e1n",
  newPassword: "M\u1eadt Kh\u1ea9u M\u1edbi",
  newPasswordPlaceholder: "Nh\u1eadp m\u1eadt kh\u1ea9u m\u1edbi",
  confirmPassword: "X\u00e1c Nh\u1eadn M\u1eadt Kh\u1ea9u",
  confirmPasswordPlaceholder: "Nh\u1eadp l\u1ea1i m\u1eadt kh\u1ea9u m\u1edbi",
  invalidToken: "Kh\u00f4ng t\u00ecm th\u1ea5y reset token h\u1ee3p l\u1ec7.",
  mismatch: "M\u1eadt kh\u1ea9u x\u00e1c nh\u1eadn kh\u00f4ng tr\u00f9ng kh\u1edbp.",
  submit: "\u0110\u1eb7t L\u1ea1i M\u1eadt Kh\u1ea9u",
  submitting: "\u0110ang c\u1eadp nh\u1eadt...",
  back: "Quay l\u1ea1i \u0111\u0103ng nh\u1eadp",
  togglePassword: "\u1ea8n/hi\u1ec7n m\u1eadt kh\u1ea9u",
  success: "\u0110\u1eb7t l\u1ea1i m\u1eadt kh\u1ea9u th\u00e0nh c\u00f4ng.",
} as const;

export function ResetPasswordPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const token = useMemo(() => searchParams.get("token") ?? "", [searchParams]);
  const tokenValidation = useQuery({
    queryKey: ["validateResetToken", token],
    queryFn: () => api.validateResetPasswordToken(token),
    enabled: Boolean(token),
    retry: false,
  });
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [newPasswordError, setNewPasswordError] = useState("");
  const [confirmPasswordError, setConfirmPasswordError] = useState("");
  const [dismissedError, setDismissedError] = useState(false);

  const reset = useMutation({
    mutationFn: api.resetPassword,
    onSuccess: (result) => {
      setSuccessMessage(result.message || text.success);
      window.setTimeout(() => {
        navigate(toMvcPath(result.nextPath));
      }, 2000);
    },
  });

  const hasMismatch = confirmPassword !== "" && newPassword !== confirmPassword;

  useEffect(() => {
    if (!token) {
      navigate(`/Customer/Login?message=${encodeURIComponent("Link không hợp lệ.")}&type=error`, { replace: true });
      return;
    }

    if (!tokenValidation.isError) {
      return;
    }

    const message = (tokenValidation.error as Error).message;
    if (message === "Link đã hết hạn. Vui lòng yêu cầu link mới.") {
      navigate(`/Customer/ForgotPassword?message=${encodeURIComponent(message)}&type=error`, { replace: true });
      return;
    }

    navigate(`/Customer/Login?message=${encodeURIComponent(message)}&type=error`, { replace: true });
  }, [navigate, token, tokenValidation.error, tokenValidation.isError]);

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    setNewPasswordError("");
    setConfirmPasswordError("");
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
    if (hasMismatch) {
      setConfirmPasswordError("Mật khẩu xác nhận không khớp");
      return;
    }
    if (!token || tokenValidation.isLoading || tokenValidation.isError) return;
    reset.mutate({ token, newPassword, confirmPassword });
  };

  if (tokenValidation.isLoading && token) {
    return (
      <div className="auth-wrapper">
        <div className="reset-container">
          <div className="alert alert-info mb-0" role="alert">
            <i className="fas fa-info-circle me-2" />
            Đang kiểm tra liên kết đặt lại mật khẩu...
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="auth-wrapper">
      <div className="reset-container">
        <div className="reset-header">
          <div className="icon">
            <i className="fas fa-key" />
          </div>
          <h1>{text.title}</h1>
          <p>{text.subtitle}</p>
        </div>

        <form className="auth-form auth-form-compact" onSubmit={onSubmit}>
          <div className="password-help">
            <h6>
              <i className="fas fa-shield-alt me-2" />
              {text.requirementsTitle}
            </h6>
            <ul>
              <li>{text.requirementOne}</li>
              <li>{text.requirementTwo}</li>
              <li>{text.requirementThree}</li>
            </ul>
          </div>

          <div className="form-group">
            <label htmlFor="reset-password">
              <i className="fas fa-lock me-2" />
              {text.newPassword}
            </label>
            <div className="password-toggle auth-password-toggle">
              <input
                id="reset-password"
                className="form-control"
                type={showNewPassword ? "text" : "password"}
                value={newPassword}
                onChange={(event) => {
                  setNewPassword(event.target.value);
                  setNewPasswordError("");
                  if (confirmPasswordError === "Mật khẩu xác nhận không khớp") {
                    setConfirmPasswordError("");
                  }
                }}
                placeholder={text.newPasswordPlaceholder}
                required
              />
              <button
                type="button"
                className="auth-password-toggle-btn"
                onClick={() => setShowNewPassword((value) => !value)}
                aria-label={text.togglePassword}
              >
                <i className={`fas ${showNewPassword ? "fa-eye-slash" : "fa-eye"}`} />
              </button>
            </div>
            {newPasswordError ? <div className="invalid-feedback">{newPasswordError}</div> : null}
          </div>

          <div className="form-group">
            <label htmlFor="reset-confirm-password">
              <i className="fas fa-lock me-2" />
              {text.confirmPassword}
            </label>
            <div className="password-toggle auth-password-toggle">
              <input
                id="reset-confirm-password"
                className="form-control"
                type={showConfirmPassword ? "text" : "password"}
                value={confirmPassword}
                onChange={(event) => {
                  setConfirmPassword(event.target.value);
                  setConfirmPasswordError("");
                }}
                placeholder={text.confirmPasswordPlaceholder}
                required
              />
              <button
                type="button"
                className="auth-password-toggle-btn"
                onClick={() => setShowConfirmPassword((value) => !value)}
                aria-label={text.togglePassword}
              >
                <i className={`fas ${showConfirmPassword ? "fa-eye-slash" : "fa-eye"}`} />
              </button>
            </div>
            {confirmPasswordError ? <div className="invalid-feedback">{confirmPasswordError}</div> : null}
          </div>

          <button
            className="btn btn-success btn-submit"
            type="submit"
            disabled={reset.isPending || !token || hasMismatch || Boolean(successMessage)}
          >
            <i className={`fas ${reset.isPending ? "fa-spinner fa-spin" : "fa-check-circle"} me-2`} />
            <span>{reset.isPending ? text.submitting : text.submit}</span>
          </button>

          <div className="back-to-login auth-links auth-back-link">
            <i className="fas fa-arrow-left me-2" />
            <Link className="secondary" to="/Customer/Login">
              {text.back}
            </Link>
          </div>

          <div id="alert-message" className="auth-inline-slot">
            {successMessage ? (
              <div className="alert alert-success auth-alert" role="alert">
                <i className="fas fa-check-circle me-2" />
                {successMessage}
              </div>
            ) : null}
            {reset.error && !dismissedError ? (
              <div className="alert alert-danger alert-dismissible fade show auth-alert" role="alert">
                <i className="fas fa-exclamation-circle me-2" />
                {(reset.error as Error).message}
                <button type="button" className="btn-close" aria-label="Close" onClick={() => setDismissedError(true)} />
              </div>
            ) : null}
            {!token ? (
              <div className="alert alert-danger auth-alert" role="alert">
                <i className="fas fa-exclamation-circle me-2" />
                {text.invalidToken}
              </div>
            ) : null}
          </div>
        </form>
      </div>
    </div>
  );
}
