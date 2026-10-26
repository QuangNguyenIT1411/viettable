import { expect, request, test, type APIRequestContext } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { registerGuest } from "../support/customer";

// Reservation item 5 (26/10) against the LOCAL stack: the booking map lists every table of the branch with its
// state, never another guest's details; two guests picking the same table get one booking and one clear refusal.
type Customer = { api: APIRequestContext; token: string };
type MapTable = { tableId: number; tableNumber: number; numberOfSeats: number; status: string; statusLabel: string };
const created: Array<{ customer: Customer; id: number }> = [];
const key = () => randomUUID().replace(/-/g, "").slice(0, 32);
const auth = (c: Customer, extra: Record<string, string> = {}) => ({ Authorization: `Bearer ${c.token}`, ...extra });

// Two accounts for the whole file: Identity limits registrations per minute and the full suite registers many.
let a: Customer;
let b: Customer;
test.beforeAll(async ({ baseURL }) => { [a, b] = [await customer(baseURL!), await customer(baseURL!)]; });

test.afterEach(async () => {
  for (const r of created.splice(0)) await r.customer.api.post(`/api/mobile/v1/reservations/${r.id}/cancel`, { headers: auth(r.customer) });
});

async function customer(baseURL: string): Promise<Customer> {
  const api = await request.newContext({ baseURL });
  const id = randomUUID().slice(0, 8);
  const email = `e2e.map.${id}@viettable.local`;
  const response = await registerGuest(api, { name: `E2E ${id}`, username: email, email, password: "E2e-pass-123", phoneNumber: `09${Date.now().toString().slice(-8)}` });
  expect(response.ok(), await response.text()).toBeTruthy();
  return { api, token: (await response.json()).accessToken };
}

function vnTime(dayOffset: number, hour: number, minute = 0) {
  const vn = new Date(Date.now() + 7 * 3600_000);
  const day = new Date(Date.UTC(vn.getUTCFullYear(), vn.getUTCMonth(), vn.getUTCDate() + dayOffset));
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${day.getUTCFullYear()}-${pad(day.getUTCMonth() + 1)}-${pad(day.getUTCDate())}T${pad(hour)}:${pad(minute)}:00+07:00`;
}

async function map(c: Customer, at: string, partySize: number, exclude?: number) {
  const response = await c.api.get(`/api/mobile/v1/branches/1/availability?reservedAt=${encodeURIComponent(at)}&partySize=${partySize}`
    + (exclude ? `&excludeReservationId=${exclude}` : ""), { headers: auth(c) });
  expect(response.ok(), await response.text()).toBeTruthy();
  return (await response.json()) as { tables: MapTable[]; availableTables: Array<{ tableId: number }> };
}

function book(c: Customer, body: Record<string, unknown>) {
  return c.api.post("/api/mobile/v1/reservations", { headers: auth(c, { "Idempotency-Key": key() }), data: { branchId: 1, ...body } });
}

async function bookOk(c: Customer, body: Record<string, unknown>) {
  const response = await book(c, body);
  const text = await response.text();
  expect(response.ok(), text).toBeTruthy();
  const reservation = JSON.parse(text);
  created.push({ customer: c, id: reservation.reservationId });
  return reservation;
}

test("sơ đồ trả mọi bàn của chi nhánh kèm trạng thái; bàn người khác đặt hiện 'Đã được đặt', không lộ thông tin", async () => {
  const at = vnTime(6, 19);
  const owner = a;
  const before = await map(owner, at, 2);
  expect(before.tables.length).toBeGreaterThan(before.availableTables.length - 1);
  const numbers = before.tables.map(t => t.tableNumber);
  expect(numbers).toEqual([...numbers].sort((a, b) => a - b));
  for (const t of before.tables) expect(["AVAILABLE", "BOOKED", "TOO_SMALL", "IN_USE", "INACTIVE"]).toContain(t.status);
  // The old list stays for older apps and equals the free tables of the map.
  expect(before.availableTables.map(t => t.tableId).sort()).toEqual(before.tables.filter(t => t.status === "AVAILABLE").map(t => t.tableId).sort());

  const free = before.tables.find(t => t.status === "AVAILABLE")!;
  const reservation = await bookOk(owner, { tableId: free.tableId, partySize: 2, reservedAt: at });

  const other = b;
  const seen = await map(other, at, 2);
  const taken = seen.tables.find(t => t.tableId === free.tableId)!;
  expect(taken.status).toBe("BOOKED");
  expect(taken.statusLabel).toBe("Đã được đặt");
  expect(Object.keys(taken).sort()).toEqual(["numberOfSeats", "status", "statusLabel", "tableId", "tableNumber"]);
  // One hour later the 2-hour hold still covers it; three hours later it is free again.
  expect((await map(other, vnTime(6, 20), 2)).tables.find(t => t.tableId === free.tableId)!.status).toBe("BOOKED");
  expect((await map(other, vnTime(6, 21, 30), 2)).tables.find(t => t.tableId === free.tableId)!.status).toBe("AVAILABLE");
  // Changing the booking: its own table shows free for its owner.
  expect((await map(owner, at, 2, reservation.reservationId)).tables.find(t => t.tableId === free.tableId)!.status).toBe("AVAILABLE");

  // Too many guests for a small table.
  const big = await map(other, at, 7);
  for (const t of big.tables.filter(x => x.numberOfSeats < 7 && x.status !== "INACTIVE")) expect(t.status).toBe("TOO_SMALL");
});

test("2 người cùng đặt 1 bàn: chỉ 1 người được, người sau nhận câu báo tiếng Việt 'table_taken'", async () => {
  const at = vnTime(6, 12, 30);
  const free = (await map(a, at, 2)).tables.find(t => t.status === "AVAILABLE")!;
  const [ra, rb] = await Promise.all([
    book(a, { tableId: free.tableId, partySize: 2, reservedAt: at }),
    book(b, { tableId: free.tableId, partySize: 2, reservedAt: at }),
  ]);
  const results = [{ c: a, r: ra }, { c: b, r: rb }];
  const winners = results.filter(x => x.r.ok());
  const losers = results.filter(x => !x.r.ok());
  for (const w of winners) created.push({ customer: w.c, id: (await w.r.json()).reservationId });
  expect(winners).toHaveLength(1);
  expect(losers[0].r.status()).toBe(409);
  const refused = await losers[0].r.json();
  expect(refused.code).toBe("table_taken");
  expect(refused.message).toContain(`Bàn ${free.tableNumber} vừa được người khác đặt`);
  expect(refused.details.tableId).toBe(free.tableId);

  // The loser's refreshed map shows that table booked; another table still works.
  const refreshed = await map(losers[0].c, at, 2);
  expect(refreshed.tables.find(t => t.tableId === free.tableId)!.status).toBe("BOOKED");
  const next = refreshed.tables.find(t => t.status === "AVAILABLE")!;
  await bookOk(losers[0].c, { tableId: next.tableId, partySize: 2, reservedAt: at });
});

test("đổi đặt bàn sang bàn người khác vừa giữ thì bị từ chối rõ ràng; giữ bàn cũ thì được", async () => {
  const at = vnTime(6, 18);
  const free = (await map(a, at, 2)).tables.filter(t => t.status === "AVAILABLE");
  const mine = await bookOk(a, { tableId: free[0].tableId, partySize: 2, reservedAt: at });
  await bookOk(b, { tableId: free[1].tableId, partySize: 2, reservedAt: at });

  const toTaken = await a.api.put(`/api/mobile/v1/reservations/${mine.reservationId}`, {
    headers: auth(a, { "Idempotency-Key": key() }), data: { tableId: free[1].tableId, partySize: 2, reservedAt: at } });
  expect(toTaken.status()).toBe(409);
  expect((await toTaken.json()).code).toBe("table_taken");

  const keep = await a.api.put(`/api/mobile/v1/reservations/${mine.reservationId}`, {
    headers: auth(a, { "Idempotency-Key": key() }), data: { tableId: free[0].tableId, partySize: 2, reservedAt: vnTime(6, 18, 30) } });
  expect(keep.ok(), await keep.text()).toBeTruthy();
  expect((await keep.json()).tableId).toBe(free[0].tableId);
});
