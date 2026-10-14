import { expect, type Page } from "@playwright/test";

const LEAKED = /\b(null|undefined|NaN)\b/;

/**
 * The visible page has no error box and no leaked programming values ("null", "undefined", "NaN"),
 * neither in text nor in form fields. Pass allowErrorBox when the test expects an error message.
 */
export async function expectCleanScreen(page: Page, options: { allowErrorBox?: boolean } = {}) {
  await page.waitForLoadState("networkidle").catch(() => undefined);
  const where = new URL(page.url()).pathname;
  if (!options.allowErrorBox) {
    const boxes = page.locator(".error-box:visible, .alert-danger:visible");
    await expect(boxes, `khung lỗi đang hiện ở ${where}: ${(await boxes.allInnerTexts()).join(" | ")}`).toHaveCount(0);
  }
  const text = await page.locator("body").innerText();
  const line = text.split("\n").find((l) => LEAKED.test(l));
  expect(line, `chữ lạ trên màn hình ở ${where}`).toBeUndefined();
  const fields = await page.locator("input:visible, textarea:visible").evaluateAll((elements) =>
    elements.map((e) => (e as HTMLInputElement).value).filter((v) => /\b(null|undefined|NaN)\b/.test(v)));
  expect(fields, `ô nhập có chữ lạ ở ${where}`).toEqual([]);
}
