// Money typed by staff (owner 29/10/2026). A number box reads "300.000" as 300 (the dot is a decimal point), which
// turned a 300.000đ minimum into 300đ. Money is whole đồng, so every dot, comma and space is a thousands separator.

/** "300.000" / "300,000" / "300 000đ" → 300000; "" → 0; anything without a digit → NaN. */
export function parseVnd(text: string | null | undefined): number {
  const raw = (text ?? "").trim();
  if (raw === "") return 0;
  const digits = raw.replace(/[.,\s_đĐ]/g, "");
  return /^\d+$/.test(digits) ? Number(digits) : Number.NaN;
}

/** 300000 → "300.000đ" (what the admin sees under the box, to catch a missing zero). */
export function formatVnd(value: number): string {
  return `${Math.round(value).toLocaleString("vi-VN")}đ`;
}
