import { expect, test, type Page } from "@playwright/test";
import { openTableWithOrder, sendKitchenRequest } from "../support/customer";
import { expectCleanScreen } from "../support/screen";
import { branchChangePushed, countRequests, reloadNow, staffRealtimeJoined } from "../support/realtime";
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
  await expectCleanScreen(page);

  await pending.getByRole("button", { name: "BẮT ĐẦU NẤU" }).click();
  const preparing = card(page, "Đang chế biến", order.orderId);
  await expect(preparing).toBeVisible();
  await expect(pending).toHaveCount(0);

  await preparing.getByRole("button", { name: "Hoàn thành" }).last().click();
  await expect(card(page, "Sẵn sàng", order.orderId)).toBeVisible();
  await expectCleanScreen(page);
  await expect(preparing).toHaveCount(0);
});

test("bếp mở được trang Lịch sử & Tài khoản", async ({ page }) => {
  await login(page, "chef");
  await page.getByRole("link", { name: /Lịch sử & Tài khoản/ }).click();
  await expect(page).toHaveURL(/\/Staff\/Chef\/History/);
  await expect(page.getByRole("heading", { name: "Lịch sử đơn hàng bếp" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Đổi mật khẩu" })).toBeVisible();
  await expectCleanScreen(page);
});

test("bếp: tab Thực đơn không có khung lỗi hay chữ lạ", async ({ page }) => {
  await login(page, "chef");
  await page.getByRole("tab", { name: /Thực Đơn/ }).click();
  await expect(page.getByRole("heading", { name: "Thực đơn hôm nay" })).toBeVisible();
  await expectCleanScreen(page);
});

// Production bug 14/10/2026: an error response with an empty body showed the word "null" in the error box.
test("lỗi máy chủ không có nội dung: khung lỗi hiện câu tiếng Việt, không hiện 'null'", async ({ page }) => {
  await login(page, "chef");
  await page.goto("/app/chef/Staff/Chef/History");
  await expect(page.getByRole("heading", { name: "Lịch sử đơn hàng bếp" })).toBeVisible();
  await page.route("**/api/gateway/staff/chef/dashboard", (route) => route.fulfill({ status: 500, body: "" }));
  await reloadNow(page);
  const box = page.locator(".error-box");
  await expect(box).toBeVisible({ timeout: 15_000 });
  await expect(box).toHaveText("Máy chủ đang gặp sự cố (mã 500). Vui lòng thử lại sau ít phút.");
  await page.unroute("**/api/gateway/staff/chef/dashboard");
  await reloadNow(page);
  await expect(box).toHaveCount(0, { timeout: 25_000 });
  await expectCleanScreen(page);
});

test("đơn mới hiện ở bếp ngay nhờ realtime, không chờ hỏi định kỳ 60 giây", async ({ page, baseURL }) => {
  const joined = staffRealtimeJoined(page);
  await login(page, "chef");
  const socket = await joined;
  const pushed = branchChangePushed(socket);
  const order = await openTableWithOrder(baseURL!);
  const createdAt = Date.now();
  await pushed;
  await expect(card(page, "Chờ chế biến", order.orderId)).toBeVisible({ timeout: 10_000 });
  // Far sooner than the 60 s fallback poll: the order came by the realtime push.
  expect(Date.now() - createdAt).toBeLessThan(10_000);
  await expectCleanScreen(page);
});

test("bếp: không thao tác 15 phút thì dừng hỏi và ngắt realtime; bấm thanh để tiếp tục", async ({ page }) => {
  await page.clock.install();
  const joined = staffRealtimeJoined(page);
  await login(page, "chef");
  const socket = await joined;
  const bar = page.getByRole("button", { name: "Tạm dừng cập nhật để tiết kiệm. Bấm để tiếp tục" });
  await page.clock.fastForward("14:00");
  await expect(bar).toHaveCount(0);

  await page.clock.fastForward("01:01");
  await expect(bar).toBeVisible();
  await expect.poll(() => socket.isClosed()).toBe(true);
  const reads = countRequests(page, "/api/gateway/staff/chef/dashboard");
  await page.clock.fastForward("05:00");
  await page.waitForTimeout(1_000);
  expect(reads(), "no polling while paused").toBe(0);

  const rejoined = staffRealtimeJoined(page);
  const reload = page.waitForRequest(r => r.url().includes("/api/gateway/staff/chef/dashboard"));
  await bar.click();
  await reload;
  await rejoined;
  await expect(bar).toHaveCount(0);
  await expectCleanScreen(page);
});

// Orders service allows cancelling a READY dish/order, so the "Sẵn sàng" column keeps the buttons behind a warning dialog.
test("cột Sẵn sàng: hủy món hoặc đơn đã nấu xong phải qua hộp xác nhận có cảnh báo", async ({ page, baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  await login(page, "chef");
  const pending = card(page, "Chờ chế biến", order.orderId);
  await expect(pending).toBeVisible({ timeout: 30_000 });
  await pending.getByRole("button", { name: "BẮT ĐẦU NẤU" }).click();
  await card(page, "Đang chế biến", order.orderId).getByRole("button", { name: "Hoàn thành" }).last().click();
  const ready = card(page, "Sẵn sàng", order.orderId);
  await expect(ready).toBeVisible();

  await ready.getByRole("button", { name: "Hủy món" }).first().click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toContainText("Hủy món đã nấu xong?");
  await expect(dialog).toContainText("đã nấu xong và đang chờ phục vụ");
  await dialog.getByRole("button", { name: "Không hủy" }).click();
  await expect(dialog).toHaveCount(0);
  await expect(ready.getByText("Sẵn sàng", { exact: true }).first()).toBeVisible();

  await ready.getByRole("button", { name: "HỦY", exact: true }).click();
  await expect(page.getByText("Đơn này đã nấu xong và đang chờ phục vụ")).toBeVisible();
  await page.getByRole("button", { name: "Đóng" }).last().click();
  await expect(page.getByText("Đơn này đã nấu xong và đang chờ phục vụ")).toHaveCount(0);
  await expect(ready).toBeVisible();

  await ready.getByRole("button", { name: "HỦY", exact: true }).click();
  await page.getByPlaceholder("Ví dụ: Khách yêu cầu hủy").fill("e2e: hủy đơn đã sẵn sàng");
  await page.getByRole("button", { name: "Xác nhận hủy" }).click();
  await expect(ready).toHaveCount(0);
  await expectCleanScreen(page);
});

// Customers' "Giục bếp" / "Báo hủy món" reach the kitchen by realtime; "Đã xem" closes them and never cancels a dish.
test("yêu cầu giục bếp và xin hủy món từ app hiện ngay trên bảng bếp, bấm Đã xem để tắt", async ({ page, baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  const joined = staffRealtimeJoined(page);
  await login(page, "chef");
  const socket = await joined;
  const pending = card(page, "Chờ chế biến", order.orderId);
  await expect(pending).toBeVisible({ timeout: 30_000 });

  const pushed = branchChangePushed(socket);
  await sendKitchenRequest(order, "KITCHEN_RUSH", order.dishName);
  await pushed;
  const notices = page.getByRole("complementary", { name: "Yêu cầu từ khách" });
  const rush = notices.getByRole("status").filter({ hasText: "Khách giục món" });
  await expect(rush).toBeVisible({ timeout: 10_000 });
  await expect(rush).toContainText(`Bàn ${order.tableNumber}`);
  await expect(pending.locator(".kitchen-request-flag")).toContainText(`Khách giục món: ${order.dishName}`);

  await sendKitchenRequest(order, "KITCHEN_CANCEL", order.dishName);
  const cancel = notices.getByRole("status").filter({ hasText: "Khách xin hủy món" });
  await expect(cancel).toBeVisible({ timeout: 10_000 });

  await rush.getByRole("button", { name: "Đã xem" }).click();
  await expect(rush).toHaveCount(0);
  await pending.getByRole("button", { name: "Đã xem" }).click();
  await expect(notices).toHaveCount(0);
  await expect(pending.locator(".kitchen-request-flag")).toHaveCount(0);
  // The request did not cancel anything: the dish is still waiting to be cooked.
  await expect(pending).toContainText(order.dishName);
  await expect(pending).toContainText("Chờ chế biến");
  await expectCleanScreen(page);
});
