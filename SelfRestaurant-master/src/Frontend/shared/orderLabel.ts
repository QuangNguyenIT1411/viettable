// Staff screens show orders like the customer app: "Đơn #45", never the long ORD-<timestamp>-<guid> code.
export function orderLabel(orderId?: number | string | null, orderCode?: string | null): string {
  const id = typeof orderId === "string" ? Number.parseInt(orderId, 10) : orderId;
  if (typeof id === "number" && Number.isInteger(id) && id > 0) return `Đơn #${id}`;
  const code = (orderCode ?? "").trim();
  return code || "-";
}
