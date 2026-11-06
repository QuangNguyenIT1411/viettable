// Waitlist rows for the cashier (owner 06/11): what each status means and who comes first.
export type WaitlistItem = {
  waitlistEntryId: number; contactName: string; phoneNumber: string; partySize: number; note?: string | null;
  windowStartUtc: string; windowEndUtc: string; status: string; position: number; createdAtUtc: string;
  offeredTableId?: number | null; offeredReservedAtUtc?: string | null; offerExpiresAtUtc?: string | null; closedReason?: string | null;
};

const LABELS: Record<string, string> = {
  Waiting: "Đang chờ", Offered: "Đang giữ bàn", Accepted: "Đã nhận bàn", Expired: "Hết giờ giữ bàn", Cancelled: "Khách huỷ", Closed: "Hết khung giờ",
};

export const waitlistStatusLabel = (status: string) => LABELS[status] ?? status;

export const waitlistTone = (status: string): "info" | "success" | "warning" | "secondary" =>
  status === "Offered" ? "warning" : status === "Waiting" ? "info" : status === "Accepted" ? "success" : "secondary";

/** Active ones first (holding a table, then waiting in sign-up order), then finished ones newest first. */
export function sortWaitlist<T extends WaitlistItem>(items: T[]): T[] {
  const rank = (s: string) => (s === "Offered" ? 0 : s === "Waiting" ? 1 : 2);
  return [...items].sort((a, b) => rank(a.status) - rank(b.status)
    || (rank(a.status) < 2 ? a.createdAtUtc.localeCompare(b.createdAtUtc) : b.createdAtUtc.localeCompare(a.createdAtUtc)));
}
