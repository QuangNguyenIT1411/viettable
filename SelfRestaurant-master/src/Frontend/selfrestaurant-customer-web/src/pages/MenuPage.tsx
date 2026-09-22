import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import { api } from "../lib/api";
import {
  addDishToGuestCart,
  clearGuestMenuCart,
  clearPendingSubmitIntent,
  getOrCreatePendingSubmitKey,
  getGuestCartSubtotal,
  getGuestMenuCart,
  readPendingSubmitIntent,
  removeGuestCartItem,
  savePendingSubmitIntent,
  updateGuestCartItemNote,
  updateGuestCartItemQuantity,
  type GuestCartItem,
} from "../lib/guestCart";
import { clearPersistentTableContext, savePersistentTableContext } from "../lib/persistentTable";
import type { ActiveOrderItemDto, MenuDishDto, ReadyDishNotificationDto } from "../lib/types";

const readyDismissedStorageKey = "selfrestaurant.customer.readyNotifications.dismissed.v1";
const readyDisplayedStorageKey = "selfrestaurant.customer.readyNotifications.displayed.v1";

function readReadyKeySet(storageKey: string) {
  if (typeof window === "undefined" || typeof window.sessionStorage === "undefined") {
    return new Set<string>();
  }

  try {
    const raw = window.sessionStorage.getItem(storageKey);
    const parsed = raw ? JSON.parse(raw) : [];
    return new Set(Array.isArray(parsed) ? parsed.filter((value): value is string => typeof value === "string") : []);
  } catch {
    return new Set<string>();
  }
}

function writeReadyKeySet(storageKey: string, values: Set<string>) {
  if (typeof window === "undefined" || typeof window.sessionStorage === "undefined") {
    return;
  }

  window.sessionStorage.setItem(storageKey, JSON.stringify([...values]));
}

function readyNotificationKey(notification: ReadyDishNotificationDto) {
  if (notification.notificationId) return `notification:${notification.notificationId}`;
  return `ready:${notification.orderId ?? "order"}:${notification.orderItemId ?? "item"}:${notification.status ?? "READY"}:${notification.createdAt ?? "time"}`;
}

function readyItemFallbackKey(item: ActiveOrderItemDto) {
  return `item:${item.orderId}:${item.itemId}:${item.status ?? "READY"}`;
}

const t = {
  loading: "\u0110ang t\u1ea3i menu...",
  home: "Quay l\u1ea1i trang ch\u1ee7",
  branch: "Chi nh\u00e1nh",
  table: "B\u00e0n s\u1ed1",
  top: "G\u1ee3i \u00fd m\u00f3n \u0103n h\u00f4m nay",
  topSub: "Gợi ý dựa trên thực đơn hôm nay",
  topEmpty: "Ch\u01b0a c\u00f3 g\u1ee3i \u00fd AI ph\u00f9 h\u1ee3p l\u00fac n\u00e0y. H\u1ec7 th\u1ed1ng s\u1ebd hi\u1ec3n th\u1ecb m\u00f3n n\u1ed5i b\u1eadt thay th\u1ebf.",
  search: "T\u00ecm m\u00f3n theo t\u00ean...",
  veg: "Ch\u1ec9 hi\u1ec7n m\u00f3n chay",
  vegOnlyLabel: "M\u00f3n chay",
  cardDescFallback: "",
  unknownDesc: "Ch\u01b0a c\u00f3 m\u00f4 t\u1ea3.",
  today: "H\u00f4m nay",
  vegBadge: "Chay",
  topBadge: "HOT",
  add: "Th\u00eam",
  detail: "Chi ti\u1ebft m\u00f3n",
  cart: "Gi\u1ecf h\u00e0ng",
  empty: "Gi\u1ecf h\u00e0ng tr\u1ed1ng",
  emptyHint: "H\u00e3y ch\u1ecdn m\u00f3n \u0103n y\u00eau th\u00edch",
  pending: "Ch\u01b0a g\u1eedi b\u1ebfp",
  kitchen: "\u0110\u00e3 g\u1eedi b\u1ebfp",
  ready: "M\u00f3n s\u1eb5n s\u00e0ng",
  received: "M\u00f3n \u0111\u00e3 nh\u1eadn",
  wait: "Ch\u1edd g\u1eedi",
  prep: "\u0110ang chu\u1ea9n b\u1ecb",
  done: "S\u1eb5n s\u00e0ng",
  receivedDone: "\u0110\u00e3 nh\u1eadn",
  note: "Ghi ch\u00fa (vd: \u00edt cay, kh\u00f4ng h\u00e0nh...)",
  noNote: "Ch\u01b0a c\u00f3 ghi ch\u00fa",
  billBtn: "Xem h\u00f3a \u0111\u01a1n t\u1ea1m t\u00ednh",
  subtotalSummary: (count: number) => `T\u1ea1m t\u00ednh (${count} m\u00f3n)`,
  total: "T\u1ed5ng c\u1ed9ng:",
  points: "\u0110i\u1ec3m t\u00edch l\u0169y:",
  currentPoints: "\u0110i\u1ec3m hi\u1ec7n t\u1ea1i:",
  estimatedPoints: "\u0110i\u1ec3m th\u01b0\u1edfng (d\u1ef1 ki\u1ebfn):",
  pointsSuffix: "\u0111i\u1ec3m",
  send: "G\u1eedi \u0111\u01a1n cho b\u1ebfp",
  sendHint: "G\u1eedi m\u00f3n tr\u01b0\u1edbc khi thanh to\u00e1n",
  loginToSend: "Vui l\u00f2ng \u0111\u0103ng nh\u1eadp \u0111\u1ec3 g\u1eedi m\u00f3n cho b\u1ebfp.",
  checkout: "Thanh to\u00e1n",
  checkoutHint: "Vui l\u00f2ng \u0111\u1ebfn qu\u1ea7y thu ng\u00e2n \u0111\u1ec3 thanh to\u00e1n",
  checkoutPendingMessage: "Vui l\u00f2ng g\u1eedi m\u00f3n cho b\u1ebfp tr\u01b0\u1edbc khi thanh to\u00e1n.",
  checkoutEmptyMessage: "Gi\u1ecf h\u00e0ng tr\u1ed1ng. Vui l\u00f2ng ch\u1ecdn m\u00f3n \u0103n.",
  checkoutPreparingMessage: "Vui l\u00f2ng \u0111\u1ee3i t\u1ea5t c\u1ea3 m\u00f3n \u0103n \u0111\u01b0\u1ee3c chu\u1ea9n b\u1ecb xong tr\u01b0\u1edbc khi thanh to\u00e1n.",
  reset: "Reset b\u00e0n",
  resetTitle: "X\u00e1c nh\u1eadn reset b\u00e0n",
  resetBody: "B\u1ea1n c\u00f3 ch\u1eafc ch\u1eafn mu\u1ed1n reset b\u00e0n hi\u1ec7n t\u1ea1i? H\u00e0nh \u0111\u1ed9ng n\u00e0y s\u1ebd x\u00f3a t\u1ea5t c\u1ea3 \u0111\u01a1n h\u00e0ng ch\u01b0a \u0111\u01b0\u1ee3c x\u1eed l\u00fd.",
  resetConfirm: "X\u00e1c nh\u1eadn reset",
  cancel: "H\u1ee7y",
  bill: "H\u00d3A \u0110\u01a0N T\u1ea0M T\u00cdNH",
  time: "Th\u1eddi gian",
  items: "T\u1ed5ng m\u00f3n",
  subtotal: "T\u1ea1m t\u00ednh",
  billPending: "Ch\u1edd g\u1eedi",
  billPlaced: "\u0110\u00e3 \u0111\u1eb7t",
  close: "\u0110\u00f3ng",
  sendConfirmTitle: "X\u00e1c nh\u1eadn g\u1eedi \u0111\u01a1n cho b\u1ebfp",
  sendConfirmBody: "Vui l\u00f2ng x\u00e1c nh\u1eadn c\u00e1c m\u00f3n sau s\u1ebd \u0111\u01b0\u1ee3c g\u1eedi cho b\u1ebfp:",
  sendConfirmHint: "H\u00e0nh \u0111\u1ed9ng n\u00e0y kh\u00f4ng th\u1ec3 ho\u00e0n t\u00e1c. Vui l\u00f2ng ki\u1ec3m tra k\u1ef9 tr\u01b0\u1edbc khi x\u00e1c nh\u1eadn.",
  sendConfirmButton: "X\u00e1c nh\u1eadn g\u1eedi",
  payNotice: "Y\u00eau c\u1ea7u thanh to\u00e1n \u0111\u00e3 g\u1eedi!",
  payBody: "Vui l\u00f2ng \u0111\u1ebfn qu\u1ea7y thu ng\u00e2n \u0111\u1ec3 ho\u00e0n t\u1ea5t thanh to\u00e1n.",
  tableCode: "M\u00e3 b\u00e0n c\u1ee7a b\u1ea1n",
  understood: "\u0110\u00e3 Hi\u1ec3u",
  readyTitle: "M\u00f3n \u0103n \u0111\u00e3 s\u1eb5n s\u00e0ng",
  readyBody: "B\u1ebfp \u0111\u00e3 ho\u00e0n th\u00e0nh m\u00f3n \u0103n cho b\u00e0n hi\u1ec7n t\u1ea1i. Vui l\u00f2ng \u0111\u1ebfn qu\u1ea7y l\u1ea5y m\u00f3n. Sau khi \u0111\u00e3 nh\u1eadn m\u00f3n, h\u00e3y nh\u1ea5n x\u00e1c nh\u1eadn \u0111\u1ec3 ho\u00e0n t\u1ea5t.",
  readyHint: "Vi\u1ec7c x\u00e1c nh\u1eadn gi\u00fap h\u1ec7 th\u1ed1ng \u1ea9n \u0111\u01a1n kh\u1ecfi m\u00e0n h\u00ecnh b\u1ebfp v\u00e0 s\u1eb5n s\u00e0ng cho l\u1ea7n \u0111\u1eb7t m\u00f3n ti\u1ebfp theo.",
  confirmReceived: "T\u00f4i \u0111\u00e3 nh\u1eadn m\u00f3n",
  later: "\u0110\u1ec3 sau",
  available: "\u0110ang b\u00e1n",
  unavailable: "T\u1ea1m h\u1ebft",
  price: "Gi\u00e1 b\u00e1n",
  unit: "\u0110\u01a1n v\u1ecb",
  desc: "M\u00f4 t\u1ea3 m\u00f3n",
  ingredients: "Nguy\u00ean li\u1ec7u ch\u00ednh",
  descFallback: "M\u00f3n \u0103n \u0111ang c\u1eadp nh\u1eadt m\u00f4 t\u1ea3.",
  ingredientsFallback: "Ch\u01b0a khai b\u00e1o th\u00e0nh ph\u1ea7n cho m\u00f3n n\u00e0y.",
  quickAdd: "Th\u00eam v\u00e0o gi\u1ecf",
  noDish: "Kh\u00f4ng c\u00f3 m\u00f3n \u0103n n\u00e0o",
  categoryChip: "Danh mục",
  unavailableHint: "M\u00f3n \u0111ang t\u1ea1m h\u1ebft ho\u1eb7c ng\u1eebng b\u00e1n",
  unavailableCartHint: "Gi\u1ecf h\u00e0ng c\u00f3 m\u00f3n \u0111ang t\u1ea1m h\u1ebft. Vui l\u00f2ng x\u00f3a ho\u1eb7c gi\u1ea3m m\u00f3n \u0111\u00f3 tr\u01b0\u1edbc khi g\u1eedi b\u1ebfp.",
} as const;

const vnd = (n: number) => `${n.toLocaleString("vi-VN")} \u0111`;
const line = (n: number, q: number) => `${vnd(n)} \u00d7 ${q}`;
const placeholderDishImage = "/images/placeholder-dish.svg";
const recommendationLimit = 6;
const textFixups = new Map<string, string>([
  ["Mi Xao Bo", "Mì Xào Bò"],
  ["Mi xao bo dam da huong vi", "Mì xào bò đậm đà hương vị"],
  ["Pháº§n", "Phần"],
  ["phan", "Phần"],
]);

function slugifyDishName(name: string) {
  return name
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/\u0111/g, "d")
    .replace(/\u0110/g, "D")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

function resolveDishImage(image: string | null | undefined, dishName: string) {
  const normalized = (image ?? "").trim();
  if (normalized.startsWith("/images/")) {
    return normalized;
  }

  const slug = slugifyDishName(dishName);
  if (!slug) {
    return placeholderDishImage;
  }

  return `/images/${slug}.jpg`;
}

function handleDishImageError(event: React.SyntheticEvent<HTMLImageElement>) {
  const img = event.currentTarget;
  if (img.src.endsWith(placeholderDishImage)) {
    return;
  }

  img.src = placeholderDishImage;
}

function normalizeDishText(value: string | null | undefined) {
  const trimmed = (value ?? "").trim();
  if (!trimmed) {
    return "";
  }

  const exact = textFixups.get(trimmed);
  if (exact) {
    return exact;
  }

  return trimmed
    .replace(/Pháº§n/g, "Phần")
    .replace(/\bphan\b/gi, "Phần");
}

function group(item: ActiveOrderItemDto, orderStatus: string) {
  const status = (item.status ?? orderStatus ?? "").toUpperCase();
  if (status === "CANCELLED") return "cancelled";
  if (status === "READY") return "ready";
  // Customer confirm-received moves READY items to SERVING in Orders.Api.
  // Treating SERVING as "ready" causes the acknowledged ready modal to
  // reappear after a reload even though the notification was already resolved.
  if (status === "SERVING") return "received";
  if (["SERVED", "COMPLETED"].includes(status)) return "received";
  if (["CONFIRMED", "PREPARING"].includes(status)) return "kitchen";
  return "pending";
}

function badge(kind: "pending" | "kitchen" | "ready" | "received" | "cancelled") {
  if (kind === "cancelled") return <span className="badge bg-danger"><i className="bi bi-x-circle me-1" />Đã hủy</span>;
  if (kind === "ready") return <span className="badge bg-success"><i className="bi bi-check-circle me-1" />{t.done}</span>;
  if (kind === "received") return <span className="badge bg-secondary"><i className="bi bi-bag-check me-1" />{t.receivedDone}</span>;
  if (kind === "kitchen") return <span className="badge bg-primary"><i className="bi bi-fire me-1" />{t.prep}</span>;
  return <span className="badge bg-secondary"><i className="bi bi-hourglass me-1" />{t.wait}</span>;
}

export function MenuPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [activeCategoryId, setActiveCategoryId] = useState<number | null>(null);
  const [dishQuantities, setDishQuantities] = useState<Record<number, number>>({});
  const [search, setSearch] = useState("");
  const [vegOnly, setVegOnly] = useState(false);
  const [selectedDish, setSelectedDish] = useState<MenuDishDto | null>(null);
  const [showBill, setShowBill] = useState(false);
  const [showCheckoutNotice, setShowCheckoutNotice] = useState(false);
  const [showSendConfirm, setShowSendConfirm] = useState(false);
  const [showResetConfirm, setShowResetConfirm] = useState(false);
  const [showReadyModal, setShowReadyModal] = useState(false);
  const [dismissedReadyKeys, setDismissedReadyKeys] = useState<Set<string>>(() => readReadyKeySet(readyDismissedStorageKey));
  const [displayedReadyKeys, setDisplayedReadyKeys] = useState<Set<string>>(() => readReadyKeySet(readyDisplayedStorageKey));
  const [showReadyNotification, setShowReadyNotification] = useState(false);
  const [toast, setToast] = useState<{ type: "success" | "info"; message: string } | null>(null);
  const [guestCartItems, setGuestCartItems] = useState<GuestCartItem[]>([]);
  const menu = useQuery({
    queryKey: ["menu"],
    queryFn: api.getMenu,
    staleTime: 60000,
    refetchInterval: 10000,
    refetchOnWindowFocus: false,
  });
  const session = useQuery({
    queryKey: ["session"],
    queryFn: api.getSession,
    staleTime: 60000,
    refetchOnWindowFocus: false,
  });
  const isAuthenticated = Boolean(session.data?.authenticated);
  const activeTableContext = session.data?.tableContext ?? menu.data?.tableContext ?? null;
  const hasGuestCart = guestCartItems.length > 0;
  const order = useQuery({
    queryKey: ["order"],
    queryFn: api.getOrder,
    enabled: isAuthenticated && !hasGuestCart,
    refetchInterval: 10000,
    refetchOnWindowFocus: false,
  });
  const orderItems = useQuery({
    queryKey: ["orderItems"],
    queryFn: api.getOrderItems,
    enabled: isAuthenticated && !hasGuestCart,
    refetchInterval: 10000,
    refetchOnWindowFocus: false,
  });
  const readyNotifications = useQuery({
    queryKey: ["readyNotifications"],
    queryFn: api.getReadyNotifications,
    enabled: isAuthenticated && !hasGuestCart,
    refetchInterval: 10000,
    refetchOnWindowFocus: false,
  });
  const recommendations = useQuery({
    queryKey: ["menuRecommendations", menu.data?.tableContext?.branchId, menu.data?.tableContext?.tableId, isAuthenticated, guestCartItems.map((item) => item.dishId).sort((a, b) => a - b).join(",")],
    queryFn: () => api.getMenuRecommendations(
      hasGuestCart
        ? Array.from(new Set(guestCartItems.map((item) => item.dishId))).sort((a, b) => a - b)
        : undefined,
    ),
    enabled: Boolean(menu.data?.menu && activeTableContext),
    staleTime: 60000,
    refetchOnWindowFocus: false,
  });

  const addItem = useMutation({
    mutationFn: api.addItem,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["order"] });
      await queryClient.invalidateQueries({ queryKey: ["orderItems"] });
    },
    onError: (error) => {
      setToast({ type: "info", message: (error as Error).message });
    },
  });
  const removeItem = useMutation({
    mutationFn: api.removeItem,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["order"] });
      await queryClient.invalidateQueries({ queryKey: ["orderItems"] });
    },
  });
  const updateNote = useMutation({
    mutationFn: ({ itemId, note }: { itemId: number; note: string }) => api.updateItemNote(itemId, note),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["order"] });
      await queryClient.invalidateQueries({ queryKey: ["orderItems"] });
    },
  });
  const bumpQty = useMutation({
    mutationFn: ({ itemId, quantity }: { itemId: number; quantity: number }) => api.updateItemQuantity(itemId, quantity),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["order"] });
      await queryClient.invalidateQueries({ queryKey: ["orderItems"] });
    },
  });
  const submitOrder = useMutation({
    mutationFn: api.submitMenuOrder,
    onSuccess: async () => {
      await queryClient.invalidateQueries();
    },
    onError: (error) => {
      setToast({ type: "info", message: (error as Error).message });
    },
  });
  const clearTable = useMutation({
    mutationFn: api.resetCurrentTable,
    onSuccess: async () => {
      clearGuestMenuCart(activeTableContext);
      clearPendingSubmitIntent();
      setGuestCartItems([]);
      if (session.data?.customer?.customerId) {
        clearPersistentTableContext(session.data.customer.customerId);
      }
      await queryClient.invalidateQueries();
      navigate("/Home/Index");
    },
  });
  const resolveNotification = useMutation({
    mutationFn: api.resolveReadyNotification,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["readyNotifications"] });
    },
  });
  const confirmReceived = useMutation({
    mutationFn: api.confirmReceived,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["order"] });
      await queryClient.invalidateQueries({ queryKey: ["orderItems"] });
      await queryClient.invalidateQueries({ queryKey: ["readyNotifications"] });
    },
  });

  const markReadyDismissed = (keys: string[]) => {
    if (keys.length === 0) return;
    setDismissedReadyKeys((current) => {
      const next = new Set(current);
      keys.forEach((key) => next.add(key));
      writeReadyKeySet(readyDismissedStorageKey, next);
      return next;
    });
    setDisplayedReadyKeys((current) => {
      const next = new Set(current);
      keys.forEach((key) => next.add(key));
      writeReadyKeySet(readyDisplayedStorageKey, next);
      return next;
    });
  };

  const resolveReadyNotificationIds = async (notificationIds: number[]) => {
    const uniqueIds = [...new Set(notificationIds.filter((notificationId) => notificationId > 0))];
    if (uniqueIds.length === 0) return;
    await Promise.all(uniqueIds.map((notificationId) => resolveNotification.mutateAsync(notificationId).catch(() => undefined)));
  };

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 5000);
    return () => window.clearTimeout(timer);
  }, [toast]);

  useEffect(() => {
    if (!showReadyNotification) return;
    const timer = window.setTimeout(() => setShowReadyNotification(false), 15000);
    return () => window.clearTimeout(timer);
  }, [showReadyNotification]);

  useEffect(() => {
    if (session.data?.customer?.customerId && session.data?.tableContext) {
      savePersistentTableContext(session.data.customer.customerId, session.data.tableContext);
    }
  }, [session.data]);

  useEffect(() => {
    setGuestCartItems(getGuestMenuCart(activeTableContext));
  }, [activeTableContext]);

  useEffect(() => {
    if (!isAuthenticated || !activeTableContext || guestCartItems.length === 0) {
      return;
    }

    const pendingSubmitIntent = readPendingSubmitIntent();
    if (!pendingSubmitIntent) {
      return;
    }

    if (pendingSubmitIntent.tableId === activeTableContext.tableId && pendingSubmitIntent.branchId === activeTableContext.branchId) {
      clearPendingSubmitIntent();
      setShowSendConfirm(true);
    }
  }, [activeTableContext, guestCartItems.length, isAuthenticated]);

  const menuPayload = menu.data;
  const categoryList = menuPayload?.menu?.categories ?? [];
  const data = menuPayload;
  const items = hasGuestCart
    ? guestCartItems.map((item) => ({
      ...item,
      orderId: 0,
      status: "PENDING",
    } as ActiveOrderItemDto))
    : (orderItems.data?.items ?? []);
  const subtotal = hasGuestCart ? getGuestCartSubtotal(guestCartItems) : (orderItems.data?.subtotal ?? 0);
  const points = Math.floor(subtotal / 10000);
  const currentLoyaltyPoints = menuPayload?.customer?.loyaltyPoints ?? session.data?.customer?.loyaltyPoints ?? 0;
  const orderStatus = hasGuestCart ? "PENDING" : (order.data?.statusCode || order.data?.orderStatus || "").toUpperCase();
  const currentOrderId = hasGuestCart ? null : (order.data?.orderId ?? null);
  const activeOrderIds = hasGuestCart ? [] : (order.data?.activeOrderIds ?? (currentOrderId ? [currentOrderId] : []));
  const hasPendingRound = hasGuestCart || Boolean(order.data?.hasPendingRound);
  const customerName = session.data?.customer?.name ?? "";
  const activeReadyNotifications = useMemo(
    () => (readyNotifications.data?.items ?? [])
      .filter((item) => !activeOrderIds.length || item.orderId == null || activeOrderIds.includes(item.orderId))
      .filter((item) => !dismissedReadyKeys.has(readyNotificationKey(item))),
    [activeOrderIds, dismissedReadyKeys, readyNotifications.data?.items],
  );
  const activeReadyNotification = activeReadyNotifications[0] ?? null;
  const visibleCategories = useMemo(
    () => categoryList.filter((category) => (
      category.dishes.some((dish) => (!vegOnly || dish.isVegetarian))
    )),
    [categoryList, vegOnly],
  );

  useEffect(() => {
    if (categoryList.length > 0 && activeCategoryId === null) {
      setActiveCategoryId(categoryList[0].categoryId);
    }
  }, [activeCategoryId, categoryList]);

  useEffect(() => {
    if (visibleCategories.length === 0) return;
    if (!visibleCategories.some((category) => category.categoryId === activeCategoryId)) {
      setActiveCategoryId(visibleCategories[0].categoryId);
    }
  }, [activeCategoryId, visibleCategories]);

  const pending = items.filter((item) => group(item, orderStatus) === "pending");
  const kitchen = items.filter((item) => group(item, orderStatus) === "kitchen");
  const ready = items.filter((item) => group(item, orderStatus) === "ready");
  const received = items.filter((item) => group(item, orderStatus) === "received");
  const cancelled = items.filter((item) => group(item, orderStatus) === "cancelled");
  const hasReadyItems = ready.length > 0;
  const hasReceivedItems = received.length > 0;
  const readyFallbackKeys = useMemo(
    () => ready.map(readyItemFallbackKey).filter((key) => !dismissedReadyKeys.has(key)),
    [dismissedReadyKeys, ready],
  );
  const activeReadyKeys = useMemo(
    () => [
      ...activeReadyNotifications.map(readyNotificationKey),
      ...readyFallbackKeys,
    ].filter((key, index, keys) => keys.indexOf(key) === index),
    [activeReadyNotifications, readyFallbackKeys],
  );
  const activeReadyNotificationIds = useMemo(
    () => [...new Set(activeReadyNotifications.map((notification) => notification.notificationId).filter((notificationId) => notificationId > 0))],
    [activeReadyNotifications],
  );
  const currentReadyOrderId = activeReadyNotification?.orderId ?? (ready[0]?.orderId ?? null);
  const isReadyLikeStatus = activeReadyKeys.length > 0;
  const checkoutItems = [...kitchen, ...ready, ...received, ...cancelled];
  const totalCartCount = items.filter((item) => group(item, orderStatus) !== "cancelled").reduce((sum, item) => sum + item.quantity, 0);
  const totalDishCount = items.filter((item) => group(item, orderStatus) !== "cancelled").reduce((sum, item) => sum + item.quantity, 0);

  useEffect(() => {
    if (!isReadyLikeStatus || (!hasReadyItems && activeReadyNotifications.length === 0)) {
      return;
    }

    const unseenKeys = activeReadyKeys.filter((key) => !displayedReadyKeys.has(key) && !dismissedReadyKeys.has(key));
    if (unseenKeys.length > 0) {
      setDisplayedReadyKeys((current) => {
        const next = new Set(current);
        unseenKeys.forEach((key) => next.add(key));
        writeReadyKeySet(readyDisplayedStorageKey, next);
        return next;
      });
      setShowReadyNotification(true);
      setToast({
        type: "success",
        message: unseenKeys.length > 1
          ? "Bếp đã hoàn thành nhiều món/đơn của bạn! Vui lòng đến quầy nhận món."
          : "Bếp đã hoàn thành món ăn của bạn! Vui lòng đến quầy nhận món.",
      });
    }

    if (activeReadyKeys.some((key) => !dismissedReadyKeys.has(key))) {
      setShowReadyModal(true);
    }
  }, [activeReadyKeys, activeReadyNotifications.length, dismissedReadyKeys, displayedReadyKeys, hasReadyItems, isReadyLikeStatus]);

  useEffect(() => {
    if (!isReadyLikeStatus) {
      setShowReadyModal(false);
      setShowReadyNotification(false);
    }
  }, [isReadyLikeStatus]);

  useEffect(() => {
    if (!selectedDish) {
      return;
    }

    const latestDish = categoryList
      .flatMap((category) => category.dishes)
      .find((dish) => dish.dishId === selectedDish.dishId);

    if (!latestDish) {
      setSelectedDish(null);
      return;
    }

    if (latestDish !== selectedDish) {
      setSelectedDish(latestDish);
    }
  }, [categoryList, selectedDish]);

  if (menu.isLoading || order.isLoading || orderItems.isLoading || session.isLoading) {
    return (
      <div className="menu-loading-state">
        <div className="spinner-border text-primary" role="status">
          <span className="visually-hidden">{t.loading}</span>
        </div>
        <p className="mt-3 text-muted mb-0">{t.loading}</p>
      </div>
    );
  }

  if (menu.error) {
    return (
      <div className="container py-4">
        <div className="alert alert-danger menu-page-alert mb-0" role="alert">
          <i className="bi bi-exclamation-triangle-fill me-2" />
          {(menu.error as Error).message}
        </div>
      </div>
    );
  }

  if (!menu.data?.menu || !Array.isArray(menu.data.menu.categories) || !session.data?.tableContext) {
    return (
      <div className="container py-4">
        <div className="card menu-page-alert-card">
          <h3 className="mb-3">Menu chưa sẵn sàng để hiển thị</h3>
          <p className="muted mb-3">
            Dữ liệu bàn hoặc thực đơn hiện chưa đồng bộ xong. Bạn tải lại trang hoặc quay về trang chủ để chọn lại bàn.
          </p>
          <div className="d-flex flex-wrap gap-2">
            <button type="button" className="btn btn-outline-secondary" onClick={() => window.location.reload()}>
              Tải lại trang
            </button>
            <Link to="/Home/Index" className="btn btn-danger">
              Quay về trang chủ
            </Link>
          </div>
        </div>
      </div>
    );
  }

  const safeData = data!;

  const categories = safeData.menu.categories
    .map((category) => ({
      ...category,
      dishes: category.dishes.filter((dish) => (
        (!vegOnly || dish.isVegetarian)
        && (!search.trim() || dish.name.toLowerCase().includes(search.trim().toLowerCase()))
      )),
    }))
    .filter((category) => category.dishes.length > 0)
    .filter((category) => (activeCategoryId ? category.categoryId === activeCategoryId : true));

  const allMenuDishes = safeData.menu.categories
    .flatMap((category) => category.dishes.map((dish) => ({ ...dish, categoryId: category.categoryId })));
  const menuDishLookup = new Map(allMenuDishes.map((dish) => [dish.dishId, dish]));
  const allAvailableDishes = safeData.menu.categories
    .flatMap((category) => category.dishes.map((dish) => ({ ...dish, categoryId: category.categoryId })))
    .filter((dish) => dish.available);

  const topDishIds = safeData.topDishIds.slice(0, recommendationLimit);
  const recommendationLookup = new Map(
    (recommendations.data?.recommendations ?? []).map((item) => [item.dishId, item]),
  );
  const recommendedDishes = (recommendations.data?.recommendations?.length
    ? recommendations.data.recommendations.map((item) => ({
      dish: {
        ...(allAvailableDishes.find((dish) => dish.dishId === item.dishId) ?? {
          dishId: item.dishId,
          name: item.name,
          description: item.description,
          price: item.price,
          image: item.image,
          unit: item.unit,
          isVegetarian: item.isVegetarian,
          isDailySpecial: item.isDailySpecial,
          available: item.available,
          ingredients: item.ingredients ?? [],
        }),
        categoryId: allAvailableDishes.find((dish) => dish.dishId === item.dishId)?.categoryId ?? item.categoryId,
      },
      reason: item.reason,
    }))
    : topDishIds.map((dishId) => ({
      dish: allAvailableDishes.find((dish) => dish.dishId === dishId),
      reason: recommendationLookup.get(dishId)?.reason ?? "Được nhiều khách gọi hôm nay",
    })))
    .filter((item): item is { dish: MenuDishDto & { categoryId: number }; reason: string } => Boolean(item.dish))
    .filter((item) => !vegOnly || item.dish.isVegetarian)
    .slice(0, recommendationLimit);
  const topSellerDishIds = topDishIds
    .map((dishId) => allAvailableDishes.find((dish) => dish.dishId === dishId))
    .filter((dish): dish is (MenuDishDto & { categoryId: number }) => Boolean(dish))
    .filter((dish) => !vegOnly || dish.isVegetarian)
    .map((dish) => dish.dishId);
  const unavailablePendingItems = pending.filter((item) => {
    const latestDish = menuDishLookup.get(item.dishId);
    return !latestDish || !latestDish.available;
  });
  const hasUnavailablePendingItems = unavailablePendingItems.length > 0;
  const unavailablePendingNames = unavailablePendingItems
    .map((item) => item.dishName)
    .filter((name, index, array) => array.indexOf(name) === index)
    .slice(0, 4);
  const resetConfirmationBody = isAuthenticated
    ? t.resetBody
    : "Bạn chưa đăng nhập. Reset sẽ dọn trạng thái tất cả bàn về trống/sẵn sàng trên hệ thống. Bạn có chắc chắn muốn tiếp tục?";

  function getDishQuantity(dishId: number) {
    return dishQuantities[dishId] ?? 1;
  }

  function increaseDishQuantity(dishId: number) {
    setDishQuantities((current) => ({ ...current, [dishId]: getDishQuantity(dishId) + 1 }));
  }

  function decreaseDishQuantity(dishId: number) {
    setDishQuantities((current) => ({ ...current, [dishId]: Math.max(1, getDishQuantity(dishId) - 1) }));
  }

  function addDishToCart(dishId: number) {
    const quantity = getDishQuantity(dishId);
    const dish = menuDishLookup.get(dishId);
    if (!dish || !activeTableContext) {
      return;
    }
    if (!dish.available) {
      setToast({ type: "info", message: `Món "${normalizeDishText(dish.name)}" hiện đang tạm hết.` });
      return;
    }

    // Once a dining session is already serving, new dishes should stay local
    // until the customer explicitly sends the next round to kitchen.
    const shouldUseGuestRoundCart = hasGuestCart || !isAuthenticated || (Boolean(order.data?.hasActiveDiningSession) && !hasPendingRound);

    if (shouldUseGuestRoundCart) {
      const nextItems = addDishToGuestCart(activeTableContext, dish, quantity);
      setGuestCartItems(nextItems);
      setDishQuantities((current) => ({ ...current, [dishId]: 1 }));
      return;
    }

    const existingPendingItem = pending.find((item) => item.dishId === dishId && !(item.note ?? "").trim());

    if (existingPendingItem) {
      bumpQty.mutate(
        { itemId: existingPendingItem.itemId, quantity: existingPendingItem.quantity + quantity },
        {
          onSuccess: async () => {
            setDishQuantities((current) => ({ ...current, [dishId]: 1 }));
            await queryClient.invalidateQueries({ queryKey: ["order"] });
            await queryClient.invalidateQueries({ queryKey: ["orderItems"] });
          },
        },
      );
      return;
    }

    addItem.mutate({ dishId, quantity, expectedDiningSessionCode: order.data?.diningSessionCode ?? null }, {
      onSuccess: async () => {
        setDishQuantities((current) => ({ ...current, [dishId]: 1 }));
        await queryClient.invalidateQueries({ queryKey: ["order"] });
        await queryClient.invalidateQueries({ queryKey: ["orderItems"] });
      },
    });
  }

  return (
    <div className="menu-page-shell">
      {toast ? (
        <div className="menu-toast-host">
          <div className={`toast show align-items-center text-white border-0 bg-${toast.type === "success" ? "success" : "info"}`} role="alert">
            <div className="d-flex">
              <div className="toast-body">
                <i className="bi bi-check-circle me-2" />
                {toast.message}
              </div>
              <button type="button" className="btn-close btn-close-white me-2 m-auto" aria-label={t.close} onClick={() => setToast(null)} />
            </div>
          </div>
        </div>
      ) : null}

      <div id="notificationArea" className="notification-area">
        {showReadyNotification && hasReadyItems ? (
          <div className="notification-item">
            <div className="d-flex align-items-start">
              <i className="bi bi-check-circle-fill text-success fs-4 me-2" />
              <div className="flex-grow-1">
                <div className="fw-bold">Món ăn sẵn sàng!</div>
                <div className="small text-muted">
                  {activeReadyNotification?.dishName
                    ? `${activeReadyNotification.dishName} đã sẵn sàng cho bàn ${safeData.tableContext.tableNumber ?? safeData.tableContext.tableId}.`
                    : `Bếp đã chuẩn bị xong món ăn cho bàn ${safeData.tableContext.tableNumber ?? safeData.tableContext.tableId}.`}
                </div>
              </div>
              <button type="button" className="btn btn-sm btn-link text-muted p-0" onClick={() => setShowReadyNotification(false)}>
                <i className="bi bi-x-lg" />
              </button>
            </div>
          </div>
        ) : null}
      </div>

      <div className="header menu-hero">
        <div className="container">
          <div className="row align-items-center">
            <div className="col-6">
              <h1 className="mb-0 h3">Self Restaurant</h1>
              <small className="opacity-75 d-block mb-1">{t.branch} {safeData.menu.branchName}</small>
              {order.data?.hasActiveDiningSession ? (
                <span className="soft-badge warning mb-2">
                  <i className="bi bi-cup-hot me-1" />
                  Phiên bàn đang hoạt động
                </span>
              ) : null}
              <Link to="/Home/Index" className="btn btn-outline-light btn-sm">
                <i className="bi bi-house-door me-1" />
                {t.home}
              </Link>
            </div>
            <div className="col-6 text-end">
              <div className="small opacity-75">{t.table}</div>
              <div className="display-5 fw-bold">{safeData.tableContext.tableNumber ?? safeData.tableContext.tableId}</div>
            </div>
          </div>
        </div>
      </div>

      <div className="container menu-main-container">
        <div className="row">
          <div className="col-lg-8 mb-4">
            <div id="topDishesWrapper" className="mb-4 premium-menu-section">
              <div className="menu-section-card menu-section-card-featured">
                <div className="d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3">
                  <h5 className="mb-0">
                    <i className="bi bi-star-fill text-warning me-1" />
                    {t.top}
                  </h5>
                  <small className="text-muted">{t.topSub}</small>
                </div>
                <div className="row g-3 menu-dish-grid" id="topDishesSection">
                  {recommendedDishes.length > 0 ? recommendedDishes.map(({ dish, reason }) => (
                    <div key={`top-${dish.dishId}`} className="col-xl-4 col-lg-4 col-md-6">
                      <article className={`card dish-card ${dish.available ? "" : "dish-card-unavailable"}`}>
                        <div className="position-relative">
                          <img className="dish-image" src={resolveDishImage(dish.image, dish.name)} alt={normalizeDishText(dish.name)} onError={handleDishImageError} />
                          {dish.isVegetarian ? <span className="badge-vegetarian"><i className="bi bi-leaf" /> {t.vegBadge}</span> : null}
                          {topSellerDishIds.includes(dish.dishId) ? <span className="badge-top-seller">{t.topBadge}</span> : null}
                        </div>
                        <div className="card-body">
                          <h5 className="card-title">{normalizeDishText(dish.name)}</h5>
                          <div className="dish-suggestion-chip">{reason}</div>
                          <p className="card-text text-muted small">{normalizeDishText(dish.description) || t.cardDescFallback}</p>
                          <div className="d-flex justify-content-between align-items-center">
                            <div>
                              <div className="price">{vnd(dish.price)}</div>
                              <small className="text-muted">{normalizeDishText(dish.unit) || "Phần"}</small>
                            </div>
                            <button type="button" className="btn-add" disabled={!dish.available} onClick={() => addDishToCart(dish.dishId)}>
                              <i className="bi bi-plus-lg me-1" />
                              {dish.available ? t.add : t.unavailable}
                            </button>
                          </div>
                          <button type="button" className="btn btn-link p-0 mt-2 small dish-detail-link" onClick={() => setSelectedDish(dish)}>
                            <i className="bi bi-info-circle me-1" />
                            {t.detail}
                          </button>
                        </div>
                      </article>
                    </div>
                  )) : (
                    <div className="col-12 text-muted small">
                      {t.topEmpty}
                    </div>
                  )}
                </div>
              </div>
            </div>

            <div className="menu-filter-panel mb-4">
                <div className="d-flex flex-wrap justify-content-between align-items-center gap-3">
                  <div className="d-flex flex-wrap flex-grow-1 gap-3 align-items-center">
                    <div className="d-flex flex-wrap category-tabs" id="categoryTabs">
                    {visibleCategories.map((category) => (
                      <button
                        type="button"
                        key={category.categoryId}
                        className={`category-tab ${activeCategoryId === category.categoryId ? "active" : ""}`}
                        data-category={category.categoryId}
                        onClick={() => setActiveCategoryId(category.categoryId)}
                      >
                        {category.categoryName}
                      </button>
                    ))}
                  </div>
                  <div className="flex-grow-1 menu-search-wrap">
                    <i className="bi bi-search" />
                    <input
                      type="text"
                      id="dishSearchInput"
                      className="form-control form-control-sm menu-search-input"
                      value={search}
                      onChange={(e) => setSearch(e.target.value)}
                      placeholder={t.search}
                    />
                  </div>
                </div>
                <button type="button" id="vegFilterBtn" className={`btn btn-sm veg-toggle-btn ${vegOnly ? "active" : ""}`} onClick={() => setVegOnly((value) => !value)}>
                  <i className="bi bi-leaf me-1" />
                  {t.veg}
                </button>
              </div>
            </div>

            <div className="row g-3 menu-dish-grid" id="dishesGrid">
              {categories.flatMap((category) => category.dishes.map((dish) => (
                <div key={dish.dishId} className="col-xl-4 col-md-6">
                  <article className={`card dish-card ai-recommendation-card ${dish.available ? "" : "dish-card-unavailable"}`}>
                    <div className="position-relative">
                      <img className="dish-image" src={resolveDishImage(dish.image, dish.name)} alt={normalizeDishText(dish.name)} onError={handleDishImageError} />
                      {dish.isVegetarian ? <span className="badge-vegetarian"><i className="bi bi-leaf" /> {t.vegBadge}</span> : null}
                      {topSellerDishIds.includes(dish.dishId) ? <span className="badge-top-seller">{t.topBadge}</span> : null}
                      {dish.isDailySpecial ? <span className="badge-special">{t.today}</span> : null}
                      {!dish.available ? <span className="badge-unavailable">{t.unavailable}</span> : null}
                    </div>
                    <div className="card-body">
                      <h5 className="card-title">{normalizeDishText(dish.name)}</h5>
                      <div className="dish-suggestion-chip">
                        <i className="bi bi-grid-3x3-gap me-1" />
                        {t.categoryChip}: {category.categoryName}
                      </div>
                      <p className="card-text text-muted small">{normalizeDishText(dish.description) || t.cardDescFallback}</p>
                      {!dish.available ? <div className="dish-unavailable-text">{t.unavailableHint}</div> : null}
                      <div className="d-flex justify-content-between align-items-center">
                        <div>
                          <div className="price">{vnd(dish.price)}</div>
                          <small className="text-muted">{normalizeDishText(dish.unit) || "Phần"}</small>
                        </div>
                        <button type="button" className="btn-add" disabled={!dish.available} onClick={() => addDishToCart(dish.dishId)}>
                          <i className="bi bi-plus-lg me-1" />
                          {dish.available ? t.add : t.unavailable}
                        </button>
                      </div>
                      <button type="button" className="btn btn-link p-0 mt-2 small dish-detail-link" onClick={() => setSelectedDish(dish)}>
                        <i className="bi bi-info-circle me-1" />
                        {t.detail}
                      </button>
                    </div>
                  </article>
                </div>
              )))}
              {categories.length === 0 ? (
                <div className="col-12">
                  <div className="empty-cart">
                    <i className="bi bi-inbox" />
                    <p>{t.noDish}</p>
                  </div>
                </div>
              ) : null}
            </div>
          </div>

          <div className="col-lg-4">
            <div className="cart-sidebar">
              <div className="cart-sidebar-header d-flex justify-content-between align-items-center mb-4">
                <h4 className="mb-0"><i className="bi bi-cart3" style={{ color: "var(--primary-color)" }} /> {t.cart}</h4>
                <span className="cart-badge" id="cartBadge">{totalCartCount}</span>
              </div>

              {items.length === 0 ? (
                <div className="empty-cart">
                  <i className="bi bi-cart3" />
                  <p className="mb-1">{t.empty}</p>
                  <small>{t.emptyHint}</small>
                </div>
              ) : (
                <>
                  <div id="cartItems" className="cart-items-scroll">
                  {pending.length > 0 ? (
                    <div className="mb-3">
                      <small className="text-muted fw-bold"><i className="bi bi-clock-history me-1" />{t.pending}</small>
                      {pending.map((item) => (
                        <div key={item.itemId} className="cart-item cart-item-pending">
                          <div className="d-flex justify-content-between align-items-start mb-2">
                            <div className="flex-grow-1">
                              <div className="fw-bold">{item.dishName}</div>
                              <small className="text-muted">{line(item.unitPrice, item.quantity)}</small>
                              <div className="mt-1 d-flex flex-wrap gap-2 align-items-center">
                                {badge("pending")}
                                {(!menuDishLookup.get(item.dishId)?.available) ? <span className="badge-unavailable-inline">{t.unavailable}</span> : null}
                              </div>
                            </div>
                            <button
                              type="button"
                              className="btn btn-link text-danger p-0 menu-link-btn"
                              onClick={() => {
                                if (hasGuestCart && activeTableContext) {
                                  setGuestCartItems(removeGuestCartItem(activeTableContext, item.itemId));
                                  return;
                                }

                                removeItem.mutate(item.itemId);
                              }}
                            >
                              <i className="bi bi-trash" />
                            </button>
                          </div>
                          <input
                            type="text"
                            className="form-control form-control-sm note-input mb-2"
                            placeholder={t.note}
                            defaultValue={item.note ?? ""}
                            onBlur={(e) => {
                              if (hasGuestCart && activeTableContext) {
                                setGuestCartItems(updateGuestCartItemNote(activeTableContext, item.itemId, e.target.value));
                                return;
                              }

                              updateNote.mutate({ itemId: item.itemId, note: e.target.value });
                            }}
                          />
                          <div className="d-flex justify-content-between align-items-center">
                            <div className={`quantity-control ${menuDishLookup.get(item.dishId)?.available === false ? "disabled" : ""}`}>
                              <button
                                type="button"
                                onClick={() => {
                                  const nextQuantity = Math.max(1, item.quantity - 1);
                                  if (hasGuestCart && activeTableContext) {
                                    setGuestCartItems(updateGuestCartItemQuantity(activeTableContext, item.itemId, nextQuantity));
                                    return;
                                  }

                                  bumpQty.mutate({ itemId: item.itemId, quantity: nextQuantity });
                                }}
                              ><i className="bi bi-dash" /></button>
                              <span className="px-2 fw-bold">{item.quantity}</span>
                              <button
                                type="button"
                                disabled={menuDishLookup.get(item.dishId)?.available === false}
                                onClick={() => {
                                  const nextQuantity = item.quantity + 1;
                                  if (hasGuestCart && activeTableContext) {
                                    setGuestCartItems(updateGuestCartItemQuantity(activeTableContext, item.itemId, nextQuantity));
                                    return;
                                  }

                                  bumpQty.mutate({ itemId: item.itemId, quantity: nextQuantity });
                                }}
                              ><i className="bi bi-plus" /></button>
                            </div>
                            <strong>{vnd(item.lineTotal)}</strong>
                          </div>
                        </div>
                      ))}
                    </div>
                  ) : null}

                  {kitchen.length > 0 ? (
                    <div className="mb-3">
                      <small className="text-muted fw-bold"><i className="bi bi-fire me-1" />{t.kitchen}</small>
                      {kitchen.map((item) => (
                        <div key={item.itemId} className="cart-item">
                          <div className="d-flex justify-content-between align-items-start mb-2">
                            <div className="flex-grow-1">
                              <div className="fw-bold">{item.dishName}</div>
                              <small className="text-muted">{line(item.unitPrice, item.quantity)}</small>
                              <div className="mt-1">{badge("kitchen")}</div>
                            </div>
                            <strong>{vnd(item.lineTotal)}</strong>
                          </div>
                          <small className="text-muted">{item.note || t.noNote}</small>
                        </div>
                      ))}
                    </div>
                  ) : null}

                    {ready.length > 0 ? (
                      <div className="mb-3">
                        <small className="text-muted fw-bold"><i className="bi bi-check-circle me-1" />{t.ready}</small>
                        {ready.map((item) => (
                          <div key={item.itemId} className="cart-item ready">
                          <div className="d-flex justify-content-between align-items-start">
                            <div>
                              <div className="fw-bold">{item.dishName}</div>
                              <small className="text-muted">{line(item.unitPrice, item.quantity)}</small>
                            </div>
                            {badge("ready")}
                          </div>
                        </div>
                        ))}
                      </div>
                    ) : null}

                    {received.length > 0 ? (
                      <div className="mb-3">
                        <small className="text-muted fw-bold"><i className="bi bi-bag-check me-1" />{t.received}</small>
                        {received.map((item) => (
                          <div key={item.itemId} className="cart-item">
                            <div className="d-flex justify-content-between align-items-start">
                              <div>
                                <div className="fw-bold">{item.dishName}</div>
                                <small className="text-muted">{line(item.unitPrice, item.quantity)}</small>
                              </div>
                              {badge("received")}
                            </div>
                          </div>
                        ))}
                      </div>
                    ) : null}
                    </div>

                  <div id="cartSummary" className="cart-summary-panel">
                    <hr />
                    <button type="button" className="btn-view-bill" onClick={() => setShowBill(true)}><i className="bi bi-receipt me-2" />{t.billBtn}</button>
                    <div className="d-flex justify-content-between mb-3">
                      <span className="h5 fw-bold">{t.total}</span>
                      <span id="totalAmount" className="h4 fw-bold" style={{ color: "var(--primary-color)" }}>{vnd(subtotal)}</span>
                    </div>
                    {hasUnavailablePendingItems ? (
                      <div className="alert alert-warning small py-2 mb-3">
                        <i className="bi bi-exclamation-triangle me-2" />
                        {t.unavailableCartHint}
                        {unavailablePendingNames.length > 0 ? ` (${unavailablePendingNames.join(", ")})` : ""}
                      </div>
                    ) : null}
                    {customerName ? (
                      <div id="loyaltyInfo" className="bg-warning bg-opacity-10 border border-warning rounded p-3 mb-3">
                        <div className="d-flex align-items-center small">
                          <i className="bi bi-person-circle me-2" />
                          <span className="fw-bold" id="customerName">{customerName}</span>
                        </div>
                        <div className="text-muted small">
                          {t.points} +<span id="loyaltyPoints">{currentLoyaltyPoints}</span> {t.pointsSuffix}
                        </div>
                      </div>
                    ) : null}
                    <div className="mb-3">
                      <button
                        type="button"
                        id="btnSendKitchen"
                        className="btn btn-success w-100 py-3"
                        disabled={submitOrder.isPending || pending.length === 0 || orderStatus !== "PENDING" || hasUnavailablePendingItems}
                        onClick={() => {
                          if (!activeTableContext) {
                            return;
                          }

                          if (!isAuthenticated) {
                            savePendingSubmitIntent(activeTableContext);
                            const returnUrl = `/Menu/Index?tableId=${activeTableContext.tableId}&branchId=${activeTableContext.branchId}`;
                            navigate(`/Customer/Login?returnUrl=${encodeURIComponent(returnUrl)}`);
                            return;
                          }

                          setShowSendConfirm(true);
                        }}
                      >
                        <i className="bi bi-check-circle me-2" />
                        {t.send}
                      </button>
                      <div className="text-center text-muted small mt-1">
                        <i className="bi bi-info-circle me-1" />
                        {isAuthenticated ? t.sendHint : t.loginToSend}
                      </div>
                    </div>
                    <button
                      type="button"
                      className="btn-checkout"
                      onClick={() => {
                        if (ready.length === 0 && received.length === 0) {
                          setToast({
                            type: "info",
                            message: pending.length > 0
                              ? t.checkoutPendingMessage
                              : kitchen.length > 0
                                ? t.checkoutPreparingMessage
                                : t.checkoutEmptyMessage,
                          });
                          return;
                        }
                        setShowCheckoutNotice(true);
                      }}
                    ><i className="bi bi-credit-card me-2" />{t.checkout}</button>
                    <div className="text-center text-muted small mt-2"><i className="bi bi-clock me-1" />{t.checkoutHint}</div>
                  </div>
                </>
              )}
              <button type="button" className="btn btn-outline-danger w-100 mt-3" onClick={() => setShowResetConfirm(true)}>
                <i className="bi bi-arrow-repeat me-2" />
                {t.reset}
              </button>
            </div>
          </div>
        </div>
      </div>

      {showSendConfirm ? (
        <div className="modal fade show d-block menu-static-modal" tabIndex={-1} aria-modal="true" role="dialog" onClick={() => setShowSendConfirm(false)}>
          <div className="modal-dialog modal-dialog-centered" onClick={(e) => e.stopPropagation()}>
            <div className="modal-content">
              <div className="modal-header border-0">
                <h5 className="modal-title"><i className="bi bi-exclamation-triangle text-warning me-2" />{t.sendConfirmTitle}</h5>
                <button type="button" className="btn-close" aria-label={t.close} onClick={() => setShowSendConfirm(false)} />
              </div>
              <div className="modal-body">
                <p className="mb-3 text-muted">{t.sendConfirmBody}</p>
                <div className="confirm-list">
                  {pending.map((item) => (
                    <div key={`confirm-${item.itemId}`} className="confirm-item">
                      <div className="d-flex justify-content-between align-items-center">
                        <div className="flex-grow-1">
                          <div className="fw-bold">{item.dishName}</div>
                          <div className="small text-muted">{line(item.unitPrice, item.quantity)}</div>
                          {item.note ? <div className="item-note-display">Ghi chú: {item.note}</div> : null}
                        </div>
                        <div className="fw-bold" style={{ color: "var(--primary-color)" }}>x{item.quantity}</div>
                      </div>
                    </div>
                  ))}
                </div>
                {hasUnavailablePendingItems ? (
                  <div className="alert alert-warning">
                    <i className="bi bi-exclamation-triangle me-2" />
                    {t.unavailableCartHint}
                  </div>
                ) : null}
                <div className="alert alert-info mb-0">
                  <i className="bi bi-info-circle me-2" />
                  <small>{t.sendConfirmHint}</small>
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-secondary" onClick={() => setShowSendConfirm(false)}>{t.cancel}</button>
                <button
                  type="button"
                  className="btn btn-success"
                  disabled={submitOrder.isPending || pending.length === 0 || hasUnavailablePendingItems}
                  onClick={() => submitOrder.mutate({
                    tableId: safeData.tableContext.tableId,
                    branchId: safeData.tableContext.branchId,
                    idempotencyKey: getOrCreatePendingSubmitKey(safeData.tableContext),
                    expectedDiningSessionCode: order.data?.diningSessionCode ?? null,
                    items: pending.map((item) => ({
                      dishId: item.dishId,
                      quantity: item.quantity,
                      note: item.note ?? "",
                    })),
                  }, {
                    onSuccess: async () => {
                      if (hasGuestCart) {
                        clearGuestMenuCart(safeData.tableContext);
                        setGuestCartItems([]);
                      }
                      clearPendingSubmitIntent();
                      setShowSendConfirm(false);
                      await queryClient.invalidateQueries();
                    },
                  })}
                >
                  <i className="bi bi-check-circle me-2" />
                  {t.sendConfirmButton}
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {showBill ? (
        <div className="modal fade show d-block menu-static-modal bill-modal" tabIndex={-1} aria-modal="true" role="dialog" onClick={() => setShowBill(false)}>
          <div className="modal-dialog modal-dialog-centered" onClick={(e) => e.stopPropagation()}>
            <div className="modal-content">
              <div className="bill-header text-center">
                <h3 className="mb-1"><i className="bi bi-receipt" /> {t.bill}</h3>
                <p className="mb-0 small opacity-75">{t.table} {safeData.tableContext.tableNumber ?? safeData.tableContext.tableId} - {t.branch} {safeData.menu.branchName}</p>
                <p className="mb-0 small opacity-75">{t.time}: {new Date().toLocaleString("vi-VN")}</p>
              </div>
              <div className="modal-body">
                {items.map((item) => (
                  <div key={`bill-${item.itemId}`} className="bill-item">
                    <div className="d-flex justify-content-between align-items-start">
                      <div>
                        <strong>{item.dishName}</strong>
                        <div className="text-muted small">{line(item.unitPrice, item.quantity)}</div>
                        {item.note ? <div className="item-note-display">{item.note}</div> : null}
                      </div>
                      <div className="text-end">
                        <div className={`badge small mb-1 ${group(item, orderStatus) === "pending" ? "bg-secondary" : "bg-success"}`}>
                          {group(item, orderStatus) === "pending" ? t.billPending : t.billPlaced}
                        </div>
                        <div><strong>{vnd(item.lineTotal)}</strong></div>
                      </div>
                    </div>
                  </div>
                ))}

                <div className="bill-total">
                  <div className="d-flex justify-content-between mb-2"><span>{t.items}</span><span>{`${totalDishCount} món`}</span></div>
                  <div className="d-flex justify-content-between mb-2"><span>{t.subtotal}</span><span>{vnd(subtotal)}</span></div>
                  <hr />
                  <div className="d-flex justify-content-between"><span className="fw-bold h5 mb-0">{t.total}</span><span className="fw-bold h4 mb-0" style={{ color: "var(--primary-color)" }}>{vnd(subtotal)}</span></div>
                  <div className="text-muted small mt-2 text-center"><i className="bi bi-star-fill text-warning" /> {t.estimatedPoints} +{points} {t.pointsSuffix}</div>
                </div>

                <div className="alert alert-info mt-3 mb-0">
                  <i className="bi bi-info-circle me-2" />
                  <small>{t.checkoutHint}</small>
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-secondary" onClick={() => setShowBill(false)}>{t.close}</button>
                <button
                  type="button"
                  className="btn btn-primary"
                  onClick={() => {
                    if (ready.length === 0 && received.length === 0) {
                      setToast({
                        type: "info",
                        message: pending.length > 0
                          ? t.checkoutPendingMessage
                          : kitchen.length > 0
                            ? t.checkoutPreparingMessage
                            : t.checkoutEmptyMessage,
                      });
                      setShowBill(false);
                      return;
                    }
                    setShowBill(false);
                    setShowCheckoutNotice(true);
                  }}
                >
                  <i className="bi bi-credit-card me-2" />
                  {t.checkout}
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {showCheckoutNotice ? (
        <div className="modal fade show d-block menu-static-modal" tabIndex={-1} aria-modal="true" role="dialog" onClick={() => setShowCheckoutNotice(false)}>
          <div className="modal-dialog modal-dialog-centered" onClick={(e) => e.stopPropagation()}>
            <div className="modal-content text-center">
              <div className="modal-body p-4">
                <div className="mb-3"><i className="bi bi-cash-coin text-primary" style={{ fontSize: "4rem" }} /></div>
                <h4 className="fw-bold mb-3">{t.payNotice}</h4>
                <p className="text-muted mb-4">{t.payBody}</p>
                <div className="bg-light rounded-3 p-3 mb-4 border border-primary border-opacity-25">
                  <small className="text-uppercase text-muted fw-bold">{t.tableCode}</small>
                  <div className="display-4 fw-bold text-primary mt-1">{safeData.tableContext.tableNumber ?? safeData.tableContext.tableId}</div>
                </div>
                <button type="button" className="btn btn-primary w-100 py-2 fw-bold" onClick={() => setShowCheckoutNotice(false)}>
                  <i className="bi bi-check-lg me-2" />
                  {t.understood}
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {showResetConfirm ? (
        <div className="modal fade show d-block menu-static-modal" tabIndex={-1} aria-modal="true" role="dialog" onClick={() => setShowResetConfirm(false)}>
          <div className="modal-dialog modal-dialog-centered" onClick={(e) => e.stopPropagation()}>
            <div className="modal-content">
              <div className="modal-header border-0">
                <h5 className="modal-title"><i className="bi bi-exclamation-triangle text-warning me-2" />{t.resetTitle}</h5>
                <button type="button" className="btn-close" aria-label={t.close} onClick={() => setShowResetConfirm(false)} />
              </div>
              <div className="modal-body">
                <p className="mb-0">{resetConfirmationBody}</p>
              </div>
              <div className="modal-footer border-0">
                <button type="button" className="btn btn-secondary" onClick={() => setShowResetConfirm(false)}>{t.cancel}</button>
                <button type="button" className="btn btn-danger" disabled={clearTable.isPending} onClick={() => clearTable.mutate(undefined, { onSuccess: async () => { setShowResetConfirm(false); await queryClient.invalidateQueries(); navigate("/Home/Index"); } })}>
                  <i className="bi bi-arrow-repeat me-2" />
                  {t.resetConfirm}
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {selectedDish ? (
        <div className="modal fade show d-block menu-static-modal dish-detail-overlay" tabIndex={-1} aria-modal="true" role="dialog" onClick={() => setSelectedDish(null)}>
          <div className="modal-dialog modal-dialog-centered menu-dish-modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-content">
              <div className="modal-header dish-detail-header">
                <h5 className="modal-title">
                  {normalizeDishText(selectedDish.name)}
                  {selectedDish.isVegetarian ? <span className="badge bg-success ms-2">{t.vegBadge}</span> : null}
                </h5>
                <button type="button" className="btn-close" aria-label={t.close} onClick={() => setSelectedDish(null)} />
              </div>
              <div className="modal-body dish-detail-body">
                <div className="mb-3 text-center dish-detail-media">
                  <img
                    className="img-fluid rounded menu-dish-modal-image"
                    src={resolveDishImage(selectedDish.image, selectedDish.name)}
                    alt={normalizeDishText(selectedDish.name)}
                    onError={handleDishImageError}
                  />
                </div>
                {!selectedDish.available ? (
                  <div className="alert alert-warning small">
                    <i className="bi bi-exclamation-triangle me-2" />
                    {t.unavailableHint}
                  </div>
                ) : null}
                <p className="text-muted dish-detail-description">{normalizeDishText(selectedDish.description) || t.descFallback}</p>
                <hr />
                <h6 className="fw-bold mb-2">Thành phần</h6>
                {selectedDish.ingredients && selectedDish.ingredients.length > 0 ? (
                  <ul className="small mb-0 dish-detail-ingredients">
                    {selectedDish.ingredients.map((ingredient) => (
                      <li key={`${selectedDish.dishId}-${ingredient.name}`}>
                        {normalizeDishText(ingredient.name)}: <strong>{ingredient.quantity.toLocaleString("vi-VN")} {normalizeDishText(ingredient.unit)}</strong>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <div className="text-muted small">{t.ingredientsFallback}</div>
                )}
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-secondary" onClick={() => setSelectedDish(null)}>{t.close}</button>
                <button
                  type="button"
                  className="btn btn-primary"
                  disabled={!selectedDish.available}
                  onClick={() => {
                    if (!activeTableContext) {
                      return;
                    }

                    if (hasGuestCart || !isAuthenticated || (Boolean(order.data?.hasActiveDiningSession) && !hasPendingRound)) {
                      setGuestCartItems(addDishToGuestCart(activeTableContext, selectedDish, 1));
                      setSelectedDish(null);
                      return;
                    }

                    addItem.mutate({
                      dishId: selectedDish.dishId,
                      quantity: 1,
                      expectedDiningSessionCode: order.data?.diningSessionCode ?? null,
                    });
                    setSelectedDish(null);
                  }}
                >
                  <i className="bi bi-plus-lg me-1" />
                  {t.quickAdd}
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {showReadyModal && isReadyLikeStatus ? (
        <div className="modal fade show d-block menu-static-modal" tabIndex={-1} aria-modal="true" role="dialog" onClick={() => {
          markReadyDismissed(activeReadyKeys);
          void resolveReadyNotificationIds(activeReadyNotificationIds);
          setShowReadyModal(false);
        }}>
          <div className="modal-dialog modal-dialog-centered" onClick={(e) => e.stopPropagation()}>
            <div className="modal-content">
              <div className="modal-header border-0">
                <h5 className="modal-title"><i className="bi bi-bell-fill text-success me-2" />{t.readyTitle}</h5>
                <button
                  type="button"
                  className="btn-close"
                  aria-label={t.close}
                  onClick={() => {
                    markReadyDismissed(activeReadyKeys);
                    void resolveReadyNotificationIds(activeReadyNotificationIds);
                    setShowReadyModal(false);
                  }}
                />
              </div>
              <div className="modal-body">
                <p className="mb-3">
                  Bếp đã hoàn thành món ăn cho bàn <strong>{safeData.tableContext.tableNumber ?? safeData.tableContext.tableId}</strong>.
                  {" "}Vui lòng đến quầy lấy món. Sau khi đã nhận món, hãy nhấn <strong>"Tôi đã nhận món"</strong> để xác nhận.
                </p>
                <div className="alert alert-success small mb-0">
                  <i className="bi bi-info-circle me-1" />
                  {t.readyHint}
                </div>
                {activeReadyNotifications.length > 1 ? (
                  <ul className="small mt-3 mb-0">
                    {activeReadyNotifications.map((notification) => (
                      <li key={readyNotificationKey(notification)}>
                        {notification.message || notification.dishName || `Đơn #${notification.orderId ?? ""}`}
                      </li>
                    ))}
                  </ul>
                ) : null}
              </div>
              <div className="modal-footer border-0">
                <button
                  type="button"
                  className="btn btn-outline-secondary"
                  onClick={() => {
                    markReadyDismissed(activeReadyKeys);
                    void resolveReadyNotificationIds(activeReadyNotificationIds);
                    setShowReadyModal(false);
                  }}
                >
                  {t.later}
                </button>
                <button
                  type="button"
                  className="btn btn-success"
                  disabled={confirmReceived.isPending || resolveNotification.isPending}
                  onClick={() => {
                    const orderId = currentReadyOrderId;
                    const alreadyReceived = !hasReadyItems || hasReceivedItems || ["SERVING", "SERVED", "COMPLETED"].includes(orderStatus);
                    markReadyDismissed(activeReadyKeys);
                    setShowReadyModal(false);
                    void (async () => {
                      try {
                        if (orderId && !alreadyReceived) {
                          await confirmReceived.mutateAsync(orderId);
                        }
                        await resolveReadyNotificationIds(activeReadyNotificationIds);
                        await queryClient.invalidateQueries({ queryKey: ["order"] });
                        await queryClient.invalidateQueries({ queryKey: ["orderItems"] });
                        await queryClient.invalidateQueries({ queryKey: ["readyNotifications"] });
                        setToast({
                          type: "success",
                          message: "Đã xác nhận nhận món. Chúc ngon miệng!",
                        });
                      } catch (err) {
                        if (alreadyReceived) {
                          return;
                        }
                        const fallbackMessage = "Không thể xác nhận nhận món lúc này.";
                        const errorMessage = err instanceof Error && err.message.trim()
                          ? err.message
                          : fallbackMessage;
                        setToast({
                          type: "info",
                          message: errorMessage,
                        });
                      }
                    })();
                  }}
                >
                  <i className="bi bi-check2-circle me-2" />
                  {t.confirmReceived}
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}

