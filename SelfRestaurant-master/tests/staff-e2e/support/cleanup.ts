import { request, type FullConfig } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { accounts, password } from "./staff";

// Tests open sessions on tables 11-20 and some leave them open (kitchen flow, table map). Before and after a run,
// settle every open order on those tables through the cashier dashboard checkout so the next run has free tables.
export async function settleTestTables(baseURL: string) {
  const api = await request.newContext({ baseURL, extraHTTPHeaders: { Origin: baseURL } });
  try {
    const login = await api.post("/api/gateway/staff/auth/login", { data: { username: accounts.cashier.username, password } });
    if (!login.ok()) throw new Error(`cleanup: cashier login failed (${login.status()})`);
    const dashboard = await (await api.get("/api/gateway/staff/cashier/dashboard")).json();
    const testTables = (dashboard.tables as Array<{ number: string; orderId?: number | null }>)
      .filter((t) => t.orderId && Number(/(\d+)\s*$/.exec(t.number)?.[1]) >= 11 && Number(/(\d+)\s*$/.exec(t.number)?.[1]) <= 20);
    for (const table of testTables) {
      const response = await api.post(`/api/gateway/staff/cashier/orders/${table.orderId}/checkout`, {
        data: { paymentMethod: "CASH", paymentAmount: 10_000_000, idempotencyKey: `e2e-cleanup-${randomUUID()}` },
      });
      if (!response.ok()) console.warn(`cleanup: ${table.number} order ${table.orderId} -> HTTP ${response.status()}`);
    }
    await api.post("/api/gateway/staff/cashier/auth/logout", { data: {} }).catch(() => undefined);
  } finally {
    await api.dispose();
  }
}

export default async function globalSetup(config: FullConfig) {
  await settleTestTables(config.projects[0].use.baseURL as string);
}
