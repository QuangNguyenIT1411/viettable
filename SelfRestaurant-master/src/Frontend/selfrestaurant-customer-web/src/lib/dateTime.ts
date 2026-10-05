/** Parse API timestamps according to the contract: offset-less ISO DateTime values are UTC. */
export function parseApiUtcTimestamp(value?: string | null): Date | null {
  if (!value) return null;
  let normalized = value.trim();
  if (!normalized) return null;
  const hasExplicitZone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(normalized);
  if (/^\d{4}-\d{2}-\d{2}T/.test(normalized) && !hasExplicitZone) normalized += "Z";
  normalized = normalized.replace(/(\.\d{3})\d+(?=(?:Z|[+-]\d{2}:\d{2})?$)/i, "$1");
  const parsed = new Date(normalized);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}
