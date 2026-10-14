import { expect, test, type Page } from "@playwright/test";
import { openTableWithOrder, requestOnsiteCashPayment, sessionStatus } from "../support/customer";
import { expectCleanScreen } from "../support/screen";
import { branchChangePushed, countRequests, staffRealtimeJoined } from "../support/realtime";
import { login } from "../support/staff";

const tableCard = (page: Page, n: number) =>
  page.locator("article").filter({ hasText: new RegExp(`^\s*Bàn ${n}(?!\d)`) }).first();

test("thu ngân thấy bàn có khách trên sơ đồ bàn", async ({ page, baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  await login(page, "cashier");
  await expect(page.getByRole("heading", { name: "Chọn bàn" })).toBeVisible();
  await expectCleanScreen(page);
  await expect(tableCard(page, order.tableNumber)).toContainText("Có khách");
  await tableCard(page, order.tableNumber).click();
  await expect(page.getByText(`Đơn #${order.orderId}`).first()).toBeVisible();
});

test("thanh toán ứng dụng tại nhà hàng: thu ngân xác nhận đã thu tiền", async ({ page, baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  const paymentId = await requestOnsiteCashPayment(order);
  await login(page, "cashier");

  const row = page.locator(`tr[data-payment-id="${paymentId}"]`);
  await expect(row).toBeVisible({ timeout: 30_000 });
  await expectCleanScreen(page);
  page.once("dialog", (dialog) => void dialog.accept());
  await row.getByRole("button", { name: "Xác nhận đã thu tiền" }).click();
  await expect(row.getByRole("button", { name: "Xác nhận đã thu tiền" })).toHaveCount(0);
  await expect.poll(() => sessionStatus(order), { timeout: 30_000 }).toBe("CLOSED");
});

test("nút Thanh toán trên dashboard: tiền mặt và đóng phiên", async ({ page, baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  await login(page, "cashier");

  await tableCard(page, order.tableNumber).click();
  await expect(page.getByText(`Đơn #${order.orderId}`).first()).toBeVisible();
  await page.getByRole("button", { name: /Tiền mặt/ }).click();
  await page.getByRole("button", { name: "Vừa đủ" }).click();
  await page.getByRole("button", { name: "Xác nhận thanh toán" }).click();
  await expect(page.getByText("Thanh toán thành công").first()).toBeVisible();
  await expect(page.getByText(`Đơn #${order.orderId}`).first()).toBeVisible();
  await expect.poll(() => sessionStatus(order), { timeout: 30_000 }).toBe("CLOSED");
});

test("trang Lịch sử hóa đơn của thu ngân mở được", async ({ page }) => {
  await login(page, "cashier");
  await page.getByRole("link", { name: /Lịch sử & Tài khoản/ }).click();
  await expect(page).toHaveURL(/\/Staff\/Cashier\/History/);
  await expect(page.getByRole("heading", { name: "Lịch sử hóa đơn đã thanh toán" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Thông tin tài khoản" })).toBeVisible();
  await expectCleanScreen(page);
});

// App-session payments create Billing Invoices; history and the daily report list them next to legacy Bills.
test("hóa đơn của phiên app hiện trong Lịch sử hóa đơn và Báo cáo ngày của thu ngân", async ({ page, baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  await login(page, "cashier");
  await tableCard(page, order.tableNumber).click();
  await page.getByRole("button", { name: /Tiền mặt/ }).click();
  await page.getByRole("button", { name: "Vừa đủ" }).click();
  await page.getByRole("button", { name: "Xác nhận thanh toán" }).click();
  await expect(page.getByText("Thanh toán thành công").first()).toBeVisible();

  await page.goto("/app/cashier/Staff/Cashier/History");
  await expect(page.getByRole("cell", { name: `Đơn #${order.orderId}` })).toBeVisible();

  await page.goto("/app/cashier/Staff/Cashier/Report");
  await page.getByRole("button", { name: "Xem báo cáo" }).click();
  await expect(page.getByRole("cell", { name: `Đơn #${order.orderId}` })).toBeVisible();
});

test("báo cáo doanh thu theo ngày mở và tải được", async ({ page }) => {
  await login(page, "cashier");
  await page.goto("/app/cashier/Staff/Cashier/Report");
  await expect(page.getByRole("heading", { name: "Báo cáo doanh thu theo ngày" })).toBeVisible();
  await page.getByRole("button", { name: "Xem báo cáo" }).click();
  await expect(page.getByRole("heading", { name: "Tổng quan" })).toBeVisible();
  await expectCleanScreen(page);
});

test("giao dịch tại quầy mới hiện ngay ở thu ngân nhờ realtime, không chờ hỏi định kỳ", async ({ page, baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  const joined = staffRealtimeJoined(page);
  await login(page, "cashier");
  const socket = await joined;
  const pushed = branchChangePushed(socket);
  const paymentId = await requestOnsiteCashPayment(order);
  const createdAt = Date.now();
  await pushed;
  await expect(page.locator(`tr[data-payment-id="${paymentId}"]`)).toBeVisible({ timeout: 10_000 });
  expect(Date.now() - createdAt).toBeLessThan(10_000);
  await expectCleanScreen(page);
});

test("thu ngân: không thao tác 15 phút thì dừng hỏi và ngắt realtime; bấm thanh để tiếp tục", async ({ page }) => {
  await page.clock.install();
  const joined = staffRealtimeJoined(page);
  await login(page, "cashier");
  const socket = await joined;
  const bar = page.getByRole("button", { name: "Tạm dừng cập nhật để tiết kiệm. Bấm để tiếp tục" });
  await page.clock.fastForward("15:01");
  await expect(bar).toBeVisible();
  await expect.poll(() => socket.isClosed()).toBe(true);
  const reads = countRequests(page, "/onsite-payments", "/service-requests", "/cashier/dashboard");
  await page.clock.fastForward("05:00");
  await page.waitForTimeout(1_000);
  expect(reads(), "no polling while paused").toBe(0);

  const rejoined = staffRealtimeJoined(page);
  const reload = page.waitForRequest(r => r.url().includes("/onsite-payments"));
  await bar.click();
  await reload;
  await rejoined;
  await expect(bar).toHaveCount(0);
  await expectCleanScreen(page);
});
