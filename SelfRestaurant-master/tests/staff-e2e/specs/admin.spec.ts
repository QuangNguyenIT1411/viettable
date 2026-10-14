import { expect, test, type Page } from "@playwright/test";
import { expectCleanScreen } from "../support/screen";
import { login } from "../support/staff";

// Read-only checks: nothing is created, edited or deleted.
// Sidebar items are buttons; their icon glyph leaks into the accessible name, so match the trailing label.
const escape = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
const openSection = async (page: Page, menu: string) => {
  await page.getByRole("button", { name: new RegExp(`(^|\\W)${escape(menu)}$`) }).first().click();
};

test.beforeEach(async ({ page }) => {
  // QR images come from an external generator; keep the suite offline and deterministic.
  await page.route("https://api.qrserver.com/**", (route) =>
    route.fulfill({ status: 200, contentType: "image/svg+xml", body: "<svg xmlns='http://www.w3.org/2000/svg' width='140' height='140'/>" }));
  await login(page, "admin");
});

test("tổng quan quản trị hiển thị", async ({ page }) => {
  await expect(page.getByRole("heading", { name: "Tổng quan quản trị" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Tổng quan hoạt động" })).toBeVisible();
  await expectCleanScreen(page);
});

test("bàn & QR: danh sách bàn và mã QR trỏ tới /Menu/FromQr", async ({ page }) => {
  await openSection(page, "Bàn & QR");
  await expect(page).toHaveURL(/\/Admin\/TablesQR\/Index/);
  await expect(page.getByRole("heading", { name: "Danh sách bàn ăn" })).toBeVisible();
  const row = page.getByRole("row").filter({ hasText: "QR-Q1-B01" });
  await expect(row).toContainText("Bàn 1");
  await expectCleanScreen(page);

  await page.goto("/app/admin/Admin/TablesQR/QR");
  await expect(page.getByRole("heading", { name: "Danh sách mã QR bàn" })).toBeVisible();
  const card = page.locator("article").filter({ has: page.getByRole("heading", { name: "Bàn 1", exact: true }) }).first();
  await expect(card).toContainText("/Menu/FromQr?code=QR-Q1-B01");
  await expect(card.getByRole("img", { name: "QR bàn 1" })).toHaveAttribute("src", /Menu%2FFromQr%3Fcode%3DQR-Q1-B01/);
  await expectCleanScreen(page);
});

test("món ăn: danh sách món có dữ liệu", async ({ page }) => {
  await openSection(page, "Món ăn");
  await expect(page).toHaveURL(/\/Admin\/Dishes\/Index/);
  await expect(page.getByRole("heading", { name: "Danh sách món ăn" })).toBeVisible();
  // The list is paginated; any dish row carries the edit/suspend actions.
  const dishRow = page.getByRole("row").filter({ has: page.getByRole("button", { name: /Sửa$/ }) }).first();
  await expect(dishRow).toBeVisible();
  await expect(dishRow.getByRole("button", { name: /Tạm ngưng$|Mở bán$|Kích hoạt$/ })).toBeVisible();
  await expectCleanScreen(page);
});

test("nhân viên: danh sách và nhật ký của một nhân viên", async ({ page }) => {
  await openSection(page, "Nhân viên");
  await expect(page).toHaveURL(/\/Admin\/Employees\/Index/);
  await expect(page.getByRole("heading", { name: "Quản lý nhân viên" })).toBeVisible();
  const row = page.getByRole("row").filter({ hasText: "chef_hung" }).first();
  await expect(row).toBeVisible();
  await expectCleanScreen(page);
  await row.getByRole("button", { name: "Nhật ký" }).or(row.getByRole("link", { name: "Nhật ký" })).first().click();
  await expect(page).toHaveURL(/\/Admin\/Employees\/History\/\d+/);
  await expectCleanScreen(page);
});

test("báo cáo: doanh thu và top món", async ({ page }) => {
  await openSection(page, "Báo cáo");
  await expect(page).toHaveURL(/\/Admin\/Reports\/Revenue/);
  await expect(page.getByRole("heading", { name: "Báo cáo doanh thu" }).first()).toBeVisible();
  await expectCleanScreen(page);
  await page.getByRole("button", { name: "Top món ăn" }).click();
  await expect(page).toHaveURL(/\/Admin\/Reports\/TopDishes/);
  await expect(page.getByRole("heading", { name: "Món ăn gọi nhiều" })).toBeVisible();
  await expectCleanScreen(page);
});

test("các trang quản trị còn lại mở được, không có khung lỗi hay chữ lạ", async ({ page }) => {
  for (const path of [
    "Admin/Categories/Index", "Admin/Categories/Statuses", "Admin/Units/Index", "Admin/Ingredients/Index",
    "Admin/Inventory/Index", "Admin/Inventory/Batches", "Admin/Inventory/Movements", "Admin/Dishes/Ingredients",
    "Admin/TablesQR/Edit", "Admin/Customers/Index", "Admin/Settings/Index",
  ]) {
    await page.goto(`/app/admin/${path}`);
    await expect(page.locator(".admin-page-header h1")).toBeVisible();
    await expectCleanScreen(page);
  }
});
