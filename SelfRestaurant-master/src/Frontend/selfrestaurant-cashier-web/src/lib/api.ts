import type {
  ApiError,
  CashierAccountDto,
  CashierCheckoutResultDto,
  CashierDashboardDto,
  StaffServiceRequestDto,
  CashierHistoryDto,
  CashierReportScreenDto,
  CashierReservationCheckInResultDto,
  ReservationDto,
  StaffSessionDto,
} from "./types";

const jsonHeaders = { "Content-Type": "application/json" };

export type OnsitePayment = {
  paymentId: number; paymentCode: string; amount: number; currency: string;
  method: string; location: string; status: string; diningSessionId: number;
  diningSessionCode: string; tableId: number; splitPart: string | null; isCurrentAttempt: boolean;
};

async function request<T>(input: string, init?: RequestInit): Promise<T> {
  const response = await fetch(input, {
    credentials: "include",
    headers: init?.body ? { ...jsonHeaders, ...(init.headers ?? {}) } : init?.headers,
    ...init,
  });

  const text = await response.text();
  const payload = text ? JSON.parse(text) : null;
  if (!response.ok) {
    const error = payload as ApiError | null;
    throw new Error(error?.message ?? `Request failed: ${response.status}`);
  }
  return payload as T;
}

export const cashierApi = {
  getOnsitePayments: () => request<OnsitePayment[]>("/api/gateway/staff/cashier/onsite-payments"),
  confirmOnsitePayment: (id: number) => request<{ paymentId: number; status: string }>(
    `/api/gateway/staff/cashier/onsite-payments/${id}/confirm`, { method: "POST", body: "{}" }),
  getSession: () => request<StaffSessionDto>("/api/gateway/staff/cashier/session"),
  login: (username: string, password: string) =>
    request<{ success: boolean; nextPath?: string; session: StaffSessionDto }>("/api/gateway/staff/auth/login", {
      method: "POST",
      body: JSON.stringify({ username, password }),
    }),
  logout: () => request<{ success: boolean; nextPath?: string }>("/api/gateway/staff/cashier/auth/logout", { method: "POST", body: JSON.stringify({}) }),
  getDashboard: () => request<CashierDashboardDto>("/api/gateway/staff/cashier/dashboard"),
  getServiceRequests: () => request<StaffServiceRequestDto[]>("/api/gateway/staff/cashier/service-requests"),
  acknowledgeServiceRequest: (id: number) => request<StaffServiceRequestDto>(`/api/gateway/staff/cashier/service-requests/${id}/acknowledge`, { method: "POST", body: "{}" }),
  completeServiceRequest: (id: number) => request<StaffServiceRequestDto>(`/api/gateway/staff/cashier/service-requests/${id}/complete`, { method: "POST", body: "{}" }),
  getHistory: () => request<CashierHistoryDto>("/api/gateway/staff/cashier/history"),
  getReport: (date?: string) => request<CashierReportScreenDto>(`/api/gateway/staff/cashier/report${date ? `?date=${date}` : ""}`),
  getTodayReservations: (branchId?: number | null, status?: string) => {
    const params = new URLSearchParams();
    if (branchId && branchId > 0) params.set("branchId", String(branchId));
    if (status && status !== "ALL") params.set("status", status);
    const suffix = params.size > 0 ? `?${params.toString()}` : "";
    return request<ReservationDto[]>(`/api/cashier/reservations/today${suffix}`);
  },
  checkInReservation: (reservationId: number, payload: { tableId?: number | null; tableIds?: number[] }) =>
    request<CashierReservationCheckInResultDto>(`/api/cashier/reservations/${reservationId}/check-in`, { method: "POST", body: JSON.stringify({ tableId: payload.tableId ?? null, tableIds: payload.tableIds ?? [] }) }),
  checkout: (orderId: number, payload: { discount?: number; pointsUsed?: number; paymentMethod?: string; paymentAmount?: number; idempotencyKey: string }) =>
    request<CashierCheckoutResultDto>(`/api/gateway/staff/cashier/orders/${orderId}/checkout`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  updateAccount: (payload: { name: string; phone: string; email?: string }) =>
    request<CashierAccountDto>("/api/gateway/staff/cashier/account", { method: "PUT", body: JSON.stringify(payload) }),
  changePassword: (payload: { currentPassword: string; newPassword: string; confirmPassword: string }) =>
    request<{ success: boolean; message: string }>("/api/gateway/staff/cashier/change-password", { method: "POST", body: JSON.stringify(payload) }),
};
