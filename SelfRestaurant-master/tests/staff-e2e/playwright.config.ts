import { defineConfig } from "@playwright/test";

// Runs against the LOCAL docker stack only (gateway at http://localhost:5100 serving the built staff web).
// Never point STAFF_BASE_URL at Vercel: these tests create customers, sessions, orders and payments.
const baseURL = process.env.STAFF_BASE_URL ?? "http://localhost:5100";
if (!/^http:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/.test(baseURL)) {
  throw new Error(`Refusing to run staff e2e against non-local URL: ${baseURL}`);
}

export default defineConfig({
  testDir: "./specs",
  fullyParallel: false,
  workers: 1, // one shared local database and single-session staff accounts
  retries: 0,
  timeout: 90_000,
  expect: { timeout: 20_000 },
  reporter: [["list"], ["html", { open: "never", outputFolder: "playwright-report" }]],
  use: {
    baseURL,
    channel: "chrome", // installed Google Chrome; no browser download needed
    viewport: { width: 1366, height: 768 },
    locale: "vi-VN",
    screenshot: "only-on-failure",
    trace: "retain-on-failure",
  },
});
