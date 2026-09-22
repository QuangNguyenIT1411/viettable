import { FormEvent, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import { api } from "../lib/api";
import { toMvcPath } from "../lib/mvcPaths";

const text = {
  title: "Self Restaurant",
  subtitle: "T\u1ea1o t\u00e0i kho\u1ea3n m\u1edbi",
  name: "H\u1ecd T\u00ean",
  namePlaceholder: "Nh\u1eadp h\u1ecd t\u00ean",
  phone: "S\u1ed1 \u0110i\u1ec7n Tho\u1ea1i",
  phonePlaceholder: "Nh\u1eadp s\u1ed1 \u0111i\u1ec7n tho\u1ea1i",
  email: "Email",
  emailPlaceholder: "Nh\u1eadp email",
  username: "T\u00ean \u0110\u0103ng Nh\u1eadp",
  usernamePlaceholder: "Nh\u1eadp t\u00ean \u0111\u0103ng nh\u1eadp",
  password: "M\u1eadt Kh\u1ea9u",
  passwordPlaceholder: "Nh\u1eadp m\u1eadt kh\u1ea9u",
  confirmPassword: "X\u00e1c Nh\u1eadn M\u1eadt Kh\u1ea9u",
  confirmPasswordPlaceholder: "X\u00e1c nh\u1eadn m\u1eadt kh\u1ea9u",
  requiredName: "Vui lòng nhập họ tên",
  requiredPhone: "Vui lòng nhập số điện thoại",
  invalidEmail: "Vui lòng nhập email hợp lệ",
  requiredUsername: "Vui lòng nhập tên đăng nhập",
  shortPassword: "Mật khẩu phải có ít nhất 6 ký tự",
  submit: "\u0110\u0103ng K\u00fd",
  submitting: "\u0110ang \u0111\u0103ng k\u00fd...",
  loginPrefix: "\u0110\u00e3 c\u00f3 t\u00e0i kho\u1ea3n?",
  loginLink: "\u0110\u0103ng nh\u1eadp",
  mismatch: "M\u1eadt kh\u1ea9u x\u00e1c nh\u1eadn kh\u00f4ng tr\u00f9ng kh\u1edbp.",
  success: "\u0110\u0103ng k\u00fd th\u00e0nh c\u00f4ng! Vui l\u00f2ng \u0111\u0103ng nh\u1eadp.",
} as const;

export function RegisterPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState({
    name: "",
    phoneNumber: "",
    email: "",
    username: "",
    password: "",
    confirmPassword: "",
  });
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [dismissedError, setDismissedError] = useState(false);

  const register = useMutation({
    mutationFn: api.register,
    onSuccess: (result) => {
      setSuccessMessage(result.message || text.success);
      window.setTimeout(() => {
        navigate(toMvcPath(result.nextPath));
      }, 2000);
    },
  });

  const hasPasswordMismatch = form.password !== "" && form.confirmPassword !== "" && form.password !== form.confirmPassword;

  const validate = () => {
    const nextErrors: Record<string, string> = {};

    if (!form.name.trim()) nextErrors.name = text.requiredName;
    if (!form.phoneNumber.trim()) nextErrors.phoneNumber = text.requiredPhone;
    if (!form.email.trim() || !/.+@.+\..+/.test(form.email.trim())) nextErrors.email = text.invalidEmail;
    if (!form.username.trim()) nextErrors.username = text.requiredUsername;
    if (!form.password.trim() || form.password.trim().length < 6) nextErrors.password = text.shortPassword;
    if (form.password !== form.confirmPassword) nextErrors.confirmPassword = text.mismatch;

    setErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  };

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!validate()) return;

    register.mutate({
      name: form.name.trim(),
      username: form.username.trim(),
      phoneNumber: form.phoneNumber.trim(),
      email: form.email.trim(),
      password: form.password,
    });
  };

  return (
    <div className="auth-wrapper customer-auth-shell">
      <div className="register-container customer-auth-card customer-register-card">
        <div className="register-header">
          <div className="logo">
            <i className="fas fa-utensils" />
          </div>
          <h1>{text.title}</h1>
          <p>{text.subtitle}</p>
        </div>

        <form id="register-form" className="auth-form auth-form-compact" onSubmit={onSubmit}>
          <div className="row">
            <div className="col-md-6">
              <div className="form-group">
                <label htmlFor="register-name">
                  <i className="fas fa-user me-2" />
                  {text.name}
                </label>
                <input
                  id="register-name"
                  className="form-control"
                  value={form.name}
                  onChange={(event) => {
                    setForm({ ...form, name: event.target.value });
                    setErrors((current) => ({ ...current, name: "" }));
                  }}
                  placeholder={text.namePlaceholder}
                  required
                />
                {errors.name ? <div className="invalid-feedback">{errors.name}</div> : null}
              </div>
            </div>

            <div className="col-md-6">
              <div className="form-group">
                <label htmlFor="register-phone">
                  <i className="fas fa-phone me-2" />
                  {text.phone}
                </label>
                <input
                  id="register-phone"
                  className="form-control"
                  value={form.phoneNumber}
                  onChange={(event) => {
                    setForm({ ...form, phoneNumber: event.target.value });
                    setErrors((current) => ({ ...current, phoneNumber: "" }));
                  }}
                  placeholder={text.phonePlaceholder}
                  required
                />
                {errors.phoneNumber ? <div className="invalid-feedback">{errors.phoneNumber}</div> : null}
              </div>
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="register-email">
              <i className="fas fa-envelope me-2" />
              {text.email}
            </label>
            <input
              id="register-email"
              className="form-control"
              type="email"
              value={form.email}
              onChange={(event) => {
                setForm({ ...form, email: event.target.value });
                setErrors((current) => ({ ...current, email: "" }));
              }}
              placeholder={text.emailPlaceholder}
              required
            />
            {errors.email ? <div className="invalid-feedback">{errors.email}</div> : null}
          </div>

          <div className="form-group">
            <label htmlFor="register-username">
              <i className="fas fa-user-tag me-2" />
              {text.username}
            </label>
            <input
              id="register-username"
              className="form-control"
              value={form.username}
              onChange={(event) => {
                setForm({ ...form, username: event.target.value });
                setErrors((current) => ({ ...current, username: "" }));
              }}
              placeholder={text.usernamePlaceholder}
              required
            />
            {errors.username ? <div className="invalid-feedback">{errors.username}</div> : null}
          </div>

          <div className="row">
            <div className="col-md-6">
              <div className="form-group">
                <label htmlFor="register-password">
                  <i className="fas fa-lock me-2" />
                  {text.password}
                </label>
                <input
                  id="register-password"
                  className="form-control"
                  type="password"
                  value={form.password}
                  onChange={(event) => {
                    setForm({ ...form, password: event.target.value });
                    setErrors((current) => ({ ...current, password: "", confirmPassword: "" }));
                  }}
                  placeholder={text.passwordPlaceholder}
                  required
                />
                {errors.password ? <div className="invalid-feedback">{errors.password}</div> : null}
              </div>
            </div>

            <div className="col-md-6">
              <div className="form-group">
                <label htmlFor="register-confirm-password">
                  <i className="fas fa-check-circle me-2" />
                  {text.confirmPassword}
                </label>
                <input
                  id="register-confirm-password"
                  className="form-control"
                  type="password"
                  value={form.confirmPassword}
                  onChange={(event) => {
                    setForm({ ...form, confirmPassword: event.target.value });
                    setErrors((current) => ({ ...current, confirmPassword: "" }));
                  }}
                  placeholder={text.confirmPasswordPlaceholder}
                  required
                />
                {errors.confirmPassword ? <div className="invalid-feedback">{errors.confirmPassword}</div> : null}
              </div>
            </div>
          </div>

          <button
            className="btn btn-danger btn-register"
            type="submit"
            disabled={register.isPending || hasPasswordMismatch || Boolean(successMessage)}
          >
            <i className={`fas ${register.isPending ? "fa-spinner fa-spin" : "fa-user-plus"} me-2`} />
            <span>{register.isPending ? text.submitting : text.submit}</span>
          </button>

          <div id="alert-message" className="auth-inline-slot" />

          <div className="login-link auth-links">
            {text.loginPrefix} <Link to="/Customer/Login">{text.loginLink}</Link>
          </div>

          <div id="alert-message" className="auth-inline-slot">
          {successMessage ? (
              <div className="alert alert-success auth-alert" role="alert">
                <i className="fas fa-check-circle me-2" />
                {successMessage}
              </div>
            ) : null}
            {hasPasswordMismatch && !errors.confirmPassword ? (
              <div className="alert alert-danger auth-alert" role="alert">
                <i className="fas fa-exclamation-circle me-2" />
                {text.mismatch}
              </div>
            ) : null}
            {register.error && !dismissedError ? (
              <div className="alert alert-danger alert-dismissible fade show auth-alert" role="alert">
                <i className="fas fa-exclamation-circle me-2" />
                {(register.error as Error).message}
                <button type="button" className="btn-close" aria-label="Close" onClick={() => setDismissedError(true)} />
              </div>
            ) : null}
          </div>
        </form>
      </div>
    </div>
  );
}
