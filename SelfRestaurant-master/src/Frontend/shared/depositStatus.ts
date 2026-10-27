// Reservation deposits shown to staff (docs/DEPOSIT_PLAN.md). Money is never refunded automatically: a deposit
// "Cần hoàn cọc" waits until the cashier/admin refunded it by hand and pressed "Đã hoàn cọc".

export type DepositStatus =
  | "PENDING" | "EXPIRED" | "CANCELLED" | "PAID" | "APPLIED" | "REFUND_REQUIRED" | "REFUNDED" | "FORFEITED";

export type DepositDto = {
  reservationDepositId: number;
  reservationId: number;
  branchId: number;
  partySize: number;
  amount: number;
  status: DepositStatus | string;
  reservedAtUtc: string;
  dueAtUtc: string;
  paidAmount?: number | null;
  paidAtUtc?: string | null;
  appliedAmount?: number | null;
  closedReason?: string | null;
  refundedAtUtc?: string | null;
  transferContent: string;
};

export type DepositBookingDto = {
  reservationId: number;
  reservationCode: string;
  customerName: string;
  phoneNumber: string;
  reservedAt: string;
  partySize: number;
  status: string;
  statusReason?: string | null;
};

export type DepositListItem = { deposit: DepositDto; reservation: DepositBookingDto | null };

const labels: Record<string, string> = {
  PENDING: "Chờ khách chuyển",
  EXPIRED: "Hết hạn (chưa trả)",
  CANCELLED: "Đã hủy (chưa trả)",
  PAID: "Đã cọc",
  APPLIED: "Đã trừ vào hóa đơn",
  REFUND_REQUIRED: "Cần hoàn cọc",
  REFUNDED: "Đã hoàn cọc",
  FORFEITED: "Mất cọc",
};

export function depositStatusLabel(status: string | null | undefined): string {
  return labels[(status ?? "").trim().toUpperCase()] ?? (status || "Không rõ");
}

/** Badge colour: something to do (warning), money held (success), closed (neutral). */
export function depositTone(status: string | null | undefined): "warning" | "success" | "neutral" {
  const value = (status ?? "").trim().toUpperCase();
  if (value === "REFUND_REQUIRED" || value === "PENDING") return "warning";
  if (value === "PAID" || value === "APPLIED" || value === "REFUNDED") return "success";
  return "neutral";
}

/** Only a deposit waiting for a refund gets the "Đã hoàn cọc" button. */
export function canMarkRefunded(status: string | null | undefined): boolean {
  return (status ?? "").trim().toUpperCase() === "REFUND_REQUIRED";
}

/** Refunds to do first, then the newest. */
export function sortDeposits(items: DepositListItem[]): DepositListItem[] {
  return [...items].sort((a, b) => {
    const todo = Number(canMarkRefunded(b.deposit.status)) - Number(canMarkRefunded(a.deposit.status));
    return todo !== 0 ? todo : b.deposit.reservationDepositId - a.deposit.reservationDepositId;
  });
}
