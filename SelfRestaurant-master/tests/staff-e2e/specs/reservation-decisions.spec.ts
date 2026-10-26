import { expect, request, test, type APIRequestContext } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { settleAfterTest } from "../support/cleanup";
import { recordTestSession } from "../support/customer";
import { accounts, login, password } from "../support/staff";

// Reservation item 2 (26/10) against the LOCAL stack: a booking from the app waits for the cashier ("Đặt bàn chờ
// xác nhận"), who confirms or rejects it with a reason; a check-in while pending confirms it. Every booking made
// here is cancelled afterwards (a cancelled or rejected one no longer holds its table).
type Customer = { api: APIRequestContext; token: string; email: string };
const created: Array<{ customer: Customer; id: number }> = [];
const key = () => randomUUID().replace(/-/g, "").slice(0, 32);
const auth = (c: Customer, extra: Record<string, string> = {}) => ({ Authorization: `Bearer ${c.token}`, ...extra });

test.afterEach(async ({ baseURL }) => {
  for (const r of created.splice(0)) await r.customer.api.post(`/api/mobile/v1/reservations/${r.id}/cancel`, { headers: auth(r.customer) });
  await settleAfterTest(baseURL!);
});

async function customer(baseURL: string): Promise<Customer> {
  const api = await request.newContext({ baseURL });
  const id = randomUUID().slice(0, 8);
  const email = `e2e.dec.${id}@viettable.local`;
  const response = await api.post("/api/mobile/v1/auth/register", {
    data: { name: `E2E ${id}`, username: email, email, password: "E2e-pass-123", phoneNumber: `09${Date.now().toString().slice(-8)}` } });
  expect(response.ok(), await response.text()).toBeTruthy();
  return { api, token: (await response.json()).accessToken, email };
}

async function cashierApi(baseURL: string) {
  const context = await request.newContext({ baseURL, extraHTTPHeaders: { Origin: baseURL } });
  expect((await context.post("/api/gateway/staff/auth/login", { data: { username: accounts.cashier.username, password } })).ok()).toBeTruthy();
  return context;
}

/** Restaurant (UTC+7) wall clock → ISO with offset. */
function vnTime(dayOffset: number, hour: number, minute = 0) {
  const vn = new Date(Date.now() + 7 * 3600_000);
  const day = new Date(Date.UTC(vn.getUTCFullYear(), vn.getUTCMonth(), vn.getUTCDate() + dayOffset));
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${day.getUTCFullYear()}-${pad(day.getUTCMonth() + 1)}-${pad(day.getUTCDate())}T${pad(hour)}:${pad(minute)}:00+07:00`;
}

/** Now + minutes as an instant, or null when that is outside 10:00–21:30 restaurant time. */
function soon(minutes: number) {
  const at = new Date(Date.now() + minutes * 60_000);
  const vn = new Date(at.getTime() + 7 * 3600_000);
  const hm = vn.getUTCHours() * 60 + vn.getUTCMinutes();
  return hm >= 600 && hm <= 1290 ? at.toISOString() : null;
}

async function reserve(c: Customer, body: Record<string, unknown>) {
  const response = await c.api.post("/api/mobile/v1/reservations", { headers: auth(c, { "Idempotency-Key": key() }), data: { branchId: 1, ...body } });
  const text = await response.text();
  expect(response.ok(), text).toBeTruthy();
  const reservation = JSON.parse(text);
  created.push({ customer: c, id: reservation.reservationId });
  return reservation;
}

async function inboxTitles(c: Customer) {
  const response = await c.api.get("/api/mobile/v1/notifications?limit=50", { headers: auth(c) });
  expect(response.ok()).toBeTruthy();
  return (await response.json()).items as Array<{ title: string; message: string; category: string; refId: number }>;
}

test("đặt từ app ở trạng thái chờ; thu ngân thấy ngay ở 'Đặt bàn chờ xác nhận' và bấm Xác nhận; khách nhận thông báo", async ({ page, baseURL }) => {
  await login(page, "cashier");
  const guest = await customer(baseURL!);
  const reservation = await reserve(guest, { partySize: 2, reservedAt: vnTime(3, 19) });
  expect(reservation.status).toBe("Pending");

  // Ordering ahead opens only after the restaurant confirmed.
  const early = await guest.api.put(`/api/mobile/v1/reservations/${reservation.reservationId}/preorder`, {
    headers: auth(guest), data: { items: [{ dishId: 1, quantity: 1 }] } });
  expect(early.status()).toBe(409);

  // The panel lists bookings of the coming days too; it appears without reloading the page (realtime or fallback poll).
  const card = page.locator(`[data-pending-reservation="${reservation.reservationId}"]`);
  await expect(card).toBeVisible({ timeout: 30_000 });
  await expect(card).toContainText(reservation.reservationCode);
  await card.getByRole("button", { name: /Xác nhận$/ }).click(); // the name starts with the icon glyph
  await expect(card).toHaveCount(0, { timeout: 15_000 });

  const after = await (await guest.api.get(`/api/mobile/v1/reservations/${reservation.reservationId}`, { headers: auth(guest) })).json();
  expect(after.status).toBe("Confirmed");
  expect(after.statusChangedByEmployeeId).toBeGreaterThan(0);
  const mine = (await inboxTitles(guest)).filter(x => x.category === "reservation" && x.refId === reservation.reservationId);
  expect(mine.map(x => x.title)).toEqual(["Đặt bàn đã được xác nhận", "Đặt bàn thành công"]);
});

test("từ chối kèm lý do chọn nhanh: khách thấy lý do, bàn được nhả cho người khác đặt", async ({ page, baseURL }) => {
  const guest = await customer(baseURL!);
  const at = vnTime(4, 12, 30);
  const reservation = await reserve(guest, { partySize: 2, reservedAt: at });
  await login(page, "cashier");
  const card = page.locator(`[data-pending-reservation="${reservation.reservationId}"]`);
  await expect(card).toBeVisible({ timeout: 30_000 });
  await card.getByRole("button", { name: "Từ chối" }).click();
  await card.getByLabel("Ngoài giờ phục vụ").check();
  await card.getByRole("button", { name: "Từ chối đặt bàn" }).click();
  await expect(card).toHaveCount(0, { timeout: 15_000 });

  const after = await (await guest.api.get(`/api/mobile/v1/reservations/${reservation.reservationId}`, { headers: auth(guest) })).json();
  expect(after.status).toBe("Rejected");
  expect(after.statusReason).toBe("Ngoài giờ phục vụ");
  const rejected = (await inboxTitles(guest)).find(x => x.title === "Đặt bàn bị từ chối" && x.refId === reservation.reservationId);
  expect(rejected?.message).toMatch(/^Đặt bàn bị từ chối: Ngoài giờ phục vụ\./);

  // The table is free again: another guest books exactly that table at that time.
  const other = await customer(baseURL!);
  const rebooked = await reserve(other, { tableId: reservation.tableId, partySize: 2, reservedAt: at });
  expect(rebooked.tableId).toBe(reservation.tableId);
  // A rejected booking can no longer be confirmed or cancelled into something else.
  const cashier = await cashierApi(baseURL!);
  expect((await cashier.post(`/api/cashier/reservations/${reservation.reservationId}/confirm`, { data: {} })).status()).toBe(409);
  const noReason = await cashier.post(`/api/cashier/reservations/${rebooked.reservationId}/reject`, { data: { reason: " " } });
  expect(noReason.status()).toBe(400);
  await cashier.dispose();
});

test("bộ chọn ngày: danh sách theo ngày hiện đặt bàn của ngày đã chọn và trạng thái 'Bị từ chối'", async ({ page, baseURL }) => {
  const guest = await customer(baseURL!);
  const reservation = await reserve(guest, { partySize: 1, reservedAt: vnTime(5, 20) });
  const cashier = await cashierApi(baseURL!);
  expect((await cashier.post(`/api/cashier/reservations/${reservation.reservationId}/reject`, { data: { reason: "Hết bàn" } })).ok()).toBeTruthy();
  await cashier.dispose();

  await login(page, "cashier");
  const day = vnTime(5, 20).slice(0, 10);
  await page.getByLabel("Ngày đặt bàn").fill(day);
  const panel = page.locator(".cashier-reservation-panel");
  await expect(panel.getByRole("heading", { name: "Đặt bàn theo ngày" })).toBeVisible();
  const row = panel.locator(".cashier-reservation-card", { hasText: reservation.reservationCode });
  await expect(row).toBeVisible({ timeout: 15_000 });
  await expect(row).toContainText("Bị từ chối");
  await expect(row).toContainText("Lý do từ chối: Hết bàn");
});

test("khách quét QR khi đặt bàn còn chờ: check-in được và đặt bàn tự xác nhận", async ({ baseURL }) => {
  const at = soon(20);
  test.skip(!at, "now + 20 min is outside 10:00–21:30");
  const guest = await customer(baseURL!);
  const availability = await guest.api.get(`/api/mobile/v1/branches/1/availability?reservedAt=${encodeURIComponent(at!)}&partySize=1`, { headers: auth(guest) });
  const free = ((await availability.json()).availableTables as Array<{ tableId: number; tableNumber: number }>)
    .filter(t => t.tableNumber >= 11 && t.tableNumber <= 20);
  test.skip(free.length === 0, "no free test table 11-20 in the next 20 minutes");
  const reservation = await reserve(guest, { tableId: free[0].tableId, partySize: 1, reservedAt: at });
  expect(reservation.status).toBe("Pending");

  const qrCode = `QR-Q1-B${String(free[0].tableNumber).padStart(2, "0")}`;
  const checkIn = await guest.api.post("/api/mobile/v1/check-ins/reservation", {
    headers: auth(guest, { "Idempotency-Key": key() }), data: { reservationId: reservation.reservationId, qrCode, confirmTableMismatch: false } });
  expect(checkIn.ok(), await checkIn.text()).toBeTruthy();
  const session = await checkIn.json();

  const after = await (await guest.api.get(`/api/mobile/v1/reservations/${reservation.reservationId}`, { headers: auth(guest) })).json();
  expect(after.status).toBe("CheckedIn");
  expect(after.statusReason).toBe("Tự xác nhận khi check-in");
  expect(after.statusChangedByEmployeeId ?? null).toBeNull();

  // The shared cleanup gives the table back (a session opened from a booking cannot be cancelled by the guest, G16).
  recordTestSession(guest.email, session.diningSessionId, guest.token);
});
