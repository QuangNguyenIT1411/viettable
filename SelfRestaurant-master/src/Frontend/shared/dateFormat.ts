// One date/time style for all staff webs: "18:54, 13/10/2026".
// Callers keep their own parsing (UTC API timestamps vs local values); these only format.
const pad = (value: number) => String(value).padStart(2, "0");

function valid(date: Date | null | undefined): date is Date {
  return date instanceof Date && !Number.isNaN(date.getTime());
}

export function formatViTime(date: Date | null | undefined, fallback = "-") {
  return valid(date) ? `${pad(date.getHours())}:${pad(date.getMinutes())}` : fallback;
}

export function formatViDate(date: Date | null | undefined, fallback = "-") {
  return valid(date) ? `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()}` : fallback;
}

export function formatViDateTime(date: Date | null | undefined, fallback = "-") {
  return valid(date) ? `${formatViTime(date)}, ${formatViDate(date)}` : fallback;
}

/** "2026-10-14" (a calendar day, no time zone) -> "14/10/2026". */
export function formatViIsoDate(value: string | null | undefined, fallback = "-") {
  if (!value) return fallback;
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value.trim());
  return match ? `${match[3]}/${match[2]}/${match[1]}` : value;
}
