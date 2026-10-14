import { expect, test, type Page } from "@playwright/test";
import { openTableWithOrder } from "../support/customer";
import { login } from "../support/staff";

// Kanban columns are <section>s titled "Chờ chế biến" / "Đang chế biến" / "Sẵn sàng"; orders are <article> cards.
const column = (page: Page, title: string) =>
  page.locator("section").filter({ has: page.getByText(title, { exact: true }) }).last();
const card = (page: Page, title: string, orderId: number) =>
  column(page, title).locator("article").filter({ hasText: `Đơn #${orderId}` });

test("bếp nhận đơn mới, bắt đầu nấu và báo hoàn thành", async ({ page, baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  await login(page, "chef");

  const pending = card(page, "Chờ chế biến", order.orderId);
  await expect(pending).toBeVisible({ timeout: 30_000 });
  await expect(pending).toContainText(order.dishName);
  await expect(pending).toContainText(`Bàn ${order.tableNumber}`);

  await pending.getByRole("button", { name: "BẮT ĐẦU NẤU" }).click();
  const preparing = card(page, "Đang chế biến", order.orderId);
  await expect(preparing).toBeVisible();
  await expect(pending).toHaveCount(0);

  await preparing.getByRole("button", { name: "Hoàn thành" }).last().click();
  await expect(card(page, "Sẵn sàng", order.orderId)).toBeVisible();
  await expect(preparing).toHaveCount(0);
});

test("bếp mở được trang Lịch sử & Tài khoản", async ({ page }) => {
  await login(page, "chef");
  await page.getByRole("link", { name: /Lịch sử & Tài khoản/ }).click();
  await expect(page).toHaveURL(/\/Staff\/Chef\/History/);
  await expect(page.getByRole("heading", { name: "Lịch sử đơn hàng bếp" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Đổi mật khẩu" })).toBeVisible();
});
