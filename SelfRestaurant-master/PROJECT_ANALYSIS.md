# PHÂN TÍCH DỰ ÁN SELFRESTAURANT - HỆ THỐNG QUẢN LÝ NHÀ HÀNG TỰ PHỤC VỤ

## 1. TỔNG QUAN DỰ ÁN

### 1.1. Mô tả
**SelfRestaurant** là một hệ thống quản lý nhà hàng tự phục vụ (self-service restaurant) được xây dựng theo kiến trúc **Microservices**. Hệ thống cho phép khách hàng tự đặt món qua QR code trên bàn, đầu bếp nhận và xử lý đơn hàng, thu ngân thanh toán, và quản trị viên quản lý toàn bộ hệ thống.

### 1.2. Mục tiêu
- Tự động hóa quy trình đặt món và phục vụ
- Giảm thiểu sự can thiệp của nhân viên phục vụ
- Tăng tốc độ xử lý đơn hàng
- Quản lý hiệu quả nguyên liệu, món ăn, nhân viên
- Theo dõi doanh thu và báo cáo thống kê

### 1.3. Công nghệ sử dụng

**Backend:**
- **Framework**: ASP.NET Core 8.0 (C#)
- **Database**: SQL Server LocalDB
- **ORM**: Entity Framework Core
- **Architecture**: Microservices với API Gateway
- **Communication**: HTTP/REST APIs, SignalR (real-time notifications)
- **Event-Driven**: File-based integration events (Outbox/Inbox pattern)

**Frontend:**
- **Framework**: React 19.2 với TypeScript
- **Build Tool**: Vite 7.1
- **Routing**: React Router DOM 7.9
- **State Management**: TanStack React Query 5.90 (cho customer và chef apps)
- **Real-time**: SignalR client (customer app)
- **Styling**: CSS thuần, Bootstrap Icons, Font Awesome

**DevOps:**
- **Container**: Docker support
- **Development**: Visual Studio 2022
- **Version Control**: Git

### 1.4. Chú thích các ký tự viết tắt

- **API (Application Programming Interface)**: Giao diện lập trình ứng dụng, dùng để các thành phần hoặc dịch vụ trao đổi dữ liệu với nhau.
- **BFF (Backend for Frontend)**: Lớp backend phục vụ riêng cho frontend, gom dữ liệu và điều phối request theo nhu cầu giao diện.
- **CRUD (Create, Read, Update, Delete)**: Bốn thao tác cơ bản với dữ liệu: tạo mới, đọc/xem, cập nhật và xóa.
- **DTO (Data Transfer Object)**: Đối tượng truyền dữ liệu giữa các lớp hoặc giữa các service, thường chỉ chứa dữ liệu cần thiết cho request/response.
- **HTTP (HyperText Transfer Protocol)**: Giao thức truyền tải request/response giữa client và server.
- **JWT (JSON Web Token)**: Chuỗi token dùng để xác thực và truyền thông tin người dùng trong mô hình stateless authentication.
- **MVC (Model - View - Controller)**: Mô hình kiến trúc tách dữ liệu, giao diện và xử lý nghiệp vụ thành ba phần Model, View và Controller.
- **ORM (Object-Relational Mapping)**: Kỹ thuật ánh xạ giữa đối tượng trong code và bảng dữ liệu trong database.
- **QR (Quick Response)**: Mã phản hồi nhanh, trong hệ thống dùng để khách hàng quét bàn và bắt đầu đặt món.
- **REST (Representational State Transfer)**: Kiểu thiết kế API dùng các phương thức HTTP để thao tác tài nguyên.
- **SPA (Single Page Application)**: Ứng dụng web một trang, tải giao diện một lần và cập nhật nội dung bằng JavaScript.
- **SMTP (Simple Mail Transfer Protocol)**: Giao thức gửi email, dùng cho các tính năng gửi thông báo hoặc xác thực qua email.

---

## 2. KIẾN TRÚC HỆ THỐNG

### 2.1. Kiến trúc tổng thể

Hệ thống áp dụng **Microservices Architecture** với các thành phần chính:

```
┌─────────────────────────────────────────────────────────────┐
│                    API GATEWAY (Port 5100)                   │
│  - Session management                                        │
│  - Request routing                                           │
│  - Actor context propagation                                 │
│  - Static file serving (4 SPAs)                             │
└──────────────┬──────────────────────────────────────────────┘
               │
       ┌───────┴───────┐
       │               │
┌──────▼──────┐ ┌─────▼──────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
│  Catalog    │ │   Orders   │ │ Identity │ │ Billing  │ │Customers │
│  Service    │ │  Service   │ │ Service  │ │ Service  │ │ Service  │
│ (Port 5101) │ │(Port 5102) │ │(Port 5104)│ │(Port 5105)│ │(Port 5103)│
└─────────────┘ └────────────┘ └──────────┘ └──────────┘ └──────────┘
       │               │              │            │            │
       └───────────────┴──────────────┴────────────┴────────────┘
                              │
                    ┌─────────▼─────────┐
                    │  SQL Server       │
                    │  LocalDB          │
                    │  (Shared Database)│
                    └───────────────────┘
```

### 2.2. Service Boundaries (Ranh giới dịch vụ)

Theo tài liệu `AGENTS.md`, các service có trách nhiệm rõ ràng:

#### **Catalog Service** (Port 5101)
- **Ownership**: Menu, Tables, Dishes, Ingredients, Categories
- **Responsibilities**:
  - Quản lý danh mục món ăn (CRUD dishes)
  - Quản lý bàn ăn và trạng thái bàn
  - Quản lý nguyên liệu và tồn kho
  - Quản lý chi nhánh (branches)
  - Validate tính khả dụng của món ăn và nguyên liệu
  - Auto-release tables (background service)

#### **Orders Service** (Port 5102)
- **Ownership**: Order lifecycle, OrderItems
- **Responsibilities**:
  - Tạo và quản lý đơn hàng
  - Quản lý trạng thái đơn hàng (PENDING → PREPARING → READY → SERVED → COMPLETED)
  - Quản lý OrderItems và trạng thái từng món
  - Tracking ChefId cho từng món
  - Submit order với idempotency
  - Cooking history và chef activity logs
  - Integration với Catalog (dish snapshots), Billing (checkout guard)

#### **Identity Service** (Port 5104)
- **Ownership**: Authentication, Customer identity, Employee identity
- **Responsibilities**:
  - Customer authentication (login, register, forgot password)
  - Employee authentication (staff login)
  - Password reset với email
  - Customer profile management
  - Employee management (CRUD)
  - Activity logs (login, password changes)
  - Aggregate employee history từ Orders service

#### **Billing Service** (Port 5105)
- **Ownership**: Checkout, Bills, Cashier aggregates, Payments
- **Responsibilities**:
  - Tạo hóa đơn từ đơn hàng
  - Xử lý thanh toán
  - Checkout guard (prevent duplicate checkout)
  - Payment completed events
  - Revenue reports

#### **Customers Service** (Port 5103)
- **Ownership**: Ready notifications, Customer read models
- **Responsibilities**:
  - Nhận sự kiện "món đã sẵn sàng" từ Orders
  - Gửi thông báo real-time cho khách hàng
  - Quản lý trạng thái thông báo (OPEN/ACKNOWLEDGED)
  - Consumer service cho OrderReady events

#### **Gateway Service** (Port 5100)
- **Responsibilities**:
  - API orchestration và routing
  - Session management (customer, employee sessions)
  - Actor context propagation (X-Actor-* headers)
  - Serve 4 frontend SPAs:
    - `/` → Customer app
    - `/app/chef` → Chef app
    - `/app/cashier` → Cashier app
    - `/app/admin` → Admin app
  - SignalR hub cho customer notifications
  - Correlation ID tracking
  - Dish recommendation service (Gemini AI integration)

### 2.3. Database Schema

Hệ thống sử dụng **shared database** (SQL Server LocalDB) nhưng mỗi service chỉ truy cập tables thuộc ownership của mình.

**Key Tables:**

**Catalog Domain:**
- `Dishes` - Món ăn (DishID, Name, Price, CategoryID, Available, Image, Description)
- `Categories` - Danh mục món (CategoryID, Name, Description)
- `DiningTables` - Bàn ăn (TableID, TableNumber, BranchID, StatusID, QRCode)
- `Ingredients` - Nguyên liệu (IngredientID, Name, Unit, CurrentStock)
- `DishIngredients` - Công thức món ăn (DishID, IngredientID, Quantity)
- `Branches` - Chi nhánh (BranchID, Name, Address)

**Orders Domain:**
- `Orders` - Đơn hàng (OrderID, OrderCode, TableID, CustomerID, StatusID, OrderTime, CompletedTime, DiningSessionCode)
- `OrderItems` - Chi tiết đơn hàng (ItemID, OrderID, DishID, Quantity, UnitPrice, StatusCode, **ChefId**, Note)
- `OrderStatus` - Trạng thái đơn (StatusID, StatusCode, StatusName)
- `SubmitCommands` - Idempotency tracking cho submit
- `OutboxEvents` / `InboxEvents` - Event-driven communication

**Identity Domain:**
- `Customers` - Khách hàng (CustomerID, Username, PasswordHash, FullName, PhoneNumber, Email, Gender, DateOfBirth)
- `Employees` - Nhân viên (EmployeeID, Username, PasswordHash, FullName, RoleID, BranchID)
- `EmployeeRoles` - Vai trò nhân viên (RoleID, RoleCode, RoleName) - CHEF, CASHIER, ADMIN
- `PasswordResetTokens` - Token reset mật khẩu

**Billing Domain:**
- `Bills` - Hóa đơn (BillID, OrderID, TotalAmount, PaymentMethodID, CashierID, CreatedAt)
- `Payments` - Thanh toán (PaymentID, BillID, Amount, PaymentMethodID, StatusID)
- `PaymentMethod` - Phương thức thanh toán (CASH, CARD, TRANSFER)

**Customers Domain:**
- `ReadyDishNotifications` - Thông báo món sẵn sàng (NotificationID, CustomerID, OrderID, DishName, Status)

**Audit:**
- `BusinessAuditLogs` - Audit logs (ActionType, EntityType, EntityID, ActorType, ActorID, BeforeState, AfterState)

### 2.4. Communication Patterns

#### **Synchronous Communication (HTTP/REST)**
- Gateway → Services: HTTP clients với correlation ID và actor context
- Service-to-Service: HTTP clients (Orders → Catalog, Orders → Billing, Identity → Orders)
- Timeout: 30 seconds (configurable)

#### **Asynchronous Communication (Event-Driven)**
- **Pattern**: Outbox/Inbox với file-based events
- **Orders Service** publishes:
  - `OrderReady` → Customers Service
  - `OrderCompleted` → Billing Service
- **Billing Service** publishes:
  - `PaymentCompleted` → Orders Service
- **Implementation**: `FileIntegrationEventPublisher`, background consumers

#### **Real-time Communication (SignalR)**
- Gateway exposes `/hubs/customer-notifications`
- Customers Service → Gateway → Customer browser
- Use case: Thông báo món đã sẵn sàng

---

## 3. FRONTEND APPLICATIONS

Hệ thống có **4 ứng dụng frontend** độc lập, tất cả đều là React SPAs:

### 3.1. Customer Web App (`/`)
**Mục đích**: Khách hàng tự đặt món và theo dõi đơn hàng

**Tính năng chính:**
- **HomePage**: Quét QR code bàn, chọn bàn, reset bàn
- **MenuPage**: Xem menu theo danh mục, thêm món vào giỏ, xem gợi ý món (AI)
- **OrderPage**: Xem giỏ hàng, chỉnh sửa số lượng, ghi chú, submit order
- **DashboardPage**: Xem profile, đổi mật khẩu, xem lịch sử đơn hàng
- **LoginPage / RegisterPage**: Đăng nhập, đăng ký tài khoản
- **Real-time notifications**: Nhận thông báo món đã sẵn sàng qua SignalR

**Tech stack đặc biệt:**
- SignalR client cho real-time
- React Query cho data fetching
- Session-based authentication

### 3.2. Chef Web App (`/app/chef`)
**Mục đích**: Đầu bếp xem và xử lý đơn hàng

**Tính năng chính:**
- **DashboardPage**: 
  - Xem danh sách đơn hàng đang chờ (PENDING, PREPARING)
  - Start preparing món (PENDING → PREPARING)
  - Mark món ready (PREPARING → READY)
  - Pause/Resume món
  - Xem cooking history (món đã hoàn thành)
  - Filter theo trạng thái
- **LoginPage**: Đăng nhập với tài khoản nhân viên (role CHEF)

**Tech stack đặc biệt:**
- React Query với auto-refresh (polling)
- Optimistic updates

### 3.3. Cashier Web App (`/app/cashier`)
**Mục đích**: Thu ngân xử lý thanh toán

**Tính năng chính:**
- **DashboardPage**:
  - Xem danh sách đơn hàng đã sẵn sàng (READY, SERVED)
  - Xem chi tiết đơn hàng và tổng tiền
  - Tạo hóa đơn và xử lý thanh toán
  - Chọn phương thức thanh toán (tiền mặt, thẻ, chuyển khoản)
- **LoginPage**: Đăng nhập với tài khoản nhân viên (role CASHIER)

### 3.4. Admin Web App (`/app/admin`)
**Mục đích**: Quản trị viên quản lý toàn bộ hệ thống

**Tính năng chính:**
- **DashboardPage**: Tổng quan thống kê (doanh thu, đơn hàng, khách hàng)
- **Customers Module**:
  - Danh sách khách hàng với search và pagination
  - Thêm/sửa/xóa khách hàng
  - Xem activity logs (login, register, password changes)
  - **Xem order history** (nhật ký đặt món) - mới thêm
- **Employees Module**:
  - Danh sách nhân viên với search và pagination
  - Thêm/sửa/xóa nhân viên
  - Xem employee history:
    - **Nhật ký tạm ngưng/tiếp tục món** (pause/resume logs)
    - **Nhật ký hoàn thành món ăn** (item completion logs) - mới thêm
    - **Nhật ký nấu ăn** (cooking history - đơn hàng đã hoàn thành)
- **Dishes Module**: Quản lý món ăn (CRUD)
- **Categories Module**: Quản lý danh mục
- **Tables Module**: Quản lý bàn ăn
- **Reports**: Báo cáo doanh thu, món ăn bán chạy

**Cải tiến gần đây:**
- Search input không còn mất focus khi typing (Enter-to-search pattern)
- Hiển thị đầy đủ 3 loại logs cho chef (pause/resume, completions, history)

---

## 4. BUSINESS FLOWS (Luồng nghiệp vụ)

### 4.1. Customer Ordering Flow (Luồng đặt món)

```
1. Customer scans QR code on table
   ↓
2. Gateway redirects to MenuPage with tableId
   ↓
3. Customer browses menu (Catalog.GetMenu)
   ↓
4. Customer adds dishes to cart (local state)
   ↓
5. Customer submits order (Orders.SubmitOrder)
   - Creates/updates Order with PENDING status
   - Creates OrderItems with PENDING status
   - Validates dish availability (Catalog)
   - Validates ingredient stock (Catalog)
   - Consumes ingredients (Catalog.ConsumeInventory)
   - Idempotency check (SubmitCommands table)
   ↓
6. Order appears in Chef dashboard
```

### 4.2. Chef Cooking Flow (Luồng nấu ăn)

```
1. Chef sees PENDING orders in dashboard
   ↓
2. Chef clicks "Bắt đầu nấu" on an order item
   - Orders.UpdateOrderItemStatus(PREPARING)
   - **Saves ChefId to OrderItems table**
   ↓
3. Chef prepares the dish
   ↓
4. Chef clicks "Sẵn sàng" when done
   - Orders.UpdateOrderItemStatus(READY)
   - **Saves ChefId again (for completion tracking)**
   - Publishes OrderReady event
   ↓
5. Customers Service receives event
   - Creates ReadyDishNotification
   - Sends SignalR notification to customer
   ↓
6. Customer receives real-time notification
```

**Tính năng mới**: ChefId tracking
- Mỗi món ăn được gán ChefId khi chef bắt đầu nấu
- Cho phép lọc cooking history theo từng chef
- Hiển thị item completion logs riêng cho mỗi chef

### 4.3. Cashier Checkout Flow (Luồng thanh toán)

```
1. Cashier sees READY/SERVED orders
   ↓
2. Cashier selects order to checkout
   - Orders.GetCheckoutContext (order details, total)
   ↓
3. Cashier confirms payment method
   ↓
4. Cashier processes payment (Billing.CreateBill)
   - Creates Bill record
   - Creates Payment record
   - Publishes PaymentCompleted event
   ↓
5. Orders Service receives PaymentCompleted
   - Updates Order status to COMPLETED
   - Releases table (Catalog.ReleaseTable)
```

### 4.4. Admin Management Flow (Luồng quản trị)

```
Admin Dashboard:
- View statistics (Orders.GetAdminStats)
- View revenue reports (Orders.GetRevenueReport)
- View top dishes (Orders.GetTopDishes)

Customer Management:
- List customers with search (Identity.GetCustomers)
- Create/Edit/Delete customer (Identity CRUD)
- View activity logs (Identity.GetCustomerActivityLogs)
- **View order history** (Identity → Orders.GetCustomerOrderHistory)

Employee Management:
- List employees with search (Identity.GetEmployees)
- Create/Edit/Delete employee (Identity CRUD)
- View employee history (Identity → Orders.GetEmployeeHistory)
  - **Pause/resume logs** (BusinessAuditLogs)
  - **Item completion logs** (OrderItems with ChefId)
  - **Cooking history** (completed orders)

Dish Management:
- List dishes (Catalog.GetDishes)
- Create/Edit/Delete dish (Catalog CRUD)
- Check dish references before delete (Orders.GetDishReferences)
```

---

## 5. KEY TECHNICAL PATTERNS

### 5.1. Actor Context Propagation
- Gateway extracts session info (CustomerId, EmployeeId, Role)
- Forwards as HTTP headers: `X-Actor-Type`, `X-Actor-Id`, `X-Actor-Code`, `X-Actor-Name`, `X-Actor-RoleCode`
- Services use `RequestActorContextAccessor` to read actor info
- Stored in `BusinessAuditLogs` for traceability

### 5.2. Correlation ID Tracking
- Gateway generates `X-Correlation-Id` for each request
- Propagated to all services via `CorrelationIdHandler`
- Used for distributed tracing and log correlation
- Stored in audit logs

### 5.3. Idempotency Pattern
- `SubmitCommands` table tracks submit operations
- Prevents duplicate order submission
- Returns existing result if idempotency key matches

### 5.4. Optimistic Concurrency
- Application-level locks using SQL Server `sp_getapplock`
- Table-level locking for order operations
- Prevents race conditions in multi-round ordering

### 5.5. Snapshot Pattern
- Orders service stores snapshots of Catalog data:
  - `CatalogDishSnapshots` (dish name, price at order time)
  - `CatalogTableSnapshots` (table info)
  - `CatalogBranchSnapshots` (branch info)
- Preserves historical data even if catalog changes

### 5.6. Outbox/Inbox Pattern
- `OutboxEvents` table in publisher service
- `InboxEvents` table in consumer service
- Background services poll and process events
- Ensures at-least-once delivery

### 5.7. Business Audit Logging
- Every significant action logged to `BusinessAuditLogs`
- Captures: actor, action type, entity, before/after state
- Enables compliance, debugging, and analytics

---

## 6. DATA CONSISTENCY ISSUES (Vấn đề đã sửa)

### 6.1. Duplicate Dishes Problem
**Vấn đề**: 
- Database có món ăn trùng tên nhưng khác DishID và giá
- Admin hiển thị 6 món, Customer hiển thị 7 món
- Customer menu load từ bảng cũ (Menus, MenuCategory, CategoryDish)
- Admin load trực tiếp từ bảng Dishes

**Giải pháp**:
- Modified `Catalog.GetMenu` để load trực tiếp từ `Dishes` table
- Bỏ qua cấu hình Menu cũ
- Filter chỉ món `Available = true`
- Tạo script `sql/cleanup_duplicate_dishes.sql` để đánh dấu món cũ là `Available = 0`

**Trạng thái**: Đã sửa code, cần chạy cleanup script

### 6.2. ChefId Tracking
**Vấn đề**:
- Cooking history hiển thị TẤT CẢ đơn hàng của chi nhánh
- Không filter theo chef cụ thể

**Giải pháp**:
- Thêm cột `ChefId INT NULL` vào bảng `OrderItems`
- Lưu ChefId khi chef start preparing và mark ready
- Filter cooking history theo `employeeId` parameter
- Mỗi chef có history riêng

**Trạng thái**: Đã hoàn thành

### 6.3. Search Input Focus Loss
**Vấn đề**:
- Input search mất focus sau mỗi ký tự
- Auto-search với debounce trigger navigate() → re-render

**Giải pháp**:
- Tách `searchInput` (typing value) và `searchQuery` (actual search)
- Chỉ search khi submit form (Enter hoặc click button)
- Bỏ URL params cho search

**Trạng thái**: Đã hoàn thành

### 6.4. Reset Button Dialog
**Vấn đề**:
- Nút "Reset" trên customer homepage không hiển thị dialog
- Dialog xuất hiện ở cuối trang

**Giải pháp**:
- Thêm CSS cho `.app-dialog-backdrop` với `position: fixed`
- Z-index cao (99999)
- Backdrop overlay với fade-in animation

**Trạng thái**: Đã hoàn thành

---

## 7. DEPLOYMENT & CONFIGURATION

### 7.1. Development Setup
```
1. Database: SQL Server LocalDB
   - Connection string: "Server=(localdb)\\mssqllocaldb;Database=RestaurantDb;..."
   
2. Start services (Visual Studio):
   - Open SelfRestaurant.Microservices.sln
   - Press F5 (starts all services + gateway)
   
3. Build frontends:
   cd src/Frontend/selfrestaurant-customer-web
   npm install
   npm run build
   
   (Repeat for admin-web, chef-web, cashier-web)
   
4. Access:
   - Gateway: http://localhost:5100
   - Customer: http://localhost:5100/
   - Chef: http://localhost:5100/app/chef
   - Cashier: http://localhost:5100/app/cashier
   - Admin: http://localhost:5100/app/admin
```

### 7.2. Service Ports
- Gateway: 5100
- Catalog: 5101
- Orders: 5102
- Customers: 5103
- Identity: 5104
- Billing: 5105

### 7.3. Configuration Files
- `appsettings.json` - Service URLs, timeouts, feature flags
- `appsettings.Development.json` - Development overrides
- `.env` - Environment variables (SMTP password, API keys)

---

## 8. TESTING & QUALITY

### 8.1. Test Logs
- Extensive test logs in `.runlogs/` directory
- Actor chain microservice tests
- Admin flow tests (customer, employee CRUD)
- Integration test scripts (PowerShell)

### 8.2. API Testing
- Swagger UI available in development mode
- `.http` files for manual API testing
- Health check endpoints: `/healthz`, `/readyz`

### 8.3. Diagnostics
- `/internal/diagnostics/eventing` - Event queue status
- Business audit logs for debugging
- Correlation ID for distributed tracing

---

## 9. STRENGTHS (Điểm mạnh)

1. **Clear Service Boundaries**: Mỗi service có ownership rõ ràng
2. **Event-Driven Architecture**: Loose coupling giữa các services
3. **Audit Trail**: Đầy đủ business audit logs
4. **Idempotency**: Xử lý duplicate requests
5. **Real-time Notifications**: SignalR cho customer experience tốt
6. **Actor Context**: Traceability cho mọi action
7. **Snapshot Pattern**: Preserve historical data
8. **Modern Frontend**: React 19 với TypeScript
9. **Scalable**: Microservices có thể scale độc lập
10. **Well-documented**: AGENTS.md, extensive logs

---

## 10. AREAS FOR IMPROVEMENT (Cần cải thiện)

### 10.1. Architecture
- **Shared Database**: Vi phạm microservices principle, nên tách database per service
- **File-based Events**: Nên dùng message broker (RabbitMQ, Kafka) cho production
- **No API Versioning**: Cần versioning strategy cho backward compatibility
- **No Circuit Breaker**: Cần resilience patterns (Polly)

### 10.2. Security
- **No HTTPS in dev**: Nên enable HTTPS ngay từ development
- **Session-based Auth**: Nên dùng JWT tokens cho stateless authentication
- **No Rate Limiting**: Chỉ có rate limit ở Identity service
- **No Input Validation**: Cần validation middleware

### 10.3. Performance
- **N+1 Queries**: Một số endpoints có thể optimize với eager loading
- **No Caching**: Nên cache menu, categories
- **Polling**: Chef app dùng polling thay vì SignalR

### 10.4. Testing
- **No Unit Tests**: Không có automated tests
- **No Integration Tests**: Chỉ có manual test scripts
- **No E2E Tests**: Cần Playwright/Cypress

### 10.5. DevOps
- **No CI/CD**: Cần GitHub Actions/Azure DevOps pipeline
- **No Monitoring**: Cần Application Insights, Prometheus
- **No Logging Aggregation**: Cần ELK stack hoặc Seq
- **Docker not used**: Có Dockerfile nhưng không dùng trong dev

### 10.6. Code Quality
- **Large Controllers**: OrdersController có 3400+ lines, nên refactor
- **Magic Strings**: Status codes nên dùng enums
- **Inconsistent Error Handling**: Cần global exception handler
- **No API Documentation**: Cần OpenAPI/Swagger annotations

---

## 11. RECOMMENDED NEXT STEPS (Bước tiếp theo)

### 11.1. Immediate (Ngay lập tức)
1. ✅ Chạy `sql/cleanup_duplicate_dishes.sql` để xóa món trùng
2. ✅ Restart services và verify Admin/Customer menu đồng bộ
3. Add unit tests cho business logic
4. Add input validation middleware

### 11.2. Short-term (Ngắn hạn - 1-2 tuần)
1. Refactor OrdersController thành smaller services
2. Implement global exception handler
3. Add API versioning
4. Enable HTTPS in development
5. Add caching cho menu và categories

### 11.3. Medium-term (Trung hạn - 1-2 tháng)
1. Migrate to JWT authentication
2. Implement circuit breaker pattern
3. Add integration tests
4. Setup CI/CD pipeline
5. Add monitoring và logging aggregation

### 11.4. Long-term (Dài hạn - 3-6 tháng)
1. Separate databases per service
2. Migrate to message broker (RabbitMQ)
3. Implement CQRS pattern
4. Add E2E tests
5. Containerize với Docker Compose
6. Deploy to cloud (Azure/AWS)

---

## 12. CONCLUSION (Kết luận)

**SelfRestaurant** là một dự án microservices được thiết kế tốt với:
- ✅ Kiến trúc rõ ràng, service boundaries hợp lý
- ✅ Event-driven communication
- ✅ Comprehensive audit logging
- ✅ Modern tech stack (ASP.NET Core 8, React 19)
- ✅ Real-time notifications
- ✅ Good separation of concerns

**Tuy nhiên**, vẫn còn nhiều điểm cần cải thiện:
- ⚠️ Shared database (nên tách)
- ⚠️ Thiếu automated tests
- ⚠️ Thiếu monitoring và observability
- ⚠️ Cần refactor một số controllers lớn
- ⚠️ Cần CI/CD pipeline

**Đánh giá tổng thể**: Dự án phù hợp cho môi trường học tập và demo, nhưng cần thêm nhiều cải tiến để production-ready.

---

**Tài liệu này được tạo bởi Kiro AI vào ngày 10/05/2026**
