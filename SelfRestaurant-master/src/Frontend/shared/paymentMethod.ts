// Payment method codes from the API, shown to staff. Guests pay "Tiền mặt" or "Chuyển khoản";
// older codes (card, online gateways) keep a readable label.

/** Normalised group used for totals: CASH, TRANSFER or OTHER. */
export function paymentMethodGroup(code: string | null | undefined): "CASH" | "TRANSFER" | "OTHER" {
  const value = (code ?? "").trim().toUpperCase();
  if (value === "CASH") return "CASH";
  if (value === "QR" || value === "TRANSFER" || value === "BANK" || value === "BANK_TRANSFER") return "TRANSFER";
  return "OTHER";
}

export function paymentMethodLabel(code: string | null | undefined): string {
  const value = (code ?? "").trim().toUpperCase();
  switch (paymentMethodGroup(value)) {
    case "CASH":
      return "Tiền mặt";
    case "TRANSFER":
      return "Chuyển khoản";
    default:
      if (value === "CARD") return "Thẻ";
      if (value === "VNPAY") return "VNPAY";
      if (value === "APP") return "Ứng dụng";
      return value || "Khác";
  }
}
