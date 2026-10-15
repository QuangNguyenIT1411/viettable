import { expect, request, type APIRequestContext } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { appendFileSync, existsSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

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
const PASSWORD = "E2e-pass-123";

// Every test session is recorded so it can be released after the test, or by the next run if a run is interrupted.
// Only 10 test tables exist (11-20); a session left open keeps its table blocked for app check-in.
const SESSIONS_FILE = join(__dirname, "..", ".e2e-sessions.jsonl");
// Sessions opened by the current test; released with their own token (Identity allows only 30 logins a minute).
const openedThisTest: Array<{ email: string; token: string }> = [];
function recordSession(email: string, sessionId: number, token: string) {
  appendFileSync(SESSIONS_FILE, JSON.stringify({ email, sessionId }) + "\n");
  openedThisTest.push({ email, token });
}

export function hasSessionsFromThisTest() {
  return openedThisTest.length > 0;
}

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
    data: { name: `E2E ${id}`, username: email, email, password: PASSWORD, phoneNumber: `09${Date.now().toString().slice(-8)}` },
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
  recordSession(email, session.diningSessionId, token);

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
  return requestOnsitePayment(f, "CASH", "CASHIER");
}

/** Customer asks to pay ONSITE in cash or by bank transfer (VietQR); returns the pending payment id. */
export async function requestOnsitePayment(f: DiningFixture, method: "CASH" | "TRANSFER", location: "TABLE" | "CASHIER" = "TABLE"): Promise<number> {
  const auth = (extra: Record<string, string> = {}) => ({ Authorization: `Bearer ${f.token}`, ...extra });
  const quote = await json(await f.api.post(`/api/mobile/v1/dining-sessions/${f.sessionId}/checkout/quote`, {
    headers: auth({ "Idempotency-Key": key() }), data: {},
  }), "quote");
  await json(await f.api.post(`/api/mobile/v1/dining-sessions/${f.sessionId}/payment-request`, {
    headers: auth({ "Idempotency-Key": key() }), data: { checkoutQuoteId: quote.checkoutQuoteId },
  }), "payment-request");
  const payment = await json(await f.api.post(`/api/mobile/v1/checkout/quotes/${quote.checkoutQuoteId}/payments`, {
    headers: auth({ "Idempotency-Key": key() }), data: { channel: "ONSITE", method, location },
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

/** VietQR the app shows for a pending bank-transfer payment. */
export async function appTransferQr(f: DiningFixture, paymentId: number) {
  return json(await f.api.get(`/api/mobile/v1/payments/${paymentId}/transfer-qr`, {
    headers: { Authorization: `Bearer ${f.token}` },
  }), "transfer-qr");
}

/** Payment options the app receives (cash is always possible; transfer when the account is configured). */
export async function appPaymentOptions(f: DiningFixture) {
  return json(await f.api.get("/api/mobile/v1/payment-options", { headers: { Authorization: `Bearer ${f.token}` } }), "payment-options");
}

/**
 * Leaves no test session open: cancels a pending payment request, cancels an empty session, and orders again
 * when every order was cancelled (such a session cannot be cancelled), so the cashier cleanup can settle it.
 * Sessions that still have an order are settled afterwards by settleTestTables.
 */
export async function releaseRecordedSessions(baseURL: string, includeEarlierRuns = false) {
  // This test's sessions use their tokens; sessions from an interrupted earlier run need a login.
  const targets: Array<{ email: string; token?: string }> = openedThisTest.splice(0);
  if (includeEarlierRuns && existsSync(SESSIONS_FILE)) {
    const known = new Set(targets.map((t) => t.email));
    for (const line of readFileSync(SESSIONS_FILE, "utf8").split("\n").filter(Boolean)) {
      const { email } = JSON.parse(line) as { email: string };
      if (!known.has(email)) { known.add(email); targets.push({ email }); }
    }
  }
  if (targets.length === 0) return;
  const api = await request.newContext({ baseURL });
  try {
    for (const target of targets) {
      let token = target.token;
      if (!token) {
        const login = await api.post("/api/mobile/v1/auth/login", { data: { username: target.email, password: PASSWORD } });
        if (!login.ok()) continue;
        token = (await login.json()).accessToken as string;
      }
      const auth = { Authorization: `Bearer ${token}` };
      const active = await api.get("/api/mobile/v1/dining-sessions/active", { headers: auth });
      const session = active.ok() ? await active.json().catch(() => null) : null;
      if (!session?.diningSessionId) continue;
      const id = session.diningSessionId;
      if (session.status === "PAYMENT_REQUESTED")
        await api.post(`/api/mobile/v1/dining-sessions/${id}/payment-request/cancel`, { headers: auth, data: {} });
      const cancelled = await api.post(`/api/mobile/v1/dining-sessions/${id}/cancel`, { headers: auth, data: {} });
      if (cancelled.ok()) continue;
      const orders = await (await api.get("/api/mobile/v1/orders?scope=active", { headers: auth })).json().catch(() => []);
      if (Array.isArray(orders) && orders.length > 0) continue;
      const menu = await (await api.get("/api/mobile/v1/branches/1/menu")).json();
      const dish = menu.categories.flatMap((c: any) => c.dishes).find((d: any) => d.available);
      await api.post(`/api/mobile/v1/dining-sessions/${id}/orders`, {
        headers: { ...auth, "Idempotency-Key": key() }, data: { items: [{ dishId: dish.dishId, quantity: 1, note: "e2e cleanup" }] },
      });
    }
  } finally {
    await api.dispose();
  }
}

/** Forget recorded sessions once they are settled. */
export function clearRecordedSessions() {
  if (existsSync(SESSIONS_FILE)) writeFileSync(SESSIONS_FILE, "");
}
