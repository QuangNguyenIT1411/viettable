import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { cashierApi } from "../lib/api";
import type { CashierAccountDto, CashierHistoryDto } from "../lib/types";

function formatDateTime(value?: string | null) {
  if (!value) return "Ch\u01b0a c\u1eadp nh\u1eadt";
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleString("vi-VN", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
      });
}

export function HistoryPage() {
  const [history, setHistory] = useState<CashierHistoryDto | null>(null);
  const [accountDraft, setAccountDraft] = useState({ name: "", phone: "", email: "" });
  const [passwordDraft, setPasswordDraft] = useState({ currentPassword: "", newPassword: "", confirmPassword: "" });
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const nextHistory = await cashierApi.getHistory();
      setHistory(nextHistory);
      setAccountDraft({
        name: nextHistory.account.name,
        phone: nextHistory.account.phone,
        email: nextHistory.account.email,
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Kh\u00f4ng th\u1ec3 t\u1ea3i l\u1ecbch s\u1eed thu ng\u00e2n.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function saveAccount() {
    setMessage(null);
    setError(null);
    if (!accountDraft.name.trim() || !accountDraft.email.trim() || !accountDraft.phone.trim()) {
      setError("Vui lòng nhập đầy đủ họ tên, email và số điện thoại.");
      return;
    }
    try {
      const updated = await cashierApi.updateAccount(accountDraft);
      setHistory((current) => (current ? { ...current, account: updated as CashierAccountDto } : current));
      setMessage("C\u1eadp nh\u1eadt t\u00e0i kho\u1ea3n th\u00e0nh c\u00f4ng.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Kh\u00f4ng th\u1ec3 c\u1eadp nh\u1eadt t\u00e0i kho\u1ea3n.");
    }
  }

  async function changePassword() {
    setMessage(null);
    setError(null);
    if (!passwordDraft.currentPassword || !passwordDraft.newPassword || !passwordDraft.confirmPassword) {
      setError("Vui lòng nhập đầy đủ thông tin đổi mật khẩu.");
      return;
    }
    if (passwordDraft.newPassword !== passwordDraft.confirmPassword) {
      setError("Xác nhận mật khẩu mới không khớp.");
      return;
    }
    try {
      const result = await cashierApi.changePassword(passwordDraft);
      setMessage(result.message || "\u0110\u1ed5i m\u1eadt kh\u1ea9u th\u00e0nh c\u00f4ng.");
      setPasswordDraft({ currentPassword: "", newPassword: "", confirmPassword: "" });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Kh\u00f4ng th\u1ec3 \u0111\u1ed5i m\u1eadt kh\u1ea9u.");
    }
  }

  if (loading) return <div className="screen-message">{"\u0110ang t\u1ea3i l\u1ecbch s\u1eed thu ng\u00e2n..."}</div>;
  if (error && !history) return <div className="screen-message error-box">{error}</div>;
  if (!history) return null;

  return (
    <main className="cashier-shell cashier-subpage">
      <section className="cashier-route-header">
        <div className="cashier-route-title">
          <i className="bi bi-person-badge" />
          <div>
            <h1>{"T\u00e0i kho\u1ea3n & L\u1ecbch s\u1eed Thu ng\u00e2n"}</h1>
            <p className="muted">
              {history.account.branchName} - {history.account.name} ({history.account.roleName})
            </p>
          </div>
        </div>
        <div className="header-actions">
          <Link className="cashier-link-button" to="/Staff/Cashier/Index">
            <i className="bi bi-arrow-left me-1" />
            {"Quay v\u1ec1 m\u00e0n h\u00ecnh thanh to\u00e1n"}
          </Link>
        </div>
      </section>

      {message ? <div className="success-box">{message}</div> : null}
      {error ? <div className="error-box">{error}</div> : null}

      <section className="cashier-history-grid">
        <div className="panel cashier-panel-card">
          <div className="cashier-panel-header">
            <h2>
              <i className="bi bi-person me-2" />
              {"Th\u00f4ng tin t\u00e0i kho\u1ea3n"}
            </h2>
            <span className="soft-badge success">{history.account.username}</span>
          </div>

          <div className="cashier-panel-body">
            <form className="cashier-account-form" onSubmit={(event) => event.preventDefault()}>
              <label>
                {"H\u1ecd t\u00ean"}
                <input
                  value={accountDraft.name}
                  onChange={(e) => setAccountDraft((current) => ({ ...current, name: e.target.value }))}
                />
              </label>
              <label>
                {"T\u00ean \u0111\u0103ng nh\u1eadp"}
                <input value={history.account.username} disabled />
              </label>
              <label>
                Email
                <input
                  type="email"
                  value={accountDraft.email}
                  onChange={(e) => setAccountDraft((current) => ({ ...current, email: e.target.value }))}
                />
              </label>
              <label>
                {"S\u1ed1 \u0111i\u1ec7n tho\u1ea1i"}
                <input
                  value={accountDraft.phone}
                  onChange={(e) => setAccountDraft((current) => ({ ...current, phone: e.target.value }))}
                />
              </label>
              <div className="muted">
                {"Chi nh\u00e1nh:"} <strong>{history.account.branchName}</strong> {"| Vai tr\u00f2:"} <strong>{history.account.roleName}</strong>
              </div>
              <button className="cashier-button-primary" onClick={() => void saveAccount()}>
                <i className="bi bi-save me-2" />
                {"L\u01b0u thay \u0111\u1ed5i"}
              </button>
            </form>

            <hr />

            <div className="stack">
              <h3 className="subsection-title">
                <i className="bi bi-key me-2" />
                {"\u0110\u1ed5i m\u1eadt kh\u1ea9u"}
              </h3>
              <label>
                {"M\u1eadt kh\u1ea9u hi\u1ec7n t\u1ea1i"}
                <input
                  type="password"
                  value={passwordDraft.currentPassword}
                  onChange={(e) => setPasswordDraft((current) => ({ ...current, currentPassword: e.target.value }))}
                />
              </label>
              <label>
                {"M\u1eadt kh\u1ea9u m\u1edbi"}
                <input
                  type="password"
                  value={passwordDraft.newPassword}
                  onChange={(e) => setPasswordDraft((current) => ({ ...current, newPassword: e.target.value }))}
                />
              </label>
              <label>
                {"X\u00e1c nh\u1eadn m\u1eadt kh\u1ea9u m\u1edbi"}
                <input
                  type="password"
                  value={passwordDraft.confirmPassword}
                  onChange={(e) => setPasswordDraft((current) => ({ ...current, confirmPassword: e.target.value }))}
                />
              </label>
              <button className="cashier-button-outline" onClick={() => void changePassword()}>
                <i className="bi bi-lock-fill me-2" />
                {"\u0110\u1ed5i m\u1eadt kh\u1ea9u"}
              </button>
            </div>

          </div>
        </div>

        <div className="panel cashier-panel-card">
          <div className="cashier-panel-header">
            <h2>
              <i className="bi bi-clock-history me-2" />
              {"L\u1ecbch s\u1eed h\u00f3a \u0111\u01a1n \u0111\u00e3 thanh to\u00e1n"}
            </h2>
            <span className="soft-badge success">{Math.min(history.bills.length, 50)} hóa đơn</span>
          </div>

          <div className="cashier-panel-body cashier-panel-body-scroll">
            {history.bills.length > 0 ? (
              <>
                <div className="table-scroll">
                  <table className="staff-table">
                    <thead>
                      <tr>
                        <th>{"Th\u1eddi gian"}</th>
                        <th>{"M\u00e3 h\u00f3a \u0111\u01a1n"}</th>
                        <th>{"\u0110\u01a1n h\u00e0ng"}</th>
                        <th>{"B\u00e0n"}</th>
                        <th className="text-end">{"Th\u00e0nh ti\u1ec1n"}</th>
                        <th>PTTT</th>
                      </tr>
                    </thead>
                    <tbody>
                      {history.bills.slice(0, 50).map((bill) => (
                        <tr key={bill.billId}>
                          <td>{formatDateTime(bill.billTime)}</td>
                          <td>{bill.billCode}</td>
                          <td>{bill.orderCode}</td>
                          <td>{bill.tableName}</td>
                          <td className="text-end">{bill.totalAmount.toLocaleString("vi-VN")} {"\u0111"}</td>
                          <td>
                            {bill.paymentMethod === "CARD"
                              ? "Th\u1ebb"
                              : bill.paymentMethod === "CASH"
                                ? "Ti\u1ec1n m\u1eb7t"
                                : bill.paymentMethod}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <small className="muted history-footnote">
                  {"Hi\u1ec3n th\u1ecb t\u1ed1i \u0111a 50 h\u00f3a \u0111\u01a1n g\u1ea7n nh\u1ea5t do b\u1ea1n thanh to\u00e1n."}
                </small>
              </>
            ) : (
              <div className="table-empty muted">
                {"Ch\u01b0a c\u00f3 h\u00f3a \u0111\u01a1n n\u00e0o \u0111\u01b0\u1ee3c ghi nh\u1eadn cho t\u00e0i kho\u1ea3n n\u00e0y."}
              </div>
            )}
          </div>
        </div>
      </section>
    </main>
  );
}
