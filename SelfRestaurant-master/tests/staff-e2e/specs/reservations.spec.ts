import { expect, request, test, type APIRequestContext } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { newGuestChecksIn, openTableWithOrder, registerGuest } from "../support/customer";
import { settleAfterTest } from "../support/cleanup";
import { accounts, password } from "../support/staff";

// Reservation review 25/10, item 1 (A1–A10) against the LOCAL stack. Every reservation made here is cancelled after
// the test: a reservation within the hour now blocks walk-ins on tables 11-20 for the other tests.
type Customer = { api: APIRequestContext; token: string };
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
  const email = `e2e.res.${id}@viettable.local`;
  const response = await registerGuest(api, { name: `E2E ${id}`, username: email, email, password: "E2e-pass-123", phoneNumber: `09${Date.now().toString().slice(-8)}` });
  expect(response.ok(), await response.text()).toBeTruthy();
  return { api, token: (await response.json()).accessToken };
}

/** Restaurant (UTC+7) wall clock → ISO with offset. dayOffset 0 = today in Vietnam. */
function vnTime(dayOffset: number, hour: number, minute = 0) {
  const vn = new Date(Date.now() + 7 * 3600_000);
  const day = new Date(Date.UTC(vn.getUTCFullYear(), vn.getUTCMonth(), vn.getUTCDate() + dayOffset));
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${day.getUTCFullYear()}-${pad(day.getUTCMonth() + 1)}-${pad(day.getUTCDate())}T${pad(hour)}:${pad(minute)}:00+07:00`;
}

/** Now + minutes, as a restaurant wall-clock time, or null when outside 10:00–21:30. */
function soon(minutes: number) {
  const at = new Date(Date.now() + minutes * 60_000);
  const vn = new Date(at.getTime() + 7 * 3600_000);
  const hm = vn.getUTCHours() * 60 + vn.getUTCMinutes();
  return hm >= 600 && hm <= 1290 ? at.toISOString() : null;
}

async function reserveOk(c: Customer, body: Record<string, unknown>) {
  const response = await c.api.post("/api/mobile/v1/reservations", { headers: auth(c, { "Idempotency-Key": key() }), data: { branchId: 1, ...body } });
  const text = await response.text();
  expect(response.ok(), text).toBeTruthy();
  const reservation = JSON.parse(text);
  created.push({ customer: c, id: reservation.reservationId });
  return reservation;
}

async function availability(c: Customer, reservedAt: string, partySize = 1) {
  const response = await c.api.get(`/api/mobile/v1/branches/1/availability?reservedAt=${encodeURIComponent(reservedAt)}&partySize=${partySize}`,
    { headers: auth(c) });
  expect(response.ok(), await response.text()).toBeTruthy();
  return (await response.json()).availableTables as Array<{ tableId: number; tableNumber: number; numberOfSeats: number }>;
}

test("A1: bàn đang có khách vẫn đặt được cho ngày mai, nhưng không đặt được trong 2 giờ tới", async ({ baseURL }) => {
  const seated = await openTableWithOrder(baseURL!);                 // table occupied right now
  const guest = await customer(baseURL!);
  const tomorrow = await availability(guest, vnTime(1, 19));
  expect(tomorrow.map(t => t.tableId)).toContain(seated.tableId);
  const at = soon(30);
  test.skip(!at, "now + 30 min is outside 10:00–21:30");
  expect((await availability(guest, at!)).map(t => t.tableId)).not.toContain(seated.tableId);
});

test("A2: khách vãng lai không ngồi được bàn có người đặt trong 60 phút tới; chủ đặt bàn được nhắc check-in theo đặt bàn", async ({ baseURL }) => {
  const at = soon(45);
  test.skip(!at, "now + 45 min is outside 10:00–21:30");
  const owner = await customer(baseURL!);
  const free = (await availability(owner, at!)).filter(t => t.tableNumber >= 11 && t.tableNumber <= 20);
  test.skip(free.length === 0, "no free test table 11-20 in the next hour");
  const table = free[0];
  const reservation = await reserveOk(owner, { tableId: table.tableId, partySize: 1, reservedAt: at });

  // Someone else scans that table: refused with the reserved time.
  const qrCode = `QR-Q1-B${String(table.tableNumber).padStart(2, "0")}`;
  const other = await customer(baseURL!);
  const walkIn = await other.api.post("/api/mobile/v1/check-ins/walk-in", { headers: auth(other, { "Idempotency-Key": key() }), data: { qrCode } });
  expect(walkIn.status()).toBe(409);
  const refused = await walkIn.json();
  expect(refused.code).toBe("table_reserved_soon");
  expect(refused.message).toMatch(/đã có khách đặt lúc \d\d:\d\d/);

  // The owner scanning as a walk-in is told to check in with the reservation instead.
  const own = await owner.api.post("/api/mobile/v1/check-ins/walk-in", { headers: auth(owner, { "Idempotency-Key": key() }), data: { qrCode } });
  expect(own.status()).toBe(409);
  expect((await own.json()).code).toBe("reservation_check_in_required");

  // Once cancelled, the table is free for walk-ins again.
  expect((await owner.api.post(`/api/mobile/v1/reservations/${reservation.reservationId}/cancel`, { headers: auth(owner) })).ok()).toBeTruthy();
  expect(await newGuestChecksIn(baseURL!, table.tableNumber)).toBe(200);
});

test("A3/A7: hủy 2 lần không lỗi; danh sách 'Sắp tới' và 'Lịch sử' theo trạng thái", async ({ baseURL }) => {
  const guest = await customer(baseURL!);
  const reservation = await reserveOk(guest, { partySize: 2, reservedAt: vnTime(1, 12) });
  const upcoming = await (await guest.api.get("/api/mobile/v1/reservations?scope=upcoming", { headers: auth(guest) })).json();
  expect(upcoming.items.map((x: { reservationId: number }) => x.reservationId)).toContain(reservation.reservationId);

  for (let i = 0; i < 2; i++) {
    const cancel = await guest.api.post(`/api/mobile/v1/reservations/${reservation.reservationId}/cancel`, { headers: auth(guest) });
    expect(cancel.ok(), await cancel.text()).toBeTruthy();
    expect((await cancel.json()).status).toBe("Cancelled");
  }
  const history = await (await guest.api.get("/api/mobile/v1/reservations?scope=history", { headers: auth(guest) })).json();
  expect(history.items.map((x: { reservationId: number }) => x.reservationId)).toContain(reservation.reservationId);
});

test("A5: ngoài 10:00–21:30 bị từ chối bằng tiếng Việt", async ({ baseURL }) => {
  const guest = await customer(baseURL!);
  for (const [hour, minute] of [[22, 0], [9, 30]]) {
    const response = await guest.api.post("/api/mobile/v1/reservations", {
      headers: auth(guest, { "Idempotency-Key": key() }), data: { branchId: 1, partySize: 2, reservedAt: vnTime(1, hour, minute) } });
    expect(response.status()).toBe(400);
    const body = await response.json();
    expect(body.code).toBe("outside_booking_hours");
    expect(body.message).toContain("10:00 đến 21:30");
  }
  await reserveOk(guest, { partySize: 2, reservedAt: vnTime(1, 21, 30) });  // the last slot is accepted
});

test("A4: đổi sang nhiều khách hơn bàn cũ thì tự chuyển bàn phù hợp, không báo hết bàn", async ({ baseURL }) => {
  const guest = await customer(baseURL!);
  const at = vnTime(2, 12, 30);
  const tables = await availability(guest, at, 1);
  const smallest = [...tables].sort((a, b) => a.numberOfSeats - b.numberOfSeats || a.tableId - b.tableId)[0];
  const bigger = tables.filter(t => t.numberOfSeats > smallest.numberOfSeats);
  test.skip(bigger.length === 0, "branch 1 has only one table size free");
  const reservation = await reserveOk(guest, { tableId: smallest.tableId, partySize: 1, reservedAt: at });

  const update = await guest.api.put(`/api/mobile/v1/reservations/${reservation.reservationId}`, {
    headers: auth(guest, { "Idempotency-Key": key() }),
    data: { tableId: smallest.tableId, partySize: bigger[0].numberOfSeats, reservedAt: at } });
  expect(update.ok(), await update.text()).toBeTruthy();
  const changed = await update.json();
  expect(changed.partySize).toBe(bigger[0].numberOfSeats);
  expect(changed.tableId).not.toBe(smallest.tableId);
});

test("A10: hủy đặt bàn thì món đặt trước cũng bị hủy, không vào bếp", async ({ baseURL }) => {
  const guest = await customer(baseURL!);
  const reservation = await reserveOk(guest, { partySize: 2, reservedAt: vnTime(1, 18) });
  // Ordering ahead needs the restaurant's confirmation (item 2, 26/10).
  const cashier = await request.newContext({ baseURL, extraHTTPHeaders: { Origin: baseURL! } });
  expect((await cashier.post("/api/gateway/staff/auth/login", { data: { username: accounts.cashier.username, password } })).ok()).toBeTruthy();
  const confirmed = await cashier.post(`/api/cashier/reservations/${reservation.reservationId}/confirm`, { data: {} });
  expect(confirmed.ok(), await confirmed.text()).toBeTruthy();
  await cashier.dispose();
  const menu = await (await guest.api.get("/api/mobile/v1/branches/1/menu")).json();
  // A dish listed as available can be refused (local stock after many runs): try the next one.
  const dishes = menu.categories.flatMap((c: { dishes: Array<{ dishId: number; available: boolean }> }) => c.dishes)
    .filter((d: { available: boolean }) => d.available);
  let saved = false;
  for (const dish of dishes) {
    const put = await guest.api.put(`/api/mobile/v1/reservations/${reservation.reservationId}/preorder`, {
      headers: auth(guest), data: { items: [{ dishId: dish.dishId, quantity: 1 }] } });
    if (put.status() === 422) continue;
    expect(put.ok(), await put.text()).toBeTruthy();
    saved = true;
    break;
  }
  expect(saved, "no dish could be ordered ahead").toBeTruthy();
  const submit = await guest.api.post(`/api/mobile/v1/reservations/${reservation.reservationId}/preorder/submit`, {
    headers: auth(guest, { "Idempotency-Key": key() }) });
  expect(submit.ok(), await submit.text()).toBeTruthy();
  expect((await submit.json()).status).toBe("SUBMITTED");

  expect((await guest.api.post(`/api/mobile/v1/reservations/${reservation.reservationId}/cancel`, { headers: auth(guest) })).ok()).toBeTruthy();
  const preorder = await (await guest.api.get(`/api/mobile/v1/reservations/${reservation.reservationId}/preorder`, { headers: auth(guest) })).json();
  expect(preorder.status).toBe("CANCELLED");
});
