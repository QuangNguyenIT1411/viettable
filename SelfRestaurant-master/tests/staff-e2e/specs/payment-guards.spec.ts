import { expect, request as playwrightRequest, test } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { openTableWithOrder, requestOnsitePayment, sessionStatus } from "../support/customer";
import { settleAfterTest } from "../support/cleanup";
import { accounts, password } from "../support/staff";

// Incident 25/10/2026 (phiên #34, TT28): while a transfer was waiting, "Back" unlocked the session through Orders,
// the app opened new quotes and a second payment request, and the cashier created a cash payment on top. When the
// transfer arrived, the session could not be closed and the cash payment stayed "Chờ thu tiền".
test.afterEach(async ({ baseURL }) => { await settleAfterTest(baseURL!); });

test("đang có giao dịch chuyển khoản chờ: không mở khóa phiên, không báo giá/yêu cầu/giao dịch mới, thu ngân không thu thêm", async ({ baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  const auth = { Authorization: `Bearer ${order.token}` };
  const paymentId = await requestOnsitePayment(order, "TRANSFER");                       // 1. TT…, quote locked
  expect(await sessionStatus(order)).toBe("PAYMENT_REQUESTED");

  // 2. "Back" in the app: refused, the session stays locked for the waiting transfer.
  const cancel = await order.api.post(`/api/mobile/v1/dining-sessions/${order.sessionId}/cancel-payment-request`, { headers: auth, data: {} });
  expect(cancel.status()).toBe(409);
  expect((await cancel.json()).code).toBe("payment_in_progress");
  expect(await sessionStatus(order)).toBe("PAYMENT_REQUESTED");

  // 3. A new quote (what created quotes 36–40) is refused with a Vietnamese message.
  const quote = await order.api.post(`/api/mobile/v1/dining-sessions/${order.sessionId}/checkout/quote`, {
    headers: { ...auth, "Idempotency-Key": randomUUID() }, data: {} });
  expect(quote.status()).toBe(409);
  const quoteBody = await quote.json();
  expect(quoteBody.code).toBe("payment_in_progress");
  expect(quoteBody.message).toMatch(/giao dịch chờ xác nhận/);

  // 4. The app finds its waiting payment and returns to that screen.
  const active = await order.api.get(`/api/mobile/v1/dining-sessions/${order.sessionId}/active-payment`, { headers: auth });
  expect(active.ok()).toBeTruthy();
  expect((await active.json()).payment.paymentId).toBe(paymentId);

  // 5. The cashier's "Thanh toán" on the same order settles THAT waiting payment (the cashier checked the money):
  //    never a second payment, and the session closes.
  const cashier = await playwrightRequest.newContext({ baseURL, extraHTTPHeaders: { Origin: baseURL! } });
  expect((await cashier.post("/api/gateway/staff/auth/login", { data: { username: accounts.cashier.username, password } })).ok()).toBeTruthy();
  const checkout = await cashier.post(`/api/gateway/staff/cashier/orders/${order.orderId}/checkout`, {
    data: { paymentMethod: "CASH", paymentAmount: order.total, discount: 0, pointsUsed: 0, idempotencyKey: randomUUID() } });
  expect(checkout.ok(), await checkout.text()).toBeTruthy();
  const payments = await (await cashier.get("/api/gateway/staff/cashier/onsite-payments")).json();
  const ofSession = payments.filter((p: { diningSessionId: number }) => p.diningSessionId === order.sessionId);
  expect(ofSession.map((p: { paymentId: number; status: string }) => [p.paymentId, p.status])).toEqual([[paymentId, "SUCCEEDED"]]);
  await expect.poll(() => sessionStatus(order), { timeout: 30_000 }).toBe("CLOSED");

  // 6. A second checkout on the paid session is refused in Vietnamese, no technical English.
  const again = await cashier.post(`/api/gateway/staff/cashier/orders/${order.orderId}/checkout`, {
    data: { paymentMethod: "CASH", paymentAmount: order.total, discount: 0, pointsUsed: 0, idempotencyKey: randomUUID() } });
  expect(JSON.stringify(await again.json())).not.toMatch(/inner exception|Response status code|entity changes/i);
  const after = (await (await cashier.get("/api/gateway/staff/cashier/onsite-payments")).json())
    .filter((p: { diningSessionId: number }) => p.diningSessionId === order.sessionId);
  expect(after).toHaveLength(1);
  await cashier.dispose();
});

test("không có giao dịch chờ: khách vẫn hủy yêu cầu thanh toán để gọi thêm món", async ({ baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  const auth = { Authorization: `Bearer ${order.token}`, "Idempotency-Key": randomUUID() };
  const quote = await (await order.api.post(`/api/mobile/v1/dining-sessions/${order.sessionId}/checkout/quote`, { headers: auth, data: {} })).json();
  expect((await order.api.post(`/api/mobile/v1/dining-sessions/${order.sessionId}/payment-request`, {
    headers: { Authorization: `Bearer ${order.token}`, "Idempotency-Key": randomUUID() }, data: { checkoutQuoteId: quote.checkoutQuoteId } })).ok()).toBeTruthy();
  expect(await sessionStatus(order)).toBe("PAYMENT_REQUESTED");
  const cancel = await order.api.post(`/api/mobile/v1/dining-sessions/${order.sessionId}/cancel-payment-request`, {
    headers: { Authorization: `Bearer ${order.token}` }, data: {} });
  expect(cancel.ok()).toBeTruthy();
  expect(await sessionStatus(order)).toBe("ACTIVE");
});
