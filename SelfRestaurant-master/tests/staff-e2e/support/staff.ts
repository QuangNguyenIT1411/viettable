import { expect, type Page } from "@playwright/test";

// Seeded LOCAL accounts (sql/sqlchuan.cleaned.sql, branch 1). Override with env vars if the local DB differs.
export const accounts = {
  chef: { username: process.env.E2E_CHEF_USER ?? "chef_hung", home: /\/app\/chef\/Staff\/Chef\/Index/ },
  cashier: { username: process.env.E2E_CASHIER_USER ?? "cashier_lan", home: /\/app\/cashier\/Staff\/Cashier\/Index/ },
  admin: { username: process.env.E2E_ADMIN_USER ?? "admin", home: /\/app\/admin\/Admin\/Dashboard\/Index/ },
} as const;
export const password = process.env.E2E_STAFF_PASSWORD ?? "123456";
export type Role = keyof typeof accounts;

/** All roles sign in through the shared staff login page and are routed by role. */
export async function login(page: Page, role: Role) {
  await page.goto("/app/chef/Staff/Account/Login");
  await page.getByLabel("Tên đăng nhập").fill(accounts[role].username);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(password);
  await page.getByRole("button", { name: /Đăng nhập/ }).click();
  await expect(page).toHaveURL(accounts[role].home);
}

export async function expectOnLoginPage(page: Page) {
  await expect(page).toHaveURL(/\/Staff\/Account\/Login/);
  await expect(page.getByLabel("Tên đăng nhập")).toBeVisible();
}
