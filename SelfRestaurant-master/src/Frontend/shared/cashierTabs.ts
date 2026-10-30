export type CashierTabKey = "quay" | "cho-thu-tien" | "dat-ban" | "phuc-vu" | "chuyen-khoan";

export type CashierTabDef = {
  key: CashierTabKey;
  label: string;
  icon: string;
  /** Read aloud with the count, e.g. "2 giao dịch chờ thu tiền". */
  countNoun: string;
};

export const CASHIER_TABS: readonly CashierTabDef[] = [
  { key: "quay", label: "Quầy thanh toán", icon: "bi-cash-coin", countNoun: "" },
  { key: "cho-thu-tien", label: "Chờ thu tiền", icon: "bi-hourglass-split", countNoun: "giao dịch chờ thu tiền" },
  { key: "dat-ban", label: "Đặt bàn", icon: "bi-calendar-check", countNoun: "việc đặt bàn cần xử lý" },
  { key: "phuc-vu", label: "Gọi phục vụ", icon: "bi-bell", countNoun: "yêu cầu phục vụ" },
  { key: "chuyen-khoan", label: "Chuyển khoản", icon: "bi-bank", countNoun: "khoản chuyển cần xử lý" },
];

/** "#cho-thu-tien" → that tab; anything unknown or empty → the counter. */
export function tabFromHash(hash: string | null | undefined): CashierTabKey {
  const key = (hash ?? "").replace(/^#/, "");
  return CASHIER_TABS.some((tab) => tab.key === key) ? (key as CashierTabKey) : "quay";
}

/** Accessible name of a tab button: the label plus the waiting count as a full phrase. */
export function tabAccessibleName(tab: CashierTabDef, count: number): string {
  return count > 0 && tab.countNoun ? `${tab.label}, ${count} ${tab.countNoun}` : tab.label;
}

/** Badge text: 1–99 as is, more shows "99+". */
export function badgeText(count: number): string {
  return count > 99 ? "99+" : String(count);
}
