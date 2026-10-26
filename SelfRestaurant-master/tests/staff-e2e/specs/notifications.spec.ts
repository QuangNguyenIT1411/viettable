import { expect, request as playwrightRequest, test, type APIRequestContext } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { openTableWithOrder, requestOnsitePayment, sendKitchenRequest, type DiningFixture, registerGuest } from "../support/customer";
import { settleAfterTest } from "../support/cleanup";
import { accounts, password } from "../support/staff";

// Notifications review 25/10 against the LOCAL stack: every customer event lands in the inbox under the right tab,
// once, for its owner only; the bell's unread count and "read all"; the booking contact phone.
test.afterEach(async ({ baseURL }) => { await settleAfterTest(baseURL!); });

type Inbox = { items: Array<{ type: string; title: string; category: string; refId: number; message: string; isRead: boolean }>; unreadCount: number };
const key = () => randomUUID().replace(/-/g, "").slice(0, 32);

async function inbox(api: APIRequestContext, token: string): Promise<Inbox> {
  const response = await api.get("/api/mobile/v1/notifications?limit=100", { headers: { Authorization: `Bearer ${token}` } });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

async function staff(baseURL: string, role: "chef" | "cashier") {
  const context = await playwrightRequest.newContext({ baseURL, extraHTTPHeaders: { Origin: baseURL } });
  expect((await context.post("/api/gateway/staff/auth/login", { data: { username: accounts[role].username, password } })).ok()).toBeTruthy();
  return context;
}

async function newCustomer(baseURL: string) {
  const api = await playwrightRequest.newContext({ baseURL });
  const id = randomUUID().slice(0, 8);
  const email = `e2e.notif.${id}@viettable.local`;
  const response = await registerGuest(api, { name: `E2E ${id}`, username: email, email, password: "E2e-pass-123", phoneNumber: `09${Date.now().toString().slice(-8)}` });
  expect(response.ok()).toBeTruthy();
  return { api, token: (await response.json()).accessToken as string };
}

function tomorrowAt(hour: number) {
  const vn = new Date(Date.now() + 7 * 3600_000);
  const day = new Date(Date.UTC(vn.getUTCFullYear(), vn.getUTCMonth(), vn.getUTCDate() + 1));
  return `${day.toISOString().slice(0, 10)}T${String(hour).padStart(2, "0")}:00:00+07:00`;
}

test("đặt bàn: tạo và hủy có thông báo ở tab Đặt bàn, số chưa đọc đúng, 'đọc tất cả' về 0; người khác không thấy", async ({ baseURL }) => {
  const guest = await newCustomer(baseURL!);
  const other = await newCustomer(baseURL!);
  const auth = { Authorization: `Bearer ${guest.token}` };
  const created = await guest.api.post("/api/mobile/v1/reservations", {
    headers: { ...auth, "Idempotency-Key": key() }, data: { branchId: 1, partySize: 2, reservedAt: tomorrowAt(19) } });
  expect(created.ok(), await created.text()).toBeTruthy();
  const reservation = await created.json();
  expect((await guest.api.post(`/api/mobile/v1/reservations/${reservation.reservationId}/cancel`, { headers: auth })).ok()).toBeTruthy();

  const box = await inbox(guest.api, guest.token);
  const mine = box.items.filter(x => x.category === "reservation" && x.refId === reservation.reservationId);
  expect(mine.map(x => x.title)).toEqual(["Đặt bàn đã hủy", "Đặt bàn thành công"]);     // newest first
  expect(mine[1].message).toContain(reservation.reservationCode);
  expect(box.unreadCount).toBe(2);
  expect(JSON.stringify(box)).not.toMatch(/\?\?|Kh\?ng|Response status code/);
  expect((await inbox(other.api, other.token)).items).toHaveLength(0);

  expect((await guest.api.post("/api/mobile/v1/notifications/read-all", { headers: auth })).ok()).toBeTruthy();
  const after = await inbox(guest.api, guest.token);
  expect(after.unreadCount).toBe(0);
  expect(after.items.every(x => x.isRead)).toBeTruthy();
});

test("SĐT liên hệ: dùng số khách nhập (+84, có dấu cách), báo đúng ô khi sai", async ({ baseURL }) => {
  const guest = await newCustomer(baseURL!);
  const auth = { Authorization: `Bearer ${guest.token}` };
  const bad = await guest.api.post("/api/mobile/v1/reservations", {
    headers: { ...auth, "Idempotency-Key": key() }, data: { branchId: 1, partySize: 2, reservedAt: tomorrowAt(12), contactPhone: "0343abc273" } });
  expect(bad.status()).toBe(400);
  const error = await bad.json();
  expect(error.code).toBe("contact_phone_invalid");
  expect(error.details.field).toBe("contactPhone");
  expect(error.message).toContain("Số điện thoại liên hệ");

  const good = await guest.api.post("/api/mobile/v1/reservations", {
    headers: { ...auth, "Idempotency-Key": key() },
    data: { branchId: 1, partySize: 2, reservedAt: tomorrowAt(12), contactName: "Chị Lan", contactPhone: "+84 343 000 273" } });
  expect(good.ok(), await good.text()).toBeTruthy();
  const reservation = await good.json();
  expect(reservation.phoneNumber).toBe("0343000273");
  expect(reservation.customerName).toBe("Chị Lan");
  await guest.api.post(`/api/mobile/v1/reservations/${reservation.reservationId}/cancel`, { headers: auth });
});

test("đơn hàng: bếp nhận đơn, món sẵn sàng, yêu cầu giục bếp được xem — tab Đơn hàng, mỗi việc một thông báo", async ({ baseURL }) => {
  const order: DiningFixture = await openTableWithOrder(baseURL!);
  const chef = await staff(baseURL!, "chef");
  expect((await chef.post(`/api/gateway/staff/chef/orders/${order.orderId}/start`, { data: {} })).ok()).toBeTruthy();
  expect((await chef.post(`/api/gateway/staff/chef/orders/${order.orderId}/start`, { data: {} })).status()).toBeLessThan(500);
  expect((await chef.post(`/api/gateway/staff/chef/orders/${order.orderId}/ready`, { data: {} })).ok()).toBeTruthy();
  const requestId = await sendKitchenRequest(order, "KITCHEN_RUSH", order.dishName);
  expect((await chef.post(`/api/gateway/staff/chef/kitchen-requests/${requestId}/seen`, { data: {} })).ok()).toBeTruthy();

  await expect.poll(async () => (await inbox(order.api, order.token)).items.filter(x => x.category === "order").map(x => x.title).sort(),
    { timeout: 45_000 }).toEqual(["Bếp đã nhận đơn", "Món ăn đã sẵn sàng", "Yêu cầu đã xong"].sort());
  const box = await inbox(order.api, order.token);
  expect(box.items.find(x => x.title === "Bếp đã nhận đơn")!.message).toContain(`#${order.orderId}`);
  expect(box.items.find(x => x.title === "Yêu cầu đã xong")!.message).toContain("giục bếp");
  await chef.dispose();
});

test("thanh toán: thu ngân hủy giao dịch chờ có thông báo ở tab Thanh toán", async ({ baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  const paymentId = await requestOnsitePayment(order, "TRANSFER");
  const cashier = await staff(baseURL!, "cashier");
  expect((await cashier.post(`/api/gateway/staff/cashier/onsite-payments/${paymentId}/cancel`, { data: {} })).ok()).toBeTruthy();
  const box = await inbox(order.api, order.token);
  const cancelled = box.items.find(x => x.category === "payment" && x.refId === paymentId);
  expect(cancelled?.title).toBe("Giao dịch đã hủy");
  expect(cancelled?.message).toContain("chọn lại cách thanh toán");
  await cashier.dispose();
});

test("ưu đãi: trả tiền xong được cộng điểm, thông báo ở tab Ưu đãi", async ({ baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  const paymentId = await requestOnsitePayment(order, "CASH", "CASHIER");
  const cashier = await staff(baseURL!, "cashier");
  expect((await cashier.post(`/api/gateway/staff/cashier/onsite-payments/${paymentId}/confirm`, { data: {} })).ok()).toBeTruthy();
  const expected = Math.floor(order.total * 0.01);
  test.skip(expected === 0, "order below 100đ earns no points");
  await expect.poll(async () => (await inbox(order.api, order.token)).items.filter(x => x.category === "offer").map(x => x.title),
    { timeout: 30_000 }).toContain("Cộng điểm thưởng");
  const box = await inbox(order.api, order.token);
  expect(box.items.find(x => x.title === "Cộng điểm thưởng")!.message).toContain(`${expected.toLocaleString("vi-VN")} điểm`);
  expect(box.items.find(x => x.category === "payment")?.title).toBe("Thanh toán thành công");
  await cashier.dispose();
});
