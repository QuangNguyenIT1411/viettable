import type {
  ApiError,
  ChefCategoryDto,
  ChefDashboardDto,
  ChefDishIngredientsDto,
  StaffForgotPasswordResultDto,
  StaffSessionDto,
} from "./types";

const jsonHeaders = { "Content-Type": "application/json" };

type ApiIssue = {
  ingredientName?: string;
  requiredQuantity?: number;
  availableQuantity?: number;
  unit?: string | null;
};

const API_TEXT_MAP: Record<string, string> = {
  "Ban can dang nhap bang tai khoan bep.": "Bạn cần đăng nhập bằng tài khoản bếp.",
  "Da cap nhat thong tin mon an.": "Đã cập nhật thông tin món ăn.",
  "Da them mon moi.": "Đã thêm món mới.",
  "Da luu nguyen lieu mon.": "Đã lưu nguyên liệu món.",
  "Nguyen lieu": "Nguyên liệu",
  "Phan": "Phần",
  "Đã có lỗi xảy ra.": "Đã có lỗi xảy ra.",
  "Nguyên liệu": "Nguyên liệu",
  "cần": "cần",
  "hiện còn": "hiện còn",
  "Phần": "Phần",
};

function normalizeApiText(value?: string | null): string {
  if (!value) return "";

  let normalized = value.replace(/\\u([0-9a-fA-F]{4})/g, (_, hex) =>
    String.fromCharCode(Number.parseInt(hex, 16)),
  );

  for (const [source, target] of Object.entries(API_TEXT_MAP)) {
    normalized = normalized.split(source).join(target);
  }

  return normalized.trim();
}

function formatApiError(error: ApiError | null): string {
  const baseMessage = normalizeApiText(error?.message) || "Đã có lỗi xảy ra.";
  const issues = Array.isArray((error as { issues?: ApiIssue[] } | null)?.issues)
    ? ((error as { issues?: ApiIssue[] }).issues ?? [])
    : [];

  if (issues.length === 0) {
    return baseMessage;
  }

  const details = issues
    .map((issue) => {
      const ingredientName = normalizeApiText(issue.ingredientName) || "Nguyên liệu";
      const required = typeof issue.requiredQuantity === "number"
        ? issue.requiredQuantity.toLocaleString("vi-VN")
        : "?";
      const available = typeof issue.availableQuantity === "number"
        ? issue.availableQuantity.toLocaleString("vi-VN")
        : "?";
      const normalizedUnit = normalizeApiText(issue.unit);
      const unit = normalizedUnit ? ` ${normalizedUnit}` : "";
      return `- ${ingredientName}: cần ${required}${unit}, hiện còn ${available}${unit}`;
    })
    .join("\n");

  return `${baseMessage}\n${details}`;
}

async function request<T>(input: string, init?: RequestInit): Promise<T> {
  const response = await fetch(input, {
    credentials: "include",
    headers: init?.body ? { ...jsonHeaders, ...(init.headers ?? {}) } : init?.headers,
    ...init,
  });

  const text = await response.text();
  let payload: unknown = null;
  if (text) {
    try {
      payload = JSON.parse(text);
    } catch {
      payload = text;
    }
  }
  if (!response.ok) {
    if (payload && typeof payload === "object") {
      const error = payload as ApiError | null;
      throw new Error(formatApiError(error) || `Request failed: ${response.status}`);
    }
    throw new Error(normalizeApiText(String(payload)) || `Request failed: ${response.status}`);
  }
  return payload as T;
}

export const chefApi = {
  getSession: () => request<StaffSessionDto>("/api/gateway/staff/session"),
  login: (username: string, password: string) =>
    request<{ success: boolean; nextPath?: string; session: StaffSessionDto }>("/api/gateway/staff/auth/login", {
      method: "POST",
      body: JSON.stringify({ username, password }),
    }),
  forgotPassword: (email: string) =>
    request<StaffForgotPasswordResultDto>("/api/gateway/staff/auth/forgot-password", {
      method: "POST",
      body: JSON.stringify({ email }),
    }),
  validateResetPasswordToken: (token: string) =>
    request<{ valid: true }>(`/api/gateway/staff/auth/reset-password/validate?token=${encodeURIComponent(token)}`),
  resetPassword: (payload: { token: string; newPassword: string; confirmPassword: string }) =>
    request<{ success: boolean; message: string; nextPath?: string }>("/api/gateway/staff/auth/reset-password", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  logout: () =>
    request<{ success: boolean; nextPath?: string }>("/api/gateway/staff/auth/logout", {
      method: "POST",
      body: JSON.stringify({}),
    }),
  getDashboard: () => request<ChefDashboardDto>("/api/gateway/staff/chef/dashboard"),
  getCategories: () => request<ChefCategoryDto[]>("/api/gateway/staff/chef/categories"),
  startOrder: (orderId: number) =>
    request<{ success: boolean; message: string }>(`/api/gateway/staff/chef/orders/${orderId}/start`, {
      method: "POST",
      body: JSON.stringify({}),
    }),
  startItem: (orderId: number, itemId: number) =>
    request<{ success: boolean; message: string }>(`/api/gateway/staff/chef/orders/${orderId}/items/${itemId}/start`, {
      method: "POST",
      body: JSON.stringify({}),
    }),
  readyOrder: (orderId: number) =>
    request<{ success: boolean; message: string }>(`/api/gateway/staff/chef/orders/${orderId}/ready`, {
      method: "POST",
      body: JSON.stringify({}),
    }),
  readyItem: (orderId: number, itemId: number) =>
    request<{ success: boolean; message: string }>(`/api/gateway/staff/chef/orders/${orderId}/items/${itemId}/ready`, {
      method: "POST",
      body: JSON.stringify({}),
    }),
  cancelOrder: (orderId: number, reason: string) =>
    request<{ success: boolean; message: string }>(`/api/gateway/staff/chef/orders/${orderId}/cancel`, {
      method: "POST",
      body: JSON.stringify({ reason }),
    }),
  cancelItem: (orderId: number, itemId: number, reason: string) =>
    request<{ success: boolean; message: string }>(`/api/gateway/staff/chef/orders/${orderId}/items/${itemId}/cancel`, {
      method: "POST",
      body: JSON.stringify({ reason }),
    }),
  updateItemNote: (orderId: number, itemId: number, note: string, append = true) =>
    request<{ success: boolean; message: string }>(`/api/gateway/staff/chef/orders/${orderId}/items/${itemId}/note`, {
      method: "PATCH",
      body: JSON.stringify({ note, append }),
    }),
  getDishIngredients: (dishId: number) =>
    request<ChefDishIngredientsDto>(`/api/gateway/staff/chef/dishes/${dishId}/ingredients`),
  getOrderItemIngredients: (orderId: number, itemId: number) =>
    request<ChefDishIngredientsDto>(`/api/gateway/staff/chef/orders/${orderId}/items/${itemId}/ingredients`),
  saveOrderItemIngredients: (orderId: number, itemId: number, items: { ingredientId: number; ingredientName: string; unit: string; quantity: number }[], note?: string) =>
    request<{ success: boolean; message: string }>(`/api/gateway/staff/chef/orders/${orderId}/items/${itemId}/ingredients`, {
      method: "PUT",
      body: JSON.stringify({ items, note }),
    }),
  updateAccount: (payload: { name: string; phone: string; email?: string }) =>
    request<StaffSessionDto["staff"]>("/api/gateway/staff/chef/account", {
      method: "PUT",
      body: JSON.stringify(payload),
    }),
  changePassword: (payload: { currentPassword: string; newPassword: string; confirmPassword: string }) =>
    request<{ success: boolean; message: string }>("/api/gateway/staff/chef/change-password", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  setDishAvailability: async (dishId: number, available: boolean) => {
    const result = await request<{ success: boolean; message: string; available: boolean }>(
      `/api/gateway/staff/chef/dishes/${dishId}/availability`,
      {
        method: "POST",
        body: JSON.stringify({ available }),
      },
    );
    if (result.available !== available) {
      throw new Error(result.message || "Không thể cập nhật trạng thái bán của món.");
    }
    return result;
  },
};
