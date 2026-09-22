import type { CustomerTableContextDto, MenuDishDto } from "./types";

const CART_STORAGE_KEY = "selfrestaurant.customer.guestCartByTable";
const INTENT_STORAGE_KEY = "selfrestaurant.customer.pendingSubmitIntent";

export type GuestCartItem = {
  itemId: number;
  dishId: number;
  dishName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  note?: string | null;
  unit?: string | null;
  image?: string | null;
};

type GuestCartRecord = {
  tableId: number;
  branchId: number;
  items: GuestCartItem[];
};

type GuestCartStore = Record<string, GuestCartRecord>;

type PendingSubmitIntent = {
  tableId: number;
  branchId: number;
  idempotencyKey: string;
};

function canUseStorage() {
  return typeof window !== "undefined" && typeof window.localStorage !== "undefined";
}

function buildKey(tableId: number, branchId: number) {
  return `${branchId}:${tableId}`;
}

function readCartStore(): GuestCartStore {
  if (!canUseStorage()) return {};

  try {
    const raw = window.localStorage.getItem(CART_STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as GuestCartStore;
    return parsed ?? {};
  } catch {
    return {};
  }
}

function writeCartStore(payload: GuestCartStore) {
  if (!canUseStorage()) return;

  try {
    window.localStorage.setItem(CART_STORAGE_KEY, JSON.stringify(payload));
  } catch {
    // Ignore storage failures for local guest cart.
  }
}

export function getGuestMenuCart(tableContext: CustomerTableContextDto | null | undefined): GuestCartItem[] {
  if (!tableContext) return [];
  const store = readCartStore();
  return store[buildKey(tableContext.tableId, tableContext.branchId)]?.items ?? [];
}

export function saveGuestMenuCart(tableContext: CustomerTableContextDto, items: GuestCartItem[]) {
  const store = readCartStore();
  const key = buildKey(tableContext.tableId, tableContext.branchId);

  if (items.length === 0) {
    delete store[key];
  } else {
    store[key] = {
      tableId: tableContext.tableId,
      branchId: tableContext.branchId,
      items,
    };
  }

  writeCartStore(store);
}

export function clearGuestMenuCart(tableContext: CustomerTableContextDto | null | undefined) {
  if (!tableContext) return;
  const store = readCartStore();
  delete store[buildKey(tableContext.tableId, tableContext.branchId)];
  writeCartStore(store);
}

function nextGuestItemId(items: GuestCartItem[]) {
  return items.reduce((max, item) => Math.max(max, item.itemId), 0) + 1;
}

function normalizeNote(note: string | null | undefined) {
  const trimmed = (note ?? "").trim();
  return trimmed.length > 0 ? trimmed : "";
}

export function addDishToGuestCart(
  tableContext: CustomerTableContextDto,
  dish: MenuDishDto,
  quantity: number,
  note?: string | null,
) {
  const items = getGuestMenuCart(tableContext);
  const normalizedNote = normalizeNote(note);
  const existingIndex = items.findIndex((item) => item.dishId === dish.dishId && normalizeNote(item.note) === normalizedNote);

  if (existingIndex >= 0) {
    const existing = items[existingIndex];
    const updatedQuantity = existing.quantity + quantity;
    items[existingIndex] = {
      ...existing,
      quantity: updatedQuantity,
      lineTotal: existing.unitPrice * updatedQuantity,
    };
  } else {
    items.push({
      itemId: nextGuestItemId(items),
      dishId: dish.dishId,
      dishName: dish.name,
      quantity,
      unitPrice: dish.price,
      lineTotal: dish.price * quantity,
      note: normalizedNote || null,
      unit: dish.unit,
      image: dish.image,
    });
  }

  saveGuestMenuCart(tableContext, items);
  return items;
}

export function updateGuestCartItemQuantity(tableContext: CustomerTableContextDto, itemId: number, quantity: number) {
  const items = getGuestMenuCart(tableContext)
    .map((item) => item.itemId === itemId
      ? { ...item, quantity, lineTotal: item.unitPrice * quantity }
      : item)
    .filter((item) => item.quantity > 0);
  saveGuestMenuCart(tableContext, items);
  return items;
}

export function updateGuestCartItemNote(tableContext: CustomerTableContextDto, itemId: number, note: string) {
  const items = getGuestMenuCart(tableContext).map((item) => item.itemId === itemId
    ? { ...item, note: normalizeNote(note) || null }
    : item);
  saveGuestMenuCart(tableContext, items);
  return items;
}

export function removeGuestCartItem(tableContext: CustomerTableContextDto, itemId: number) {
  const items = getGuestMenuCart(tableContext).filter((item) => item.itemId !== itemId);
  saveGuestMenuCart(tableContext, items);
  return items;
}

export function getGuestCartSubtotal(items: GuestCartItem[]) {
  return items.reduce((sum, item) => sum + item.lineTotal, 0);
}

function createIntentKey() {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }

  return `submit-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

export function savePendingSubmitIntent(tableContext: CustomerTableContextDto, existingKey?: string | null) {
  if (!canUseStorage()) return;

  const payload: PendingSubmitIntent = {
    tableId: tableContext.tableId,
    branchId: tableContext.branchId,
    idempotencyKey: existingKey?.trim() || createIntentKey(),
  };

  try {
    window.localStorage.setItem(INTENT_STORAGE_KEY, JSON.stringify(payload));
  } catch {
    // Ignore local storage failures.
  }
}

export function readPendingSubmitIntent() {
  if (!canUseStorage()) return null;

  try {
    const raw = window.localStorage.getItem(INTENT_STORAGE_KEY);
    if (!raw) return null;
    return JSON.parse(raw) as PendingSubmitIntent;
  } catch {
    return null;
  }
}

export function clearPendingSubmitIntent() {
  if (!canUseStorage()) return;
  try {
    window.localStorage.removeItem(INTENT_STORAGE_KEY);
  } catch {
    // Ignore local storage failures.
  }
}

export function getOrCreatePendingSubmitKey(tableContext: CustomerTableContextDto) {
  const current = readPendingSubmitIntent();
  if (current && current.tableId === tableContext.tableId && current.branchId === tableContext.branchId && current.idempotencyKey) {
    return current.idempotencyKey;
  }

  savePendingSubmitIntent(tableContext);
  return readPendingSubmitIntent()?.idempotencyKey ?? createIntentKey();
}
