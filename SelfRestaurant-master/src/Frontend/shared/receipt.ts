/**
 * "PHIẾU THANH TOÁN" for the cashier screen (owner 05/11/2026): the same rows, order and wording as the app's PDF
 * (app/.../util/ReceiptText.java). Every number comes from the server's snapshot of the paid bill; a row with nothing
 * in it is left out. 80 mm wide for the counter's thermal printer. Never "Hoá đơn điện tử" / "Hoá đơn GTGT".
 */
export type ReceiptLine = { dishId: number; dishName: string; quantity: number; unitPrice: number; lineTotal: number };
export type Receipt = {
  invoiceId: number; invoiceCode?: string | null; branchName?: string | null; branchAddress?: string | null; tableName?: string | null;
  sessionStartedAtUtc?: string | null; paidAtUtc?: string | null; issuedAtUtc?: string | null; cashierName?: string | null;
  items: ReceiptLine[]; subtotal: number; promotionName?: string | null; promotionDiscount?: number; voucherCode?: string | null;
  voucherDiscount?: number; manualDiscount?: number; pointsUsed?: number; pointsDiscount?: number;
  serviceFeePercent?: number | null; serviceFeeAmount?: number; vatPercent?: number | null; vatAmount?: number;
  depositApplied?: number; grossTotal?: number; totalAmount: number; amountPaid?: number; changeAmount?: number;
  paymentMethod?: string | null; bankTransactionRef?: string | null; pointsEarned?: number | null;
};
export type ReceiptRow = { label: string; value: string; kind: "normal" | "discount" | "total" | "paid" };

export const RECEIPT_TITLE = "PHIẾU THANH TOÁN";
export const NOT_VAT_INVOICE = "Không phải hoá đơn GTGT";

export function money(value: number): string {
  return `${Math.round(value).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ".")}đ`;
}

export function percent(value: number): string {
  return `${Number(value.toFixed(2)).toString().replace(".", ",")}%`;
}

export function methodLabel(method?: string | null): string {
  switch ((method ?? "").trim().toUpperCase()) {
    case "CASH": return "Tiền mặt";
    case "QR": case "TRANSFER": case "BANK_TRANSFER": return "Chuyển khoản";
    case "CARD": return "Thẻ";
    case "VNPAY": return "VNPAY";
    default: return (method ?? "").trim();
  }
}

const points = (n: number) => Math.round(n).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ".");

export function amountRows(r: Receipt): ReceiptRow[] {
  const rows: ReceiptRow[] = [{ label: "Tạm tính", value: money(r.subtotal), kind: "normal" }];
  if ((r.promotionDiscount ?? 0) > 0)
    rows.push({ label: r.promotionName ? `Khuyến mãi "${r.promotionName}"` : "Khuyến mãi", value: `−${money(r.promotionDiscount!)}`, kind: "discount" });
  if ((r.voucherDiscount ?? 0) > 0)
    rows.push({ label: r.voucherCode ? `Voucher ${r.voucherCode}` : "Voucher", value: `−${money(r.voucherDiscount!)}`, kind: "discount" });
  if ((r.manualDiscount ?? 0) > 0) rows.push({ label: "Giảm giá", value: `−${money(r.manualDiscount!)}`, kind: "discount" });
  if ((r.pointsDiscount ?? 0) > 0)
    rows.push({ label: `Dùng ${points(r.pointsUsed ?? 0)} điểm`, value: `−${money(r.pointsDiscount!)}`, kind: "discount" });
  if (r.serviceFeePercent != null && (r.serviceFeePercent > 0 || (r.serviceFeeAmount ?? 0) > 0))
    rows.push({ label: `Phí phục vụ (${percent(r.serviceFeePercent)})`, value: money(r.serviceFeeAmount ?? 0), kind: "normal" });
  if (r.vatPercent != null && (r.vatPercent > 0 || (r.vatAmount ?? 0) > 0))
    rows.push({ label: `VAT (${percent(r.vatPercent)})`, value: money(r.vatAmount ?? 0), kind: "normal" });
  if ((r.depositApplied ?? 0) > 0) {
    rows.push({ label: "Tổng hoá đơn", value: money(r.grossTotal ?? r.totalAmount + (r.depositApplied ?? 0)), kind: "normal" });
    rows.push({ label: "Đã đặt cọc", value: `−${money(r.depositApplied!)}`, kind: "discount" });
  }
  rows.push({ label: "Tổng thanh toán", value: money(r.totalAmount), kind: "total" });
  rows.push({ label: "Đã trả", value: money(r.amountPaid ?? r.totalAmount), kind: "paid" });
  if ((r.changeAmount ?? 0) > 0) rows.push({ label: "Tiền thừa", value: money(r.changeAmount!), kind: "normal" });
  return rows;
}

export function paymentRows(r: Receipt): ReceiptRow[] {
  const rows: ReceiptRow[] = [];
  const method = methodLabel(r.paymentMethod);
  if (method) rows.push({ label: "Phương thức", value: method, kind: "normal" });
  if (r.bankTransactionRef) rows.push({ label: "Mã giao dịch ngân hàng", value: r.bankTransactionRef, kind: "normal" });
  if (r.pointsEarned != null && r.pointsEarned > 0) rows.push({ label: "Điểm được cộng", value: `+${points(r.pointsEarned)} điểm`, kind: "normal" });
  return rows;
}

function vnTime(utc?: string | null): string {
  if (!utc) return "";
  const d = new Date(/Z$|[+-]\d\d:?\d\d$/.test(utc) ? utc : `${utc}Z`);
  if (Number.isNaN(d.getTime())) return "";
  const v = new Date(d.getTime() + 7 * 3600 * 1000);
  const two = (n: number) => String(n).padStart(2, "0");
  return `${two(v.getUTCHours())}:${two(v.getUTCMinutes())}, ${two(v.getUTCDate())}/${two(v.getUTCMonth() + 1)}/${v.getUTCFullYear()}`;
}

export function timeRange(startedAtUtc?: string | null, paidAtUtc?: string | null): string {
  const end = vnTime(paidAtUtc);
  if (!end) return "";
  const start = vnTime(startedAtUtc);
  return start ? `${start.slice(0, 5)} – ${end}` : end;
}

const esc = (s: string) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");

/** A whole printable page (80 mm) for one receipt. */
export function receiptHtml(r: Receipt): string {
  const pair = (row: ReceiptRow) =>
    `<div class="row ${row.kind}"><span>${esc(row.label)}</span><span>${esc(row.value)}</span></div>`;
  const info: ReceiptRow[] = [{ label: "Mã hoá đơn", value: r.invoiceId > 0 ? `#${r.invoiceId}` : r.invoiceCode ?? "", kind: "normal" }];
  if (r.tableName) info.push({ label: "Bàn", value: r.tableName, kind: "normal" });
  const time = timeRange(r.sessionStartedAtUtc, r.paidAtUtc ?? r.issuedAtUtc);
  if (time) info.push({ label: "Giờ vào – thanh toán", value: time, kind: "normal" });
  if (r.cashierName) info.push({ label: "Thu ngân", value: r.cashierName, kind: "normal" });
  const items = r.items.length === 0
    ? `<div class="muted">Hoá đơn này không lưu danh sách món.</div>`
    : r.items.map(i => `<div class="dish"><b>${esc(i.dishName)}</b><div class="row"><span>${i.quantity} × ${money(i.unitPrice)}</span><span>${money(i.lineTotal)}</span></div></div>`).join("");
  const pay = paymentRows(r);
  return `<!doctype html><html lang="vi"><head><meta charset="utf-8"><title>${RECEIPT_TITLE} #${r.invoiceId}</title>
<style>
@page { size: 80mm auto; margin: 4mm; }
body { font-family: "Segoe UI", Arial, sans-serif; width: 72mm; margin: 0 auto; color: #000; font-size: 11.5px; }
h1 { font-size: 15px; text-align: center; margin: 2mm 0 0; letter-spacing: .04em; }
.center { text-align: center; } .muted { color: #444; font-size: 10px; }
.rule { border-top: 1px dashed #777; margin: 2.5mm 0; }
.row { display: flex; justify-content: space-between; gap: 3mm; padding: .6mm 0; }
.row span:last-child { text-align: right; white-space: nowrap; }
.total { font-weight: 700; font-size: 13px; } .paid { font-weight: 600; }
.dish { padding: .8mm 0; }
</style></head><body>
<h1>${RECEIPT_TITLE}</h1><div class="center muted">${NOT_VAT_INVOICE}</div>
${r.branchName ? `<div class="center"><b>${esc(r.branchName)}</b></div>` : ""}${r.branchAddress ? `<div class="center muted">${esc(r.branchAddress)}</div>` : ""}
<div class="rule"></div>${info.map(pair).join("")}<div class="rule"></div>${items}<div class="rule"></div>
${amountRows(r).map(pair).join("")}${pay.length ? `<div class="rule"></div>${pay.map(pair).join("")}` : ""}
<div class="rule"></div><div class="center muted">Cảm ơn quý khách — hẹn gặp lại!</div>
</body></html>`;
}
