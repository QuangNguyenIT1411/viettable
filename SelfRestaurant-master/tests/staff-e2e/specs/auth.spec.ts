import { expect, test } from "@playwright/test";
import { expectCleanScreen } from "../support/screen";
import { accounts, expectOnLoginPage, login, password, type Role } from "../support/staff";

test.describe("Đăng nhập / đăng xuất nhân viên", () => {
  for (const role of Object.keys(accounts) as Role[]) {
    test(`${role}: đăng nhập vào đúng khu vực rồi đăng xuất`, async ({ page }) => {
      await login(page, role);
      await expectCleanScreen(page);
      const logout = page.getByRole("button", { name: "Đăng xuất" }).first();
      await logout.click();
      await expectOnLoginPage(page);
      await expectCleanScreen(page);
      // Session is gone: a protected page sends the user back to login.
      await page.goto(role === "chef" ? "/app/chef/Staff/Chef/Index" : role === "cashier" ? "/app/cashier/Staff/Cashier/Index" : "/app/admin/Admin/Dashboard/Index");
      await expectOnLoginPage(page);
    });
  }

  test("sai mật khẩu thì báo lỗi và không vào được", async ({ page }) => {
    await page.goto("/app/chef/Staff/Account/Login");
    await page.getByLabel("Tên đăng nhập").fill(accounts.chef.username);
    await page.getByLabel("Mật khẩu", { exact: true }).fill(password + "-sai");
    await page.getByRole("button", { name: /Đăng nhập/ }).click();
    await expect(page.locator(".error-box, [role=alert]").first()).toBeVisible();
    await expectCleanScreen(page, { allowErrorBox: true });
    await expectOnLoginPage(page);
  });
});
