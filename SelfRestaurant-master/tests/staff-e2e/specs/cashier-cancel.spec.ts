import { createHmac, randomUUID } from "node:crypto";
import { expect, request as playwrightRequest, test, type APIRequestContext } from "@playwright/test";
import { appTransferQr, openTableWithOrder, requestOnsitePayment, sessionStatus, type DiningFixture } from "../support/customer";
import { settleAfterTest, settleTestTables } from "../support/cleanup";
import { accounts, login, password } from "../support/staff";

// Cashier cancels a waiting app payment (owner's request 25/10): the guest pays cash instead of the transfer.
// Billing cancels the payment and unlocks the session in Orders for exactly that quote, in one guarded step, so a
// new request can be made and the session is never stuck at PAYMENT_REQUESTED. LOCAL docker stack only.
test.afterEach(async ({ baseURL }) => { await settleAfterTest(baseURL!); });

type Row = { paymentId: number; diningSessionId: number; status: string };

async function cashierContext(baseURL: string) {
  const cashier = await playwrightRequest.newContext({ baseURL, extraHTTPHeaders: { Origin: baseURL } });
  expect((await cashier.post("/api/gateway/staff/auth/login", { data: { username: accounts.cashier.username, password } })).ok()).toBeTruthy();
  return cashier;
}

const cancelPayment = (cashier: APIRequestContext, paymentId: number) =>
  cashier.post(`/api/gateway/staff/cashier/onsite-payments/${paymentId}/cancel`, { data: {} });

async function sessionPayments(cashier: APIRequestContext, order: DiningFixture) {
  const rows: Row[] = await (await cashier.get("/api/gateway/staff/cashier/onsite-payments")).json();
  return rows.filter(p => p.diningSessionId === order.sessionId).map(p => [p.paymentId, p.status]).sort((a, b) => +a[0] - +b[0]);
}

async function paymentStatus(order: DiningFixture, paymentId: number) {
  const body = await (await order.api.get(`/api/mobile/v1/payments/${paymentId}`, { headers: { Authorization: `Bearer ${order.token}` } })).json();
  return body.status as string;
}

test("thu ngân hủy chuyển khoản chờ: phiên mở khóa, khách trả tiền mặt, 1 hóa đơn, phiên đóng", async ({ baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  const auth = { Authorization: `Bearer ${order.token}` };
  const transferId = await requestOnsitePayment(order, "TRANSFER");
  expect(await sessionStatus(order)).toBe("PAYMENT_REQUESTED");
  // While it waits, the guards of 9c76c94 still hold: no new quote.
  expect((await order.api.post(`/api/mobile/v1/dining-sessions/${order.sessionId}/checkout/quote`, {
    headers: { ...auth, "Idempotency-Key": randomUUID() }, data: {} })).status()).toBe(409);

  const cashier = await cashierContext(baseURL!);
  const cancel = await cancelPayment(cashier, transferId);
  expect(cancel.ok(), await cancel.text()).toBeTruthy();
  expect((await cancel.json()).status).toBe("CANCELLED");
  expect(await paymentStatus(order, transferId)).toBe("CANCELLED");
  expect(await sessionStatus(order)).toBe("ACTIVE");                        // not stuck at PAYMENT_REQUESTED
  expect((await order.api.get(`/api/mobile/v1/dining-sessions/${order.sessionId}/active-payment`, { headers: auth })).status()).toBe(404);

  // Guards re-checked after the cancel: nothing waiting or paid any more, so a new quote/request/payment is allowed.
  const cashId = await requestOnsitePayment(order, "CASH", "CASHIER");
  expect(cashId).not.toBe(transferId);
  expect(await sessionStatus(order)).toBe("PAYMENT_REQUESTED");
  const confirm = await cashier.post(`/api/gateway/staff/cashier/onsite-payments/${cashId}/confirm`, { data: {} });
  expect(confirm.ok(), await confirm.text()).toBeTruthy();
  await expect.poll(() => sessionStatus(order), { timeout: 30_000 }).toBe("CLOSED");
  expect(await sessionPayments(cashier, order)).toEqual([[transferId, "CANCELLED"], [cashId, "SUCCEEDED"]]);
  const invoice = await order.api.get(`/api/mobile/v1/dining-sessions/${order.sessionId}/invoice`, { headers: auth });
  expect(invoice.ok()).toBeTruthy();
  const issued = await invoice.json();
  expect(issued.paymentId).toBe(cashId);
  expect(issued.totalAmount).toBe(order.total);

  // The cancelled and the paid payment can no longer be cancelled or confirmed; messages stay Vietnamese.
  const late = await cancelPayment(cashier, cashId);
  expect(late.status()).toBe(409);
  expect((await late.json()).message).toMatch(/đã được thanh toán/);
  const again = await cashier.post(`/api/gateway/staff/cashier/onsite-payments/${transferId}/confirm`, { data: {} });
  expect(again.ok()).toBeFalsy();
  expect(JSON.stringify(await again.json())).not.toMatch(/Response status code|entity changes|inner exception/i);
  await cashier.dispose();
});

test("hủy 2 lần liên tiếp và 2 tab cùng hủy: chỉ 1 lần hủy, dữ liệu không lệch, sau đó vẫn tạo được 1 yêu cầu mới", async ({ baseURL }) => {
  // Sequential double click.
  const first = await openTableWithOrder(baseURL!);
  const firstId = await requestOnsitePayment(first, "TRANSFER");
  const cashier = await cashierContext(baseURL!);
  expect((await cancelPayment(cashier, firstId)).ok()).toBeTruthy();
  const repeat = await cancelPayment(cashier, firstId);
  expect(repeat.ok(), await repeat.text()).toBeTruthy();                   // a repeated cancel is a no-op
  expect(await sessionPayments(cashier, first)).toEqual([[firstId, "CANCELLED"]]);
  expect(await sessionStatus(first)).toBe("ACTIVE");

  // Two tabs (two cashier logins) at the same moment.
  const second = await openTableWithOrder(baseURL!);
  const secondId = await requestOnsitePayment(second, "TRANSFER");
  const otherTab = await cashierContext(baseURL!);
  const [a, b] = await Promise.all([cancelPayment(cashier, secondId), cancelPayment(otherTab, secondId)]);
  expect([a.status(), b.status()], `${await a.text()} | ${await b.text()}`).toEqual([200, 200]);
  expect(await sessionPayments(cashier, second)).toEqual([[secondId, "CANCELLED"]]);
  expect(await sessionStatus(second)).toBe("ACTIVE");
  // Exactly one new payment can follow, and it settles normally.
  const cashId = await requestOnsitePayment(second, "CASH", "CASHIER");
  expect((await cashier.post(`/api/gateway/staff/cashier/onsite-payments/${cashId}/confirm`, { data: {} })).ok()).toBeTruthy();
  await expect.poll(() => sessionStatus(second), { timeout: 30_000 }).toBe("CLOSED");
  expect(await sessionPayments(cashier, second)).toEqual([[secondId, "CANCELLED"], [cashId, "SUCCEEDED"]]);
  await Promise.all([cashier.dispose(), otherTab.dispose()]);
});

test("web thu ngân: nút 'Hủy giao dịch chờ' hỏi lại 'CHƯA chuyển khoản', bấm Hủy bỏ thì không đổi gì", async ({ page, baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  const paymentId = await requestOnsitePayment(order, "TRANSFER");
  await login(page, "cashier");
  const row = page.locator(`tr[data-payment-id="${paymentId}"]`);
  await expect(row).toContainText("Chờ thu tiền");

  let message = "";
  page.once("dialog", d => { message = d.message(); void d.dismiss(); });
  await row.getByRole("button", { name: "Hủy giao dịch chờ" }).click();
  await expect.poll(() => message).toMatch(/Chỉ hủy khi chắc chắn khách CHƯA chuyển khoản/);
  expect(await sessionStatus(order)).toBe("PAYMENT_REQUESTED");
  await expect(row).toContainText("Chờ thu tiền");

  page.once("dialog", d => void d.accept());
  await row.getByRole("button", { name: "Hủy giao dịch chờ" }).click();
  await expect(row).toContainText("Đã hủy");
  await expect(row.getByRole("button")).toHaveCount(0);
  expect(await sessionStatus(order)).toBe("ACTIVE");
});

// ---- Against SePay: needs the local throwaway secret (see sepay.spec.ts) ----

const secret = process.env.SEPAY_E2E_SECRET;
const account = process.env.SEPAY_E2E_ACCOUNT ?? "LOCALTEST01";
let nextId = Date.now() * 10;

async function webhook(baseURL: string, content: string, amount: number) {
  const id = ++nextId;
  const body = JSON.stringify({ id, gateway: "BIDV", transactionDate: "2026-10-25 17:00:00", accountNumber: account,
    subAccount: null, code: null, content, transferType: "in", transferAmount: amount, accumulated: 0, referenceCode: `FT${id}` });
  const timestamp = String(Math.floor(Date.now() / 1000));
  const signature = "sha256=" + createHmac("sha256", secret!).update(`${timestamp}.${body}`).digest("hex");
  const response = await fetch(`${baseURL}/api/payments/sepay/webhook`, { method: "POST", body,
    headers: { "Content-Type": "application/json", "X-SePay-Timestamp": timestamp, "X-SePay-Signature": signature } });
  return response.status;
}

test("tiền về SAU khi đã hủy: không tự xác nhận, thu ngân thấy cảnh báo, phiên vẫn mở", async ({ page, baseURL }) => {
  test.skip(!secret, "SePay e2e needs SEPAY_E2E_SECRET (local throwaway secret)");
  const order = await openTableWithOrder(baseURL!);
  const paymentId = await requestOnsitePayment(order, "TRANSFER");
  const content = (await appTransferQr(order, paymentId)).content as string;
  const cashier = await cashierContext(baseURL!);
  expect((await cancelPayment(cashier, paymentId)).ok()).toBeTruthy();

  expect(await webhook(baseURL!, content, order.total)).toBe(200);
  expect(await paymentStatus(order, paymentId)).toBe("CANCELLED");
  expect(await sessionStatus(order)).toBe("ACTIVE");
  await login(page, "cashier");
  const alert = page.locator("#bank-transfer-alerts tr", { hasText: `Giao dịch #${paymentId}` });
  await expect(alert).toContainText("Đơn đã thanh toán hoặc đã hủy");
  await cashier.dispose();
});

test("hủy đúng lúc tiền về (lặp 8 lần, lệch giờ khác nhau): chỉ 1 trong 2 thắng, không lệch dữ liệu", async ({ baseURL }) => {
  test.skip(!secret, "SePay e2e needs SEPAY_E2E_SECRET (local throwaway secret)");
  test.setTimeout(240_000);
  const cashier = await cashierContext(baseURL!);
  const outcomes: string[] = [];
  // The cancel starts 0–400 ms after the money, so both orders (cancel first / money first) are exercised.
  const cancelDelaysMs = [0, 0, 15, 40, 80, 150, 250, 400];
  for (const delay of cancelDelaysMs) {
    const order = await openTableWithOrder(baseURL!);
    const paymentId = await requestOnsitePayment(order, "TRANSFER");
    const content = (await appTransferQr(order, paymentId)).content as string;

    const [cancel, hook] = await Promise.all([
      new Promise(r => setTimeout(r, delay)).then(() => cancelPayment(cashier, paymentId)),
      webhook(baseURL!, content, order.total)]);
    expect(hook).toBe(200);
    const status = await paymentStatus(order, paymentId);
    if (status === "SUCCEEDED") {
      // Money won: the cancel was refused and the session closes with exactly one paid payment.
      expect(cancel.status(), await cancel.text()).toBe(409);
      expect((await cancel.json()).code).toBe("payment_already_succeeded");
      await expect.poll(() => sessionStatus(order), { timeout: 30_000 }).toBe("CLOSED");
    } else {
      // Cancel won: never confirmed, session unlocked for a new payment, nothing paid.
      expect(status).toBe("CANCELLED");
      expect(cancel.ok(), await cancel.text()).toBeTruthy();
      expect(await sessionStatus(order)).toBe("ACTIVE");
    }
    expect(await sessionPayments(cashier, order)).toEqual([[paymentId, status]]);
    outcomes.push(`${delay}ms:${status}`);
    await settleTestTables(baseURL!, false);   // only 10 test tables: give this one back before the next round
  }
  console.log(`cancel vs webhook outcomes: ${outcomes.join(", ")}`);
  await cashier.dispose();
});
