import { expect, test, type Page } from "@playwright/test";
import { appPaymentOptions, appTransferQr, openTableWithOrder, requestOnsiteCashPayment, requestOnsitePayment, sessionStatus } from "../support/customer";
import { expectCleanScreen } from "../support/screen";
import { branchChangePushed, countRequests, staffRealtimeJoined } from "../support/realtime";
import { accounts, login, password } from "../support/staff";

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

// EMVCo/VietQR fields: 2-char id, 2-digit length, value.
function tlv(payload: string) {
  const fields: Record<string, string> = {};
  for (let i = 0; i < payload.length;) {
    const id = payload.slice(i, i + 2);
    const length = Number(payload.slice(i + 2, i + 4));
    fields[id] = payload.slice(i + 4, i + 4 + length);
    i += 4 + length;
  }
  return fields;
}

test("thanh toán chuyển khoản tại quầy: hiện mã VietQR đúng số tiền và nội dung, thu ngân bấm Đã nhận chuyển khoản", async ({ page, baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  await login(page, "cashier");
  await tableCard(page, order.tableNumber).click();
  await expect(page.getByText(`Đơn #${order.orderId}`).first()).toBeVisible();

  const qrResponse = page.waitForResponse((r) => r.url().includes("/api/gateway/staff/cashier/transfer-qr") && r.ok());
  await page.getByRole("button", { name: /Chuyển khoản/ }).click();
  const qr = await (await qrResponse).json();
  const image = page.getByRole("img", { name: /Mã VietQR chuyển khoản/ });
  await expect(image).toBeVisible();
  expect(await image.evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(100);
  await expect(page.getByText(`VT B1 DON${order.orderId}`)).toBeVisible();
  const fields = tlv(qr.payload);
  expect(fields["54"]).toBe(String(order.total));
  expect(tlv(fields["62"])["08"]).toBe(`VT B1 DON${order.orderId}`);
  await expectCleanScreen(page);

  await page.getByRole("button", { name: "Đã nhận chuyển khoản" }).click();
  await expect(page.getByText("Thanh toán thành công").first()).toBeVisible();
  await expect(page.getByText("Chuyển khoản").first()).toBeVisible();
  await expect.poll(() => sessionStatus(order), { timeout: 30_000 }).toBe("CLOSED");
});

test("khách chọn chuyển khoản trong app: app nhận mã QR, thu ngân hiện cùng mã và xác nhận đã nhận", async ({ page, baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  expect((await appPaymentOptions(order)).transferAvailable).toBe(true);
  const paymentId = await requestOnsitePayment(order, "TRANSFER");
  const appQr = await appTransferQr(order, paymentId);
  expect(appQr.content).toBe(`VT B1 TT${paymentId}`);
  expect(tlv(appQr.payload)["54"]).toBe(String(order.total));
  expect(appQr.qrPngBase64.length).toBeGreaterThan(100);

  await login(page, "cashier");
  const row = page.locator(`tr[data-payment-id="${paymentId}"]`);
  await expect(row).toBeVisible({ timeout: 30_000 });
  await expect(row).toContainText("Chuyển khoản");
  await row.getByRole("button", { name: "Hiện mã QR" }).click();
  const dialog = page.getByRole("dialog", { name: "Mã QR chuyển khoản" });
  await expect(dialog.getByRole("img", { name: /Mã VietQR chuyển khoản/ })).toBeVisible();
  await expect(dialog).toContainText(`VT B1 TT${paymentId}`);
  await dialog.getByRole("button", { name: "Đóng" }).click();

  page.once("dialog", (d) => void d.accept());
  await row.getByRole("button", { name: "Đã nhận chuyển khoản" }).click();
  await expect(row.getByRole("button", { name: "Đã nhận chuyển khoản" })).toHaveCount(0);
  await expect.poll(() => sessionStatus(order), { timeout: 30_000 }).toBe("CLOSED");
});

// Admin revenue = money actually collected, the same figure the cashier sees, split by method.
test("doanh thu Admin khớp doanh thu thu ngân hôm nay, có tách Tiền mặt / Chuyển khoản", async ({ page, browser, baseURL }) => {
  const order = await openTableWithOrder(baseURL!);
  const paymentId = await requestOnsitePayment(order, "TRANSFER");
  await login(page, "cashier");
  const row = page.locator(`tr[data-payment-id="${paymentId}"]`);
  await expect(row).toBeVisible({ timeout: 30_000 });
  page.once("dialog", (d) => void d.accept());
  await row.getByRole("button", { name: "Đã nhận chuyển khoản" }).click();
  await expect.poll(() => sessionStatus(order), { timeout: 30_000 }).toBe("CLOSED");

  const cashierDashboard = await (await page.request.get("/api/gateway/staff/cashier/dashboard")).json();
  const cashierReport = await (await page.request.get("/api/gateway/staff/cashier/report")).json();
  expect(cashierDashboard.todayRevenue).toBeGreaterThan(0);
  expect(cashierReport.transferRevenue).toBeGreaterThanOrEqual(order.total);
  expect(cashierReport.cashRevenue + cashierReport.transferRevenue + cashierReport.otherRevenue).toBe(cashierReport.totalRevenue);

  const adminContext = await browser.newContext({ baseURL });
  const admin = await adminContext.newPage();
  await admin.goto("/app/chef/Staff/Account/Login");
  await admin.getByLabel("Tên đăng nhập").fill(accounts.admin.username);
  await admin.getByLabel("Mật khẩu", { exact: true }).fill(password);
  await admin.getByRole("button", { name: /Đăng nhập/ }).click();
  await expect(admin).toHaveURL(accounts.admin.home);
  const reports = await (await admin.request.get("/api/gateway/admin/reports?revenueDays=1")).json();
  const adminDashboard = await (await admin.request.get("/api/gateway/admin/dashboard")).json();
  const today = reports.revenue.revenueByBranchDate.find((r: { branchId: number }) => r.branchId === 1);
  expect(today, "admin has a revenue row for branch 1 today").toBeTruthy();
  expect(today.totalRevenue).toBe(cashierDashboard.todayRevenue);
  expect(adminDashboard.stats.todayRevenue).toBe(cashierDashboard.todayRevenue);
  expect(today.cashRevenue + today.transferRevenue + today.otherRevenue).toBe(today.totalRevenue);
  expect(today.transferRevenue).toBeGreaterThanOrEqual(order.total);

  await admin.goto("/app/admin/Admin/Reports/Revenue");
  const revenueTable = admin.locator("table.data-table");
  await expect(revenueTable.locator("thead").getByText("Tiền mặt", { exact: true })).toBeVisible();
  await expect(revenueTable.locator("thead").getByText("Chuyển khoản", { exact: true })).toBeVisible();
  await adminContext.close();
});
