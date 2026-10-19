import { createHmac } from "node:crypto";
import { expect, test } from "@playwright/test";
import { appTransferQr, openTableWithOrder, requestOnsitePayment, sessionStatus } from "../support/customer";
import { settleAfterTest } from "../support/cleanup";

// SePay auto-confirmation against the LOCAL docker stack only. Runs only when the local billing container was
// started with a throwaway secret, e.g. SEPAY_ENABLED=true SEPAY_WEBHOOK_SECRET=<local> SEPAY_ACCOUNT_NUMBERS=LOCALTEST01,
// and the same values are given to this test as SEPAY_E2E_SECRET / SEPAY_E2E_ACCOUNT.
const secret = process.env.SEPAY_E2E_SECRET;
const account = process.env.SEPAY_E2E_ACCOUNT ?? "LOCALTEST01";
test.skip(!secret, "SePay e2e needs SEPAY_E2E_SECRET (local throwaway secret)");
test.afterEach(async ({ baseURL }) => { await settleAfterTest(baseURL!); });

let nextId = Date.now();

async function webhook(baseURL: string, content: string, amount: number, id = ++nextId) {
  const body = JSON.stringify({ id, gateway: "BIDV", transactionDate: "2026-10-19 21:00:00", accountNumber: account,
    subAccount: null, code: null, content, transferType: "in", transferAmount: amount, accumulated: 0, referenceCode: `FT${id}` });
  const timestamp = String(Math.floor(Date.now() / 1000));
  const signature = "sha256=" + createHmac("sha256", secret!).update(`${timestamp}.${body}`).digest("hex");
  const response = await fetch(`${baseURL}/api/payments/sepay/webhook`, { method: "POST", body,
    headers: { "Content-Type": "application/json", "X-SePay-Timestamp": timestamp, "X-SePay-Signature": signature } });
  return { status: response.status, id };
}

test("chuyển khoản đủ tiền đúng mã: tự xác nhận, phiên đóng, gửi trùng không xử lý lại", async ({ baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  const paymentId = await requestOnsitePayment(order, "TRANSFER");
  const content = (await appTransferQr(order, paymentId)).content as string;

  // Not enough money: never confirmed (left for the cashier).
  expect((await webhook(baseURL!, content, order.total - 1000)).status).toBe(200);
  expect(await sessionStatus(order)).not.toBe("CLOSED");

  const paid = await webhook(baseURL!, `IBFT ${content} CT tu 0123`, order.total);
  expect(paid.status).toBe(200);
  await expect.poll(() => sessionStatus(order), { timeout: 30_000 }).toBe("CLOSED");
  const payment = await (await order.api.get(`/api/mobile/v1/payments/${paymentId}`, {
    headers: { Authorization: `Bearer ${order.token}` } })).json();
  expect(payment.status).toBe("SUCCEEDED");

  // SePay retries the same transaction: accepted, nothing happens twice.
  expect((await webhook(baseURL!, content, order.total, paid.id)).status).toBe(200);
});

test("chữ ký sai: 401, không xác nhận gì", async ({ baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  const paymentId = await requestOnsitePayment(order, "TRANSFER");
  const body = JSON.stringify({ id: ++nextId, accountNumber: account, content: `VT B1 TT${paymentId}`, transferType: "in",
    transferAmount: order.total });
  const response = await fetch(`${baseURL}/api/payments/sepay/webhook`, { method: "POST", body,
    headers: { "Content-Type": "application/json", "X-SePay-Timestamp": String(Math.floor(Date.now() / 1000)),
      "X-SePay-Signature": "sha256=" + "0".repeat(64) } });
  expect(response.status).toBe(401);
  expect(await sessionStatus(order)).not.toBe("CLOSED");
});
