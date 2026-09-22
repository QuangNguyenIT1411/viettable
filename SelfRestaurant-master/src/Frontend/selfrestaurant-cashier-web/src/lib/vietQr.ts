const VIET_QR_BANK_ID = "BIDV";
const VIET_QR_ACCOUNT_NUMBER = "8830150124";
const VIET_QR_TEMPLATE = "compact2";

function normalizeTransferReference(orderCode?: string | null, orderId?: number | null) {
  const code = (orderCode ?? "").trim();
  if (code) {
    return `TT ${code}`.slice(0, 40);
  }

  if (orderId && orderId > 0) {
    return `TT ORD${orderId}`.slice(0, 40);
  }

  return "TT SELFRESTAURANT";
}

export function buildCashierTransferReference(orderCode?: string | null, orderId?: number | null) {
  return normalizeTransferReference(orderCode, orderId);
}

export function buildCashierVietQrUrl(input: { amount: number; orderCode?: string | null; orderId?: number | null }) {
  const amount = Math.max(0, Math.round(Number.isFinite(input.amount) ? input.amount : 0));
  const params = new URLSearchParams();

  if (amount > 0) {
    params.set("amount", String(amount));
  }

  params.set("addInfo", normalizeTransferReference(input.orderCode, input.orderId));
  return `https://img.vietqr.io/image/${VIET_QR_BANK_ID}-${VIET_QR_ACCOUNT_NUMBER}-${VIET_QR_TEMPLATE}.png?${params.toString()}`;
}

export const cashierQrBankInfo = {
  bankName: "BIDV",
  accountNumber: VIET_QR_ACCOUNT_NUMBER,
} as const;
