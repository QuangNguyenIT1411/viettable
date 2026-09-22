import type { AdminCategoriesScreenDto, AdminCustomerDto, AdminCustomersScreenDto, AdminDishesScreenDto, AdminEmployeeDto, AdminEmployeeHistoryResponse, AdminEmployeesScreenDto, AdminIngredientBatchDto, AdminIngredientDto, AdminIngredientsScreenDto, AdminRelatedIngredientDishDto, AdminReportsScreenDto, AdminSettingsDto, AdminTablesScreenDto, ApiError, StaffSessionDto, AdminDishIngredientsDto, AdminDishIngredientLineDto, AdminDashboardDto, AdminUnitsScreenDto, AdminUnitDto, CreateIngredientBatchRequest, IngredientStockMovementDto, InventoryBatchDto, InventoryMovementDto, InventoryStockInRequest, InventoryStockOutRequest, InventorySummaryDto, Paged, UpdateIngredientBatchRequest, AdminCustomerOrderHistoryItemDto } from "./types";

const jsonHeaders = { "Content-Type": "application/json" };

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
    throw new Error(error?.message ?? `Yêu cầu thất bại: ${response.status}`);
  }
  return payload as T;
}


async function requestForm<T>(input: string, formData: FormData, method = "POST"): Promise<T> {
  const response = await fetch(input, {
    method,
    body: formData,
    credentials: "include",
  });

  const text = await response.text();
  const payload = text ? JSON.parse(text) : null;
  if (!response.ok) {
    const error = payload as ApiError | null;
    throw new Error(error?.message ?? `Yêu cầu thất bại: ${response.status}`);
  }
  return payload as T;
}

function toDishFormData(payload: Record<string, unknown>, imageFile?: File | null): FormData {
  const form = new FormData();
  Object.entries(payload).forEach(([key, value]) => {
    if (value === undefined || value === null) return;
    form.append(key, String(value));
  });
  if (imageFile) form.append("imageFile", imageFile);
  return form;
}

export const adminApi = {
  getSession: () => request<StaffSessionDto>("/api/gateway/admin/session"),
  login: (username: string, password: string) => request<{ success: boolean; nextPath?: string; session: StaffSessionDto }>("/api/gateway/staff/auth/login", { method: "POST", body: JSON.stringify({ username, password }) }),
  logout: () => request<{ success: boolean; nextPath?: string }>("/api/gateway/admin/auth/logout", { method: "POST", body: JSON.stringify({}) }),
  getDashboard: () => request<AdminDashboardDto>("/api/gateway/admin/dashboard"),
  getCategories: () => request<AdminCategoriesScreenDto>("/api/gateway/admin/categories"),
  createCategory: (payload: { name: string; description?: string | null; displayOrder: number }) => request<{ success: boolean; message: string }>("/api/gateway/admin/categories", { method: "POST", body: JSON.stringify(payload) }),
  updateCategory: (categoryId: number, payload: { name: string; description?: string | null; displayOrder: number; isActive: boolean }) => request<{ success: boolean; message: string }>(`/api/gateway/admin/categories/${categoryId}`, { method: "PUT", body: JSON.stringify(payload) }),
  deleteCategory: (categoryId: number) => request<{ success: boolean; message: string }>(`/api/gateway/admin/categories/${categoryId}`, { method: "DELETE" }),
  getDishes: (search = "", categoryId?: number, page = 1, pageSize = 10, includeInactive = false, vegetarianOnly = false) =>
    request<AdminDishesScreenDto>(`/api/gateway/admin/dishes?search=${encodeURIComponent(search)}${categoryId ? `&categoryId=${categoryId}` : ""}&page=${page}&pageSize=${pageSize}&includeInactive=${includeInactive}${vegetarianOnly ? "&vegetarianOnly=true" : ""}`),
  createDish: (payload: unknown) => request<{ success: boolean; message: string }>("/api/gateway/admin/dishes", { method: "POST", body: JSON.stringify(payload) }),
  createDishWithImage: (payload: Record<string, unknown>, imageFile: File) => requestForm<{ success: boolean; message: string }>("/api/gateway/admin/dishes/upload", toDishFormData(payload, imageFile), "POST"),
  updateDish: (dishId: number, payload: unknown) => request<{ success: boolean; message: string }>(`/api/gateway/admin/dishes/${dishId}`, { method: "PUT", body: JSON.stringify(payload) }),
  updateDishWithImage: (dishId: number, payload: Record<string, unknown>, imageFile: File) => requestForm<{ success: boolean; message: string }>(`/api/gateway/admin/dishes/${dishId}/upload`, toDishFormData(payload, imageFile), "PUT"),
  deleteDish: (dishId: number) => request<{ success: boolean; message: string }>(`/api/gateway/admin/dishes/${dishId}`, { method: "DELETE" }),
  deactivateDish: (dishId: number) => request<{ success: boolean; message: string }>(`/api/gateway/admin/dishes/${dishId}/deactivate`, { method: "POST", body: JSON.stringify({}) }),
  setDishAvailability: (dishId: number, available: boolean) => request<{ success: boolean; message: string }>(`/api/gateway/admin/dishes/${dishId}/availability`, { method: "POST", body: JSON.stringify({ available }) }),
  getDishIngredients: (dishId: number) => request<AdminDishIngredientsDto>(`/api/gateway/admin/dishes/${dishId}/ingredients`),
  saveDishIngredients: (dishId: number, items: { ingredientId: number; quantityPerDish: number }[]) => request<{ success: boolean; message: string }>(`/api/gateway/admin/dishes/${dishId}/ingredients`, { method: "PUT", body: JSON.stringify({ items }) }),
  getIngredients: (search = "", page = 1, pageSize = 10, includeInactive = true, stockStatus = "ALL") =>
    request<AdminIngredientsScreenDto>(`/api/gateway/admin/ingredients?search=${encodeURIComponent(search)}&page=${page}&pageSize=${pageSize}&includeInactive=${includeInactive}&stockStatus=${encodeURIComponent(stockStatus)}`),
  getIngredientRelatedDishes: (ingredientId: number) => request<AdminRelatedIngredientDishDto[]>(`/api/gateway/admin/ingredients/${ingredientId}/related-dishes`),
  getIngredientById: (ingredientId: number) => request<AdminIngredientDto>(`/api/gateway/admin/ingredients/${ingredientId}`),
  createIngredient: (payload: unknown) => request<{ success: boolean; message: string }>("/api/gateway/admin/ingredients", { method: "POST", body: JSON.stringify(payload) }),
  updateIngredient: (ingredientId: number, payload: unknown) => request<{ success: boolean; message: string }>(`/api/gateway/admin/ingredients/${ingredientId}`, { method: "PUT", body: JSON.stringify(payload) }),
  deleteIngredient: (ingredientId: number) => request<{ success: boolean; message: string }>(`/api/gateway/admin/ingredients/${ingredientId}`, { method: "DELETE" }),
  deactivateIngredient: (ingredientId: number) => request<{ success: boolean; message: string }>(`/api/gateway/admin/ingredients/${ingredientId}/deactivate`, { method: "POST", body: JSON.stringify({}) }),
  getIngredientBatches: (ingredientId: number) => request<AdminIngredientBatchDto[]>(`/api/gateway/admin/ingredients/${ingredientId}/batches`),
  createIngredientBatch: (ingredientId: number, payload: CreateIngredientBatchRequest) => request<{ success: boolean; message: string }>(`/api/gateway/admin/ingredients/${ingredientId}/batches`, { method: "POST", body: JSON.stringify(payload) }),
  updateIngredientBatch: (ingredientId: number, batchId: number, payload: UpdateIngredientBatchRequest) => request<{ success: boolean; message: string }>(`/api/gateway/admin/ingredients/${ingredientId}/batches/${batchId}`, { method: "PUT", body: JSON.stringify(payload) }),
  deactivateIngredientBatch: (ingredientId: number, batchId: number) => request<{ success: boolean; message: string }>(`/api/gateway/admin/ingredients/${ingredientId}/batches/${batchId}/deactivate`, { method: "POST", body: JSON.stringify({}) }),
  getIngredientStockMovements: (ingredientId: number, page = 1, pageSize = 20) => request<Paged<IngredientStockMovementDto>>(`/api/gateway/admin/ingredients/${ingredientId}/stock-movements?page=${page}&pageSize=${pageSize}`),
  getInventorySummary: () => request<InventorySummaryDto>("/api/gateway/admin/inventory/summary"),
  getInventoryBatches: (search = "", status = "all", ingredientId?: number, page = 1, pageSize = 20) =>
    request<Paged<InventoryBatchDto>>(`/api/gateway/admin/inventory/batches?search=${encodeURIComponent(search)}&status=${encodeURIComponent(status)}${ingredientId ? `&ingredientId=${ingredientId}` : ""}&page=${page}&pageSize=${pageSize}`),
  stockIn: (payload: InventoryStockInRequest) => request<{ success: boolean; message: string }>("/api/gateway/admin/inventory/stock-in", { method: "POST", body: JSON.stringify(payload) }),
  stockOut: (payload: InventoryStockOutRequest) => request<{ success: boolean; message: string }>("/api/gateway/admin/inventory/stock-out", { method: "POST", body: JSON.stringify(payload) }),
  getInventoryMovements: (params: { ingredientId?: number; batchId?: number; movementType?: string; dateFrom?: string; dateTo?: string; search?: string; page?: number; pageSize?: number } = {}) => {
    const query = new URLSearchParams();
    if (params.ingredientId) query.set("ingredientId", String(params.ingredientId));
    if (params.batchId) query.set("batchId", String(params.batchId));
    if (params.movementType) query.set("movementType", params.movementType);
    if (params.dateFrom) query.set("dateFrom", params.dateFrom);
    if (params.dateTo) query.set("dateTo", params.dateTo);
    if (params.search) query.set("search", params.search);
    query.set("page", String(params.page ?? 1));
    query.set("pageSize", String(params.pageSize ?? 20));
    return request<Paged<InventoryMovementDto>>(`/api/gateway/admin/inventory/movements?${query.toString()}`);
  },
  getUnits: (search = "", page = 1, pageSize = 10, includeInactive = true) =>
    request<AdminUnitsScreenDto>(`/api/gateway/admin/units?search=${encodeURIComponent(search)}&page=${page}&pageSize=${pageSize}&includeInactive=${includeInactive}`),
  getUnitById: (unitId: number) => request<AdminUnitDto>(`/api/gateway/admin/units/${unitId}`),
  createUnit: (payload: { name: string; description?: string | null; displayOrder: number; isActive: boolean }) => request<{ success: boolean; message: string }>("/api/gateway/admin/units", { method: "POST", body: JSON.stringify(payload) }),
  updateUnit: (unitId: number, payload: { name: string; description?: string | null; displayOrder: number; isActive: boolean }) => request<{ success: boolean; message: string }>(`/api/gateway/admin/units/${unitId}`, { method: "PUT", body: JSON.stringify(payload) }),
  deleteUnit: (unitId: number) => request<{ success: boolean; message: string }>(`/api/gateway/admin/units/${unitId}`, { method: "DELETE" }),
  getTables: (search = "", branchId?: number, page = 1, pageSize = 10) =>
    request<AdminTablesScreenDto>(`/api/gateway/admin/tables?search=${encodeURIComponent(search)}${branchId ? `&branchId=${branchId}` : ""}&page=${page}&pageSize=${pageSize}`),
  createTable: (payload: unknown) => request<{ success: boolean; message: string }>("/api/gateway/admin/tables", { method: "POST", body: JSON.stringify(payload) }),
  updateTable: (tableId: number, payload: unknown) => request<{ success: boolean; message: string }>(`/api/gateway/admin/tables/${tableId}`, { method: "PUT", body: JSON.stringify(payload) }),
  deactivateTable: (tableId: number) => request<{ success: boolean; message: string }>(`/api/gateway/admin/tables/${tableId}/deactivate`, { method: "POST", body: JSON.stringify({}) }),
  deleteTable: (tableId: number) => request<{ success: boolean; message: string }>(`/api/gateway/admin/tables/${tableId}`, { method: "DELETE" }),
  getEmployees: (search = "", branchId?: number, roleId?: number, page = 1, pageSize = 10) => request<AdminEmployeesScreenDto>(`/api/gateway/admin/employees?search=${encodeURIComponent(search)}${branchId ? `&branchId=${branchId}` : ""}${roleId ? `&roleId=${roleId}` : ""}&page=${page}&pageSize=${pageSize}`),
  getEmployeeById: (employeeId: number) => request<AdminEmployeeDto>(`/api/gateway/admin/employees/${employeeId}`),
  createEmployee: (payload: unknown) => request<{ success: boolean; message: string }>("/api/gateway/admin/employees", { method: "POST", body: JSON.stringify(payload) }),
  updateEmployee: (employeeId: number, payload: unknown) => request<{ success: boolean; message: string }>(`/api/gateway/admin/employees/${employeeId}`, { method: "PUT", body: JSON.stringify(payload) }),
  deactivateEmployee: (employeeId: number) => request<{ success: boolean; message: string }>(`/api/gateway/admin/employees/${employeeId}/deactivate`, { method: "POST", body: JSON.stringify({}) }),
  getEmployeeHistory: (employeeId: number, activityPage = 1, cookingPage = 1, pageSize = 50, days = 90) => request<AdminEmployeeHistoryResponse>(`/api/gateway/admin/employees/${employeeId}/history?activityPage=${activityPage}&cookingPage=${cookingPage}&pageSize=${pageSize}&days=${days}`),
  getCustomers: (search = "", page = 1, pageSize = 10) => request<AdminCustomersScreenDto>(`/api/gateway/admin/customers?search=${encodeURIComponent(search)}&page=${page}&pageSize=${pageSize}`),
  getCustomerById: (customerId: number) => request<AdminCustomerDto>(`/api/gateway/admin/customers/${customerId}`),
  getCustomerActivityLogs: (customerId: number, page = 1, pageSize = 50) => request<any>(`/api/gateway/admin/customers/${customerId}/activity-logs?page=${page}&pageSize=${pageSize}`),
  getCustomerOrderHistory: (customerId: number, page = 1, pageSize = 20, days = 90) => request<Paged<AdminCustomerOrderHistoryItemDto>>(`/api/gateway/admin/customers/${customerId}/order-history?page=${page}&pageSize=${pageSize}&days=${days}`),
  createCustomer: (payload: unknown) => request<{ success: boolean; message: string }>("/api/gateway/admin/customers", { method: "POST", body: JSON.stringify(payload) }),
  updateCustomer: (customerId: number, payload: unknown) => request<{ success: boolean; message: string }>(`/api/gateway/admin/customers/${customerId}`, { method: "PUT", body: JSON.stringify(payload) }),
  deactivateCustomer: (customerId: number) => request<{ success: boolean; message: string }>(`/api/gateway/admin/customers/${customerId}/deactivate`, { method: "POST", body: JSON.stringify({}) }),
  getReports: () => request<AdminReportsScreenDto>("/api/gateway/admin/reports"),
  getSettings: () => request<AdminSettingsDto>("/api/gateway/admin/settings"),
  updateSettings: (payload: { name: string; phone: string; email?: string | null }) => request<AdminSettingsDto>("/api/gateway/admin/settings", { method: "PUT", body: JSON.stringify(payload) }),
  changePassword: (payload: { currentPassword: string; newPassword: string; confirmPassword: string }) => request<{ success: boolean; message: string }>("/api/gateway/admin/settings/change-password", { method: "POST", body: JSON.stringify(payload) }),
};
