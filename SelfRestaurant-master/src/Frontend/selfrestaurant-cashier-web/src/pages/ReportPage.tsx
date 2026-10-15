import { paymentMethodLabel } from "../../../shared/paymentMethod";
import { userMessage, cleanText } from "../../../shared/apiError";
import { formatViDateTime, formatViIsoDate } from "../../../shared/dateFormat";
import { orderLabel } from "../../../shared/orderLabel";
import { useEffect, useState } from "react";
import { CashierTopbar } from "../components/CashierTopbar";
import { cashierApi } from "../lib/api";
import { parseApiUtcTimestamp } from "../lib/dateTime";
import type { CashierReportScreenDto } from "../lib/types";

function formatDateTime(value?: string | null) {
  if (!value) return "Ch\u01b0a c\u1eadp nh\u1eadt";
  const date = parseApiUtcTimestamp(value);
  return formatViDateTime(date, value);
}

function formatDate(value?: string | null) {
  if (!value) return "Ch\u01b0a c\u1eadp nh\u1eadt";
  return formatViIsoDate(value);
}

export function ReportPage() {
  const [reportDate, setReportDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<CashierReportScreenDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  async function load(date = reportDate) {
    setLoading(true);
    setError(null);
    try {
      setReport(await cashierApi.getReport(date));
    } catch (err) {
      setError(userMessage(err, "Kh\u00f4ng th\u1ec3 t\u1ea3i b\u00e1o c\u00e1o thu ng\u00e2n."));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  if (loading) return <div className="screen-message">{"\u0110ang t\u1ea3i b\u00e1o c\u00e1o thu ng\u00e2n..."}</div>;
  if (cleanText(error) && !report) return <div className="screen-message error-box">{cleanText(error)}</div>;
  if (!report) return null;

  return (
    <>
    <CashierTopbar
      active="report"
      branchName={report.staff.branchName}
      staffName={report.staff.name}
      roleName={report.staff.roleName}
    />
    <main className="cashier-shell cashier-subpage">
      <section className="cashier-page-heading">
        <div>
          <h1>{"Báo cáo doanh thu theo ngày"}</h1>
          <p className="muted">
            {"Nhân viên:"} {report.staff.name} ({report.staff.branchName})
          </p>
        </div>
      </section>

      {cleanText(error) ? <div className="error-box">{cleanText(error)}</div> : null}

      <section className="panel cashier-panel-card">
        <div className="cashier-panel-body">
          <div className="staff-toolbar">
            <div className="staff-toolbar-form">
              <label>
                {"Ch\u1ecdn ng\u00e0y"}
                <input type="date" value={reportDate} onChange={(e) => setReportDate(e.target.value)} />
              </label>
              <button className="cashier-button-primary" onClick={() => void load(reportDate)}>
                <i className="bi bi-search me-1" />
                {"Xem b\u00e1o c\u00e1o"}
              </button>
            </div>
            <button className="cashier-button-outline" onClick={() => window.print()}>
              <i className="bi bi-printer me-1" />
              {"In b\u00e1o c\u00e1o"}
            </button>
          </div>
        </div>
      </section>

      <section className="panel cashier-panel-card">
        <div className="cashier-panel-header">
          <div>
            <h2>{"T\u1ed5ng quan"}</h2>
            <p className="muted">{"Ng\u00e0y:"} {formatDate(report.date)}</p>
          </div>
          <span className="soft-badge success">{report.billCount} hóa đơn</span>
        </div>

        <div className="cashier-panel-body">
          <div className="cashier-report-metrics">
            <article className="cashier-report-metric-card">
              <span className="eyebrow">{"S\u1ed1 h\u00f3a \u0111\u01a1n"}</span>
              <strong>{report.billCount}</strong>
              <div className="muted">{"T\u1ed5ng h\u00f3a \u0111\u01a1n \u0111\u00e3 ghi nh\u1eadn."}</div>
            </article>
            <article className="cashier-report-metric-card">
              <span className="eyebrow">{"T\u1ed5ng doanh thu"}</span>
              <strong>{report.totalRevenue.toLocaleString("vi-VN")} {"\u0111"}</strong>
              <div className="muted">{"Doanh thu c\u1ee7a ng\u00e0y \u0111ang ch\u1ecdn."}</div>
            </article>
            <article className="cashier-report-metric-card">
              <span className="eyebrow">Tiền mặt</span>
              <strong>{(report.cashRevenue ?? 0).toLocaleString("vi-VN")} {"\u0111"}</strong>
              <div className="muted">Tổng thu bằng tiền mặt.</div>
            </article>
            <article className="cashier-report-metric-card">
              <span className="eyebrow">Chuyển khoản</span>
              <strong>{(report.transferRevenue ?? 0).toLocaleString("vi-VN")} {"\u0111"}</strong>
              <div className="muted">Tổng thu bằng chuyển khoản.</div>
            </article>
            {(report.otherRevenue ?? 0) > 0 ? (
              <article className="cashier-report-metric-card">
                <span className="eyebrow">Khác</span>
                <strong>{(report.otherRevenue ?? 0).toLocaleString("vi-VN")} {"\u0111"}</strong>
                <div className="muted">Phương thức cũ (thẻ, cổng online).</div>
              </article>
            ) : null}
          </div>

          <div className="table-scroll">
            <table className="staff-table">
              <thead>
                <tr>
                  <th>{"Th\u1eddi gian"}</th>
                  <th>{"M\u00e3 H\u0110"}</th>
                  <th>{"\u0110\u01a1n h\u00e0ng"}</th>
                  <th>{"B\u00e0n"}</th>
                  <th className="text-end">{"Th\u00e0nh ti\u1ec1n"}</th>
                  <th>PTTT</th>
                </tr>
              </thead>
              <tbody>
                {report.bills.length > 0 ? (
                  report.bills.map((bill) => (
                    <tr key={bill.billId}>
                      <td>{formatDateTime(bill.billTime)}</td>
                      <td>{bill.billCode}</td>
                      <td>{orderLabel(bill.orderId, bill.orderCode)}</td>
                      <td>{bill.tableName}</td>
                      <td className="text-end">{bill.totalAmount.toLocaleString("vi-VN")} {"\u0111"}</td>
                      <td>
                        {paymentMethodLabel(bill.paymentMethod)}
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={6} className="table-empty muted">
                      {"Kh\u00f4ng c\u00f3 h\u00f3a \u0111\u01a1n n\u00e0o trong ng\u00e0y n\u00e0y."}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      </section>
    </main>
    </>
  );
}
