import { request, type APIRequestContext, type FullConfig } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { accounts, password } from "./staff";
import { clearRecordedSessions, hasSessionsFromThisTest, releaseRecordedSessions } from "./customer";

// Tests open sessions on tables 11-20 and some leave them open (kitchen flow, table map). Before and after a run,
// settle every open order on those tables through the cashier dashboard checkout so the next run has free tables.
// One cashier login is reused by every cleanup (staff login is limited to 30 a minute); log in again when it expires.
let cashier: APIRequestContext | null = null;
async function cashierRequest(baseURL: string, fresh = false) {
  if (cashier && !fresh) return cashier;
  await cashier?.dispose();
  cashier = await request.newContext({ baseURL, extraHTTPHeaders: { Origin: baseURL } });
  const login = await cashier.post("/api/gateway/staff/auth/login", { data: { username: accounts.cashier.username, password } });
  if (!login.ok()) throw new Error(`cleanup: cashier login failed (${login.status()})`);
  return cashier;
}

/** Releases recorded sessions, then checks out every open order on tables 11-20. */
export async function settleTestTables(baseURL: string, includeEarlierRuns = true) {
  await releaseRecordedSessions(baseURL, includeEarlierRuns);
  let api = await cashierRequest(baseURL);
  let dashboard = await api.get("/api/gateway/staff/cashier/dashboard");
  if (dashboard.status() === 401) {
    api = await cashierRequest(baseURL, true);
    dashboard = await api.get("/api/gateway/staff/cashier/dashboard");
  }
  const testTables = ((await dashboard.json()).tables as Array<{ number: string; orderId?: number | null }>)
    .filter((t) => t.orderId && Number(/(\d+)\s*$/.exec(t.number)?.[1]) >= 11 && Number(/(\d+)\s*$/.exec(t.number)?.[1]) <= 20);
  for (const table of testTables) {
    const response = await api.post(`/api/gateway/staff/cashier/orders/${table.orderId}/checkout`, {
      data: { paymentMethod: "CASH", paymentAmount: 10_000_000, idempotencyKey: `e2e-cleanup-${randomUUID()}` },
    });
    if (!response.ok()) console.warn(`cleanup: ${table.number} order ${table.orderId} -> HTTP ${response.status()}`);
  }
  if (includeEarlierRuns) clearRecordedSessions();
}

/** After a test: only when it opened tables, give them back (no login otherwise). */
export async function settleAfterTest(baseURL: string) {
  if (hasSessionsFromThisTest()) await settleTestTables(baseURL, false);
}

export default async function globalSetup(config: FullConfig) {
  await settleTestTables(config.projects[0].use.baseURL as string);
}
