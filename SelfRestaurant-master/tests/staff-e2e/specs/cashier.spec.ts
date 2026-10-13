import { expect, test, type Page } from "@playwright/test";
import { openTableWithOrder, requestOnsiteCashPayment, sessionStatus } from "../support/customer";
import { login } from "../support/staff";

const tableCard = (page: Page, n: number) =>
  page.locator("article").filter({ hasText: new RegExp(`^\s*Bàn ${n}(?!\d)`) }).first();

test("thu ngân thấy bàn có khách trên sơ đồ bàn", async ({ page, baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  await login(page, "cashier");
  await expect(page.getByRole("heading", { name: "Chọn bàn" })).toBeVisible();
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
});

// KNOWN GAP (13/10/2026, not a UI issue): app-session payments create Billing Invoices, but this page lists
// legacy Bills only (CashierBillsController -> _db.Bills), so they never appear. Remove fixme once fixed.
test.fixme("hóa đơn của phiên app hiện trong Lịch sử hóa đơn của thu ngân", async ({ page, baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  await login(page, "cashier");
  await tableCard(page, order.tableNumber).click();
  await page.getByRole("button", { name: /Tiền mặt/ }).click();
  await page.getByRole("button", { name: "Vừa đủ" }).click();
  await page.getByRole("button", { name: "Xác nhận thanh toán" }).click();
  await expect(page.getByText("Thanh toán thành công").first()).toBeVisible();
  await page.goto("/app/cashier/Staff/Cashier/History");
  await expect(page.getByRole("cell", { name: `Đơn #${order.orderId}` })).toBeVisible();
});

test("báo cáo doanh thu theo ngày mở và tải được", async ({ page }) => {
  await login(page, "cashier");
  await page.goto("/app/cashier/Staff/Cashier/Report");
  await expect(page.getByRole("heading", { name: "Báo cáo doanh thu theo ngày" })).toBeVisible();
  await page.getByRole("button", { name: "Xem báo cáo" }).click();
  await expect(page.getByRole("heading", { name: "Tổng quan" })).toBeVisible();
});
