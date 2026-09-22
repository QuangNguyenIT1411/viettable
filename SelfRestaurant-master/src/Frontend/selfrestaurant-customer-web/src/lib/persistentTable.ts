import type { CustomerTableContextDto } from "./types";

const STORAGE_KEY = "selfrestaurant.customer.tableContextByCustomer";

type SavedTableContext = {
  customerId: number;
  tableId: number;
  branchId: number;
  branchName?: string | null;
  tableNumber?: number | null;
};

function canUseStorage() {
  return typeof window !== "undefined" && typeof window.localStorage !== "undefined";
}

function readStorage(): Record<string, SavedTableContext> {
  if (!canUseStorage()) return {};

  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as Record<string, SavedTableContext>;
    return parsed ?? {};
  } catch {
    return {};
  }
}

function writeStorage(payload: Record<string, SavedTableContext>) {
  if (!canUseStorage()) return;

  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
  } catch {
    // Ignore storage quota/privacy issues.
  }
}

export function savePersistentTableContext(customerId: number, tableContext: CustomerTableContextDto) {
  const store = readStorage();
  store[String(customerId)] = {
    customerId,
    tableId: tableContext.tableId,
    branchId: tableContext.branchId,
    branchName: tableContext.branchName,
    tableNumber: tableContext.tableNumber,
  };
  writeStorage(store);
}

export function getPersistentTableContext(customerId: number): SavedTableContext | null {
  const store = readStorage();
  return store[String(customerId)] ?? null;
}

export function clearPersistentTableContext(customerId: number) {
  const store = readStorage();
  delete store[String(customerId)];
  writeStorage(store);
}
