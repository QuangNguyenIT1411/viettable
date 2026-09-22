export type ApiError = {
  success: false;
  code: string;
  message: string;
  details?: unknown;
};

export type StaffSessionUserDto = {
  employeeId: number;
  username: string;
  name: string;
  phone?: string | null;
  email?: string | null;
  roleId: number;
  roleCode: string;
  roleName: string;
  branchId: number;
  branchName: string;
};

export type StaffSessionDto = {
  authenticated: boolean;
  staff?: StaffSessionUserDto | null;
  loginPath?: string | null;
};

export type StaffForgotPasswordResultDto = {
  message: string;
  resetToken?: string | null;
  expiresAt?: string | null;
  resetPath?: string | null;
};

export type ChefOrderItemDto = {
  itemId: number;
  dishId: number;
  dishName: string;
  quantity: number;
  note?: string | null;
  statusCode: string;
};

export type ChefOrderDto = {
  orderId: number;
  orderCode?: string | null;
  tableId?: number | null;
  tableName?: string | null;
  statusCode: string;
  statusName: string;
  orderTime: string;
  items: ChefOrderItemDto[];
};

export type ChefHistoryDto = {
  orderId: number;
  orderCode?: string | null;
  orderTime: string;
  completedTime?: string | null;
  tableName?: string | null;
  statusCode: string;
  statusName: string;
  dishesSummary: string;
};

export type ChefMenuDishDto = {
  dishId: number;
  name: string;
  price: number;
  unit?: string | null;
  categoryId: number;
  categoryName: string;
  image?: string | null;
  description?: string | null;
  available: boolean;
  isVegetarian: boolean;
  isDailySpecial: boolean;
};

export type ChefCategoryDto = {
  categoryId: number;
  name: string;
  description?: string | null;
  displayOrder: number;
  isActive: boolean;
};

export type ChefMenuDto = {
  branchId: number;
  branchName: string;
  menuDate: string;
  dishes: ChefMenuDishDto[];
};

export type AdminIngredientDto = {
  ingredientId: number;
  name: string;
  unit: string;
  currentStock: number;
  reorderLevel: number;
  isActive: boolean;
};

export type ChefDishIngredientItemDto = {
  ingredientId: number;
  name: string;
  unit: string;
  currentStock: number;
  isActive: boolean;
  quantityPerDish: number;
  defaultQuantityPerDish: number;
  isOverridden: boolean;
};

export type ChefDishIngredientsDto = {
  dishId: number;
  dishName: string;
  orderId?: number | null;
  itemId?: number | null;
  items: ChefDishIngredientItemDto[];
};

export type ChefDashboardSummaryDto = {
  pendingOrders: number;
  preparingOrders: number;
  readyOrders: number;
  totalMenuDishes: number;
  availableMenuDishes: number;
};

export type ChefDashboardDto = {
  staff: StaffSessionUserDto;
  pendingOrders: ChefOrderDto[];
  preparingOrders: ChefOrderDto[];
  readyOrders: ChefOrderDto[];
  history: ChefHistoryDto[];
  menu: ChefMenuDto;
  ingredients: AdminIngredientDto[];
  summary: ChefDashboardSummaryDto;
};
