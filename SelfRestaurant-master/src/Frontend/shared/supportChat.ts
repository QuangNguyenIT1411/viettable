// "Chat với nhân viên hỗ trợ" (owner 06/11): wording shared by the cashier "Hỗ trợ" tab. Pure; unit tested.

export type SupportStatus = "Waiting" | "InProgress" | "Resolved";
export type SupportSender = "CUSTOMER" | "STAFF" | "AUTO";

export function supportStatusLabel(status: string | null | undefined): string {
  switch (status) {
    case "Waiting": return "Đang chờ";
    case "InProgress": return "Đang xử lý";
    case "Resolved": return "Đã giải quyết";
    default: return status ?? "";
  }
}

export function supportStatusTone(status: string | null | undefined): "warning" | "info" | "success" {
  return status === "Waiting" ? "warning" : status === "Resolved" ? "success" : "info";
}

/** "Chờ 5 phút", "Chờ 2 giờ 10 phút", "Chờ 1 ngày" — how long the customer has waited for an answer. */
export function waitingText(waitingSinceUtc: string | null | undefined, now: Date = new Date()): string {
  if (!waitingSinceUtc) return "";
  const since = Date.parse(/[zZ]|[+-]\d\d:?\d\d$/.test(waitingSinceUtc) ? waitingSinceUtc : `${waitingSinceUtc}Z`);
  if (Number.isNaN(since)) return "";
  const minutes = Math.max(0, Math.floor((now.getTime() - since) / 60000));
  if (minutes < 1) return "Vừa gửi";
  if (minutes < 60) return `Chờ ${minutes} phút`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return minutes % 60 === 0 ? `Chờ ${hours} giờ` : `Chờ ${hours} giờ ${minutes % 60} phút`;
  return `Chờ ${Math.floor(hours / 24)} ngày`;
}

/** Who wrote a message, as the cashier sees it. */
export function senderLabel(sender: string, senderName?: string | null, customerName?: string | null): string {
  if (sender === "AUTO") return "Tin nhắn tự động";
  if (sender === "STAFF") return senderName ? `Nhân viên ${senderName}` : "Nhân viên";
  return customerName || "Khách";
}

/** The customer's message has been seen by staff when the staff read marker reaches it. */
export function seenByStaff(messageId: number, staffLastReadMessageId: number): boolean {
  return messageId <= staffLastReadMessageId;
}

/** Total unread customer messages across the branch's open conversations (badge on the tab). */
export function totalUnread(conversations: { unread?: number; status?: string }[]): number {
  return conversations.reduce((sum, c) => sum + (c.status === "Resolved" ? 0 : Math.max(0, c.unread ?? 0)), 0);
}

/** A cashier tab key from the server's "actionTab" (only known tabs; anything else gives no link). */
export function actionHash(actionTab: string | null | undefined): string | null {
  return actionTab && ["dat-ban", "chuyen-khoan", "cho-thu-tien"].includes(actionTab) ? `#${actionTab}` : null;
}
