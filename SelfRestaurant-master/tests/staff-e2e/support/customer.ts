import { expect, request, type APIRequestContext } from "@playwright/test";
import { randomUUID } from "node:crypto";

// Drives the customer side through the public mobile API on the LOCAL gateway so staff screens have real data.
export type DiningFixture = {
  api: APIRequestContext;
  token: string;
  sessionId: number;
  tableId: number;
  tableNumber: number;
  orderId: number;
  dishName: string;
  total: number;
};

const key = () => randomUUID().replace(/-/g, "").slice(0, 32);

async function json(response: Awaited<ReturnType<APIRequestContext["get"]>>, label: string) {
  const body = await response.text();
  expect(response.ok(), `${label}: HTTP ${response.status()} ${body.slice(0, 300)}`).toBeTruthy();
  return body ? JSON.parse(body) : null;
}

export async function openTableWithOrder(baseURL: string): Promise<DiningFixture> {
  const api = await request.newContext({ baseURL });
  const id = randomUUID().slice(0, 8);
  const email = `e2e.${id}@viettable.local`;
  const auth = await json(await api.post("/api/mobile/v1/auth/register", {
    data: { name: `E2E ${id}`, username: email, email, password: "E2e-pass-123", phoneNumber: `09${Date.now().toString().slice(-8)}` },
  }), "register");
  const token: string = auth.accessToken;
  const headers = (extra: Record<string, string> = {}) => ({ Authorization: `Bearer ${token}`, ...extra });

  // Use a free branch-1 table; seats 11-20 keep the low-numbered tables free for manual checks.
  // Seeded QR codes map QR-Q1-Bnn to "Bàn nn" of branch 1.
  let session: any = null;
  let tableNumber = 0;
  for (let n = 11; n <= 20 && !session; n++) {
    const qrCode = `QR-Q1-B${String(n).padStart(2, "0")}`;
    const response = await api.post("/api/mobile/v1/check-ins/walk-in", { headers: headers({ "Idempotency-Key": key() }), data: { qrCode } });
    if (response.ok()) { session = await response.json(); tableNumber = n; }
  }
  expect(session, "no free local table QR-Q1-B11..B20 for walk-in").toBeTruthy();

  const menu = await json(await api.get("/api/mobile/v1/branches/1/menu"), "menu");
  const dish = menu.categories.flatMap((c: any) => c.dishes).find((d: any) => d.available);
  expect(dish, "no available dish on branch 1").toBeTruthy();
  const order = await json(await api.post(`/api/mobile/v1/dining-sessions/${session.diningSessionId}/orders`, {
    headers: headers({ "Idempotency-Key": key() }), data: { items: [{ dishId: dish.dishId, quantity: 1, note: "e2e" }] },
  }), "order");

  return {
    api, token, sessionId: session.diningSessionId, tableId: session.tableId, tableNumber,
    orderId: order.orderId, dishName: dish.name, total: Number(order.subtotal),
  };
}

/** Customer asks to pay ONSITE in cash at the counter; returns the pending payment id the cashier must confirm. */
export async function requestOnsiteCashPayment(f: DiningFixture): Promise<number> {
  const auth = (extra: Record<string, string> = {}) => ({ Authorization: `Bearer ${f.token}`, ...extra });
  const quote = await json(await f.api.post(`/api/mobile/v1/dining-sessions/${f.sessionId}/checkout/quote`, {
    headers: auth({ "Idempotency-Key": key() }), data: {},
  }), "quote");
  await json(await f.api.post(`/api/mobile/v1/dining-sessions/${f.sessionId}/payment-request`, {
    headers: auth({ "Idempotency-Key": key() }), data: { checkoutQuoteId: quote.checkoutQuoteId },
  }), "payment-request");
  const payment = await json(await f.api.post(`/api/mobile/v1/checkout/quotes/${quote.checkoutQuoteId}/payments`, {
    headers: auth({ "Idempotency-Key": key() }), data: { channel: "ONSITE", method: "CASH", location: "CASHIER" },
  }), "payment");
  return payment.paymentId;
}

export async function sessionStatus(f: DiningFixture): Promise<string> {
  const body = await json(await f.api.get(`/api/mobile/v1/dining-sessions/${f.sessionId}`, {
    headers: { Authorization: `Bearer ${f.token}` },
  }), "session");
  return body.status;
}

/** The customer taps "Giục bếp" (KITCHEN_RUSH) or "Báo hủy món" (KITCHEN_CANCEL) in the app. */
export async function sendKitchenRequest(f: DiningFixture, requestType: "KITCHEN_RUSH" | "KITCHEN_CANCEL", note: string): Promise<number> {
  const body = await json(await f.api.post(`/api/mobile/v1/dining-sessions/${f.sessionId}/service-requests`, {
    headers: { Authorization: `Bearer ${f.token}`, "Idempotency-Key": key() }, data: { requestType, note },
  }), "kitchen request");
  return body.serviceRequestId;
}
