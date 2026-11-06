// "Khu vực & ghép bàn" (owner 06/11): the admin types table numbers ("5, 6"); the server stores table ids.
export type GroupingRow = { tableId: number; tableNumber: number; numberOfSeats: number; area?: string | null; linkedTableIds: number[] };

/** "5, 6" / "5 6" / "5;6" → table ids of this branch; unknown numbers and the table itself are reported. */
export function parseLinkedTables(text: string, self: GroupingRow, rows: GroupingRow[]): { ids: number[]; error: string | null } {
  const parts = text.split(/[\s,;+]+/).map(x => x.trim()).filter(Boolean);
  const ids: number[] = [];
  for (const part of parts) {
    if (!/^\d+$/.test(part)) return { ids: [], error: `"${part}" không phải số bàn.` };
    const number = Number(part);
    if (number === self.tableNumber) return { ids: [], error: "Không ghép bàn với chính nó." };
    const row = rows.find(r => r.tableNumber === number);
    if (!row) return { ids: [], error: `Không có Bàn ${number} ở chi nhánh này.` };
    if (!ids.includes(row.tableId)) ids.push(row.tableId);
  }
  return { ids, error: null };
}

/** Table ids → "5, 6" for the input. */
export function linkedNumbers(ids: number[], rows: GroupingRow[]): string {
  return ids.map(id => rows.find(r => r.tableId === id)?.tableNumber).filter((n): n is number => n != null)
    .sort((a, b) => a - b).join(", ");
}
