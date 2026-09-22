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

export type CashierTableDto = {
  tableId: number;
  number: string;
  seats: number;
  status: string;
  orderId?: number | null;
};

export type CashierOrderItemCardDto = {
  dishName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  image: string;
  statusCode: string;
};

export type CashierOrderCardDto = {
  orderId: number;
  orderCode: string;
  statusCode: string;
  statusName: string;
  customerId?: number | null;
  customerName: string;
  customerCreditPoints: number;
  subtotal: number;
  itemCount: number;
  items: CashierOrderItemCardDto[];
};

export type CashierBillHistoryItemDto = {
  billId: number;
  billCode: string;
  billTime: string;
  orderCode: string;
  tableName: string;
  subtotal: number;
  discount: number;
  pointsDiscount: number;
  pointsUsed?: number | null;
  totalAmount: number;
  paymentMethod: string;
  paymentAmount?: number | null;
  changeAmount?: number | null;
  customerName: string;
};

export type CashierAccountDto = {
  employeeId: number;
  name: string;
  username: string;
  email: string;
  phone: string;
  branchName: string;
  roleName: string;
};

export type CashierDashboardDto = {
  staff: StaffSessionUserDto;
  tables: CashierTableDto[];
  orders: CashierOrderCardDto[];
  todayOrders: number;
  todayRevenue: number;
  account: CashierAccountDto;
};

export type CashierHistoryDto = {
  staff: StaffSessionUserDto;
  bills: CashierBillHistoryItemDto[];
  account: CashierAccountDto;
};

export type CashierReportScreenDto = {
  staff: StaffSessionUserDto;
  date: string;
  billCount: number;
  totalRevenue: number;
  bills: CashierBillHistoryItemDto[];
  account: CashierAccountDto;
};

export type CashierCheckoutResultDto = {
  billCode: string;
  totalAmount: number;
  changeAmount: number;
  pointsUsed: number;
  pointsEarned: number;
  customerPoints: number;
  customerName?: string | null;
  pointsBefore: number;
  message: string;
};

export type ActiveOrderItemDto = {
  itemId: number;
  orderId: number;
  dishId: number;
  dishName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  note?: string | null;
  unit?: string | null;
  image?: string | null;
  status?: string | null;
};

export type ActiveOrderResponse = {
  orderId: number;
  orderCode?: string | null;
  tableId?: number | null;
  statusCode: string;
  orderStatus: string;
  subtotal: number;
  totalItems: number;
  items: ActiveOrderItemDto[];
  diningSessionCode?: string | null;
  hasActiveDiningSession?: boolean;
  activeOrderIds?: number[];
  hasPendingRound?: boolean;
  pendingOrderId?: number | null;
};

export type ReservationPreOrderItemDto = {
  reservationItemId: number;
  reservationId: number;
  dishId: number;
  dishNameSnapshot: string;
  unitPriceSnapshot: number;
  quantity: number;
  note?: string | null;
  status: string;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  convertedAtUtc?: string | null;
};

export type ReservationDto = {
  reservationId: number;
  reservationCode: string;
  customerId?: number | null;
  customerName: string;
  phoneNumber: string;
  branchId: number;
  tableId?: number | null;
  partySize: number;
  reservedAt: string;
  arrivalWindowMinutes: number;
  status: string;
  note?: string | null;
  convertedOrderId?: number | null;
  diningSessionCode?: string | null;
  checkedInAtUtc?: string | null;
  checkedInByEmployeeId?: number | null;
  cancelledAtUtc?: string | null;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  assignedTables?: ReservationAssignedTableDto[] | null;
  preOrderItems: ReservationPreOrderItemDto[];
};

export type ReservationAssignedTableDto = {
  tableId: number;
  isPrimary: boolean;
};

export type CashierReservationCheckInResultDto = {
  success: boolean;
  message: string;
  reservation: ReservationDto;
  order?: ActiveOrderResponse | null;
  alreadyCheckedIn: boolean;
};
