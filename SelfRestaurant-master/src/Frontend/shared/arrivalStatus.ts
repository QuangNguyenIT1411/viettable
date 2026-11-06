// The guest's answer to the booking reminder (owner 06/11), shown on the cashier's booking cards.
export type ArrivalBadge = { label: string; tone: "success" | "neutral" | "danger" };

const OPEN = new Set(["Pending", "Confirmed", "PendingDeposit"]);

/** "Khách xác nhận sẽ đến" / "Chưa phản hồi" for an open booking; "Khách huỷ từ lời nhắc"; null otherwise. */
export function arrivalBadge(status: string | null | undefined, arrivalConfirmation: string | null | undefined): ArrivalBadge | null {
  if (arrivalConfirmation === "Cancelled" && status === "Cancelled") return { label: "Khách huỷ từ lời nhắc", tone: "danger" };
  if (!status || !OPEN.has(status)) return null;
  if (arrivalConfirmation === "WillCome") return { label: "Khách xác nhận sẽ đến", tone: "success" };
  return { label: "Chưa phản hồi", tone: "neutral" };
}
