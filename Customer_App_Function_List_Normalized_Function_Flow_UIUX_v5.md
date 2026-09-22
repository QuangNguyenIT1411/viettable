# Customer App – Hệ thống đặt bàn và đặt món nhà hàng
## Chuỗi nhiều chi nhánh – Normalized Flow v2.0

---

# 1. Mục tiêu

Xây dựng ứng dụng mobile dành cho **khách hàng của chuỗi nhà hàng**, hỗ trợ toàn bộ hành trình:

```text
Chọn chi nhánh
→ Đặt bàn trước (tùy chọn)
→ Đặt món trước / Pre-order (tùy chọn)
→ Đến nhà hàng
→ Check-in
→ Scan QR tại bàn
→ Bắt đầu phiên dùng bữa
→ Gọi món / Gọi thêm món
→ Theo dõi order
→ Thanh toán
→ Kết thúc phiên dùng bữa
→ Đánh giá
```

Ứng dụng phục vụ mô hình **chuỗi nhà hàng nhiều chi nhánh cùng một thương hiệu**.


> **Phạm vi tài liệu:** Tài liệu này chỉ tập trung vào **chức năng Customer App, business flow, trạng thái nghiệp vụ và danh sách màn hình UI/UX**. Không mô tả kiến trúc backend, module Java, API hoặc chi tiết triển khai kỹ thuật.

Các dữ liệu dùng chung toàn hệ thống:

- Tài khoản khách hàng
- Điểm loyalty
- Hạng thành viên
- Lịch sử hoạt động
- Voucher toàn hệ thống

Các dữ liệu gắn với từng chi nhánh:

- Menu áp dụng tại chi nhánh
- Giá món
- Tình trạng món
- Bàn
- Đặt bàn
- Phiên dùng bữa
- Order
- Thanh toán
- Đánh giá chi nhánh

---

# 2. Nguyên tắc chuẩn hóa nghiệp vụ

Hệ thống tách rõ 4 nghiệp vụ chính:

```text
Reservation
    │
    │ optional
    ▼
Pre-order
    │
    ▼
Check-in / QR
    │
    ▼
Dining Session
    │
    ├── Order #1
    ├── Order #2
    ├── Order #3
    │
    ▼
Payment
```

## 2.1. Reservation

`Reservation` chỉ chịu trách nhiệm **giữ bàn trước**.

Reservation không đại diện cho phiên dùng bữa và không thay thế Order.

## 2.2. Pre-order

Pre-order là một Order được tạo **trước thời điểm khách check-in** và phải gắn với một Reservation.

## 2.3. Dining Session

`DiningSession` đại diện cho **một phiên khách thực tế đang dùng bàn tại nhà hàng**.

Dining Session được tạo khi:

- Khách đặt bàn trước và check-in; hoặc
- Khách walk-in được xếp bàn và scan QR.

## 2.4. Order

Một Dining Session có thể có **nhiều Order**.

Ví dụ:

```text
Dining Session DS001
├── Order #1: món gọi ban đầu
├── Order #2: gọi thêm món
└── Order #3: gọi thêm đồ uống
```

Các Order được cộng lại thành một hóa đơn của Dining Session.

---

# 3. Danh sách chức năng

## 3.1. Tài khoản & xác thực

- Đăng ký tài khoản
- Đăng nhập
- Đăng xuất
- Quên mật khẩu
- Đặt lại mật khẩu
- Đổi mật khẩu
- Cập nhật thông tin cá nhân
- Quản lý avatar
- Quản lý số điện thoại
- Quản lý email
- Quản lý địa chỉ

> Tài khoản dùng chung cho toàn bộ chuỗi nhà hàng.

---

## 3.2. Chọn chi nhánh

- Xem danh sách chi nhánh
- Xem chi tiết chi nhánh
  - Tên
  - Địa chỉ
  - Số điện thoại
  - Giờ mở cửa
  - Hình ảnh
  - Mô tả
  - Tiện ích
- Tìm chi nhánh gần nhất bằng GPS
- Xem chi nhánh trên bản đồ
- Chọn chi nhánh mặc định
- Đổi chi nhánh đang sử dụng
- Hiển thị trạng thái:
  - Đang mở cửa
  - Sắp đóng cửa
  - Đã đóng cửa
- Hiển thị khoảng cách
- Hiển thị tình trạng nhận đặt bàn nếu cần

> Chi nhánh đang chọn ảnh hưởng tới menu, giá, bàn trống, voucher theo chi nhánh và các nghiệp vụ đặt bàn/đặt món.

---

## 3.3. Trang chủ

- Hiển thị chi nhánh đang chọn
- Đổi nhanh chi nhánh
- Hiển thị thông tin thương hiệu
- Banner
- Chương trình khuyến mãi
- Món nổi bật
- Món bán chạy
- Món mới
- Danh mục món
- Gợi ý cá nhân hóa
- Chi nhánh yêu thích
- Reservation sắp tới
- Dining Session đang diễn ra
- Order đang xử lý
- Tìm kiếm nhanh

Truy cập nhanh:

- Đặt bàn
- Đặt món
- Scan QR
- Đơn hàng
- Lịch đặt bàn

---

## 3.4. Tìm kiếm & khám phá món

- Tìm kiếm theo tên món
- Tìm kiếm theo danh mục
- Lọc theo giá
- Lọc theo đánh giá
- Lọc theo trạng thái món
- Sắp xếp:
  - Giá thấp → cao
  - Giá cao → thấp
  - Phổ biến
  - Đánh giá cao
- Xem món bán chạy
- Xem món mới
- Xem món được đề xuất

> Kết quả tìm kiếm mặc định thuộc menu của chi nhánh đang chọn.

---

## 3.5. Menu & chi tiết món

### 3.5.1. Danh mục

- Khai vị
- Món chính
- Đồ uống
- Tráng miệng
- Danh mục khác

### 3.5.2. Chi tiết món

- Hình ảnh
- Tên món
- Giá tại chi nhánh
- Mô tả
- Thành phần
- Cảnh báo dị ứng
- Thông tin dinh dưỡng
- Đánh giá
- Số lượng đã bán
- Món liên quan
- Topping
- Kích thước
- Mức cay
- Ghi chú
- Thêm yêu thích
- Thêm giỏ hàng

### 3.5.3. Menu theo chi nhánh

Menu gồm:

```text
Brand Menu
    ↓
Branch Menu Override
```

Chi nhánh có thể:

- Ẩn món
- Hiện món
- Override giá
- Có món riêng
- Thay đổi trạng thái còn bán / hết món

---

## 3.6. Đặt bàn online – Reservation

### 3.6.1. Chức năng

- Chọn chi nhánh
- Chọn ngày
- Chọn giờ
- Chọn số lượng khách
- Chọn khu vực
- Xem sơ đồ bàn
- Xem bàn còn trống
- Chọn bàn
- Chọn vị trí:
  - Trong nhà
  - Ngoài trời
  - Phòng riêng
  - Gần cửa sổ
- Nhập ghi chú
- Xem chính sách đặt bàn
- Đặt cọc nếu yêu cầu
- Xác nhận đặt bàn
- Xem chi tiết Reservation
- Thay đổi Reservation
- Hủy Reservation
- Xử lý hoàn cọc
- Xử lý hết bàn

### 3.6.2. Flow chuẩn

```text
Chọn chi nhánh
    ↓
Chọn ngày
    ↓
Chọn giờ
    ↓
Chọn số người
    ↓
Chọn khu vực
    ↓
Xem bàn trống
    ↓
Chọn bàn
    ↓
Nhập ghi chú
    ↓
Có yêu cầu đặt cọc?
    ├── Có → Thanh toán đặt cọc
    └── Không
    ↓
Xác nhận đặt bàn
    ↓
Reservation = CONFIRMED
```

### 3.6.3. Reservation Status

Các trạng thái đề xuất:

- `PENDING`
- `PENDING_DEPOSIT`
- `CONFIRMED`
- `CHECKED_IN`
- `COMPLETED`
- `CANCELLED`
- `NO_SHOW`

Flow:

```text
PENDING
   ↓
PENDING_DEPOSIT (nếu có)
   ↓
CONFIRMED
   ↓
CHECKED_IN
   ↓
COMPLETED
```

Nhánh ngoại lệ:

```text
PENDING / CONFIRMED
        ├── CANCELLED
        └── NO_SHOW
```

---

## 3.7. Đặt món trước – Pre-order

Pre-order chỉ áp dụng khi khách có Reservation hợp lệ.

### 3.7.1. Flow

```text
Reservation = CONFIRMED
        ↓
Bạn có muốn đặt món trước?
        │
   ┌────┴────┐
   │         │
  Có       Không
   │         │
   ↓         ↓
Menu        Kết thúc
   ↓
Chọn món
   ↓
Giỏ hàng
   ↓
Xác nhận
   ↓
Order type = PRE_ORDER
```

### 3.7.2. Chức năng

- Xem menu của chi nhánh đã đặt bàn
- Chọn món
- Chọn số lượng
- Chọn topping
- Chọn kích thước
- Chọn mức cay
- Ghi chú
- Thêm món
- Xóa món
- Sửa món
- Xem tổng tiền
- Áp voucher nếu được phép
- Xác nhận pre-order
- Thay đổi pre-order
- Hủy pre-order theo chính sách

### 3.7.3. Quy tắc

Pre-order phải gắn với **Reservation và chi nhánh đã đặt bàn**.

Trước khi khách check-in, pre-order vẫn được hiểu là món đặt trước cho Reservation. Khi khách check-in, pre-order được đưa vào phiên dùng bữa tương ứng.

---

## 3.8. Giỏ hàng

- Xem món
- Tăng / giảm số lượng
- Xóa món
- Chỉnh sửa topping
- Chỉnh sửa size
- Chỉnh sửa mức cay
- Thêm ghi chú
- Kiểm tra giá
- Tạm tính
- Voucher
- Phí dịch vụ
- Tổng tiền

Giỏ hàng cần phân biệt rõ ngữ cảnh sử dụng:

- **Pre-order:** giỏ hàng thuộc Reservation đã chọn.
- **Dine-in:** giỏ hàng thuộc phiên dùng bữa tại bàn.

> Một giỏ hàng chỉ thuộc một chi nhánh.

Khi đổi chi nhánh:

```text
Giỏ hàng có món?
    ↓
Có
    ↓
Hiển thị cảnh báo
    ↓
Xác nhận đổi?
    ├── Không → giữ nguyên
    └── Có → reset giỏ hàng
```

---

## 3.9. Check-in

Check-in là bước nối giữa Reservation và Dining Session.

### 3.9.1. Khách đặt bàn trước

```text
Reservation = CONFIRMED
        ↓
Khách đến nhà hàng
        ↓
Check-in
        ↓
Reservation = CHECKED_IN
        ↓
Create / Activate Dining Session
```

### 3.9.2. Hình thức check-in

Có thể hỗ trợ:

- Scan QR tại bàn
- Nhân viên xác nhận
- Nhập mã đặt bàn
- QR Reservation riêng nếu triển khai

MVP khuyến nghị:

> Scan QR bàn + xác thực Reservation.

---

## 3.10. QR Ordering tại bàn

QR đại diện cho:

```text
branch + table
```

QR cần xác định được **đúng chi nhánh và đúng bàn**. Người dùng chỉ cần quét QR; các chi tiết định danh kỹ thuật không cần xuất hiện trên UI.

### 3.10.1. Flow QR chuẩn

```text
Scan QR
   ↓
Validate QR Token
   ↓
Xác định Branch + Table
   ↓
QR hợp lệ?
   ├── Không → Báo QR không hợp lệ
   └── Có
          ↓
Kiểm tra Dining Session đang active
          │
     ┌────┴────┐
     │         │
    Có        Không
     │         │
     ↓         ↓
Load Session  Kiểm tra Reservation
                   │
                   ├── Có Reservation phù hợp
                   │       ↓
                   │    Check-in
                   │       ↓
                   │    Create Dining Session
                   │
                   └── Không có Reservation
                           ↓
                       Walk-in
                           ↓
                    Create Dining Session
```

Sau đó:

```text
Dining Session ACTIVE
      ↓
Load menu
      ↓
Load Pre-order nếu có
      ↓
Chọn món / Gọi thêm món
      ↓
Create Order
```

---

## 3.11. Dining Session – Phiên dùng bữa

Dining Session là trung tâm của toàn bộ QR / Dine-in flow.

### 3.11.1. Thông tin cần hiển thị / quản lý

- Chi nhánh
- Bàn
- Reservation liên quan nếu có
- Khách chính nếu có tài khoản
- Thời gian bắt đầu
- Trạng thái phiên dùng bữa

### 3.11.2. Status

- `CREATED`
- `ACTIVE`
- `PAYMENT_REQUESTED`
- `PAID`
- `CLOSED`
- `CANCELLED`

Flow:

```text
CREATED
   ↓
ACTIVE
   ↓
PAYMENT_REQUESTED
   ↓
PAID
   ↓
CLOSED
```

### 3.11.3. Quy tắc

Một bàn tại một thời điểm chỉ nên có tối đa một Dining Session active.

Một Dining Session:

- Thuộc một chi nhánh
- Thuộc một bàn
- Có thể có Reservation hoặc không
- Có thể có nhiều Order
- Có thể có nhiều lần gọi nhân viên
- Có một hóa đơn tổng cuối phiên

---

## 3.12. Đặt món tại bàn – Dine-in Order

### 3.12.1. Flow

```text
Dining Session = ACTIVE
        ↓
Xem Menu
        ↓
Chọn món
        ↓
Giỏ hàng
        ↓
Place Order
        ↓
Order = PLACED
        ↓
Restaurant Confirm
        ↓
CONFIRMED
        ↓
PREPARING
        ↓
SERVING
        ↓
COMPLETED
```

### 3.12.2. Gọi thêm món

Khách có thể tạo nhiều Order:

```text
Dining Session DS001
├── Order O001
├── Order O002
└── Order O003
```

Mỗi lần bấm "Đặt món" tạo một Order mới.

---

## 3.13. Order

Order đại diện cho **một lần khách gửi món xuống nhà hàng**.

Một Dining Session có thể có nhiều Order:

```text
Dining Session DS001
├── Order O001
├── Order O002
└── Order O003
```

Mỗi lần khách bấm **Đặt món** hoặc **Gọi thêm món** sẽ tạo một Order mới.

### 3.13.1. Loại Order

- `PRE_ORDER`
- `DINE_IN`

Có thể mở rộng sau:

- `TAKEAWAY`
- `DELIVERY`

### 3.13.2. Order Status – trạng thái tổng quát

Order status chỉ thể hiện trạng thái **tổng quát của cả Order**, không dùng để mô tả chính xác từng món.

Các trạng thái:

- `PLACED`
- `CONFIRMED`
- `IN_PROGRESS`
- `COMPLETED`
- `CANCELLED`

Flow chính:

```text
PLACED
   ↓
CONFIRMED
   ↓
IN_PROGRESS
   ↓
COMPLETED
```

Nhánh ngoại lệ:

```text
PLACED / CONFIRMED
        ↓
CANCELLED
```

Ý nghĩa:

| Status | Ý nghĩa |
|---|---|
| `PLACED` | Khách vừa gửi Order |
| `CONFIRMED` | Nhà hàng đã xác nhận nhận Order |
| `IN_PROGRESS` | Ít nhất một món đang được chuẩn bị / sẵn sàng / phục vụ |
| `COMPLETED` | Tất cả món đã hoàn tất hoặc bị hủy hợp lệ |
| `CANCELLED` | Toàn bộ Order bị hủy |

### 3.13.3. Order Item Status – trạng thái từng món

Mỗi món trong Order có trạng thái riêng.

Các trạng thái:

- `PENDING`
- `CONFIRMED`
- `PREPARING`
- `READY`
- `SERVED`
- `CANCELLED`

Flow chuẩn:

```text
PENDING
   ↓
CONFIRMED
   ↓
PREPARING
   ↓
READY
   ↓
SERVED
```

Nhánh ngoại lệ:

```text
PENDING / CONFIRMED / PREPARING
              ↓
          CANCELLED
```

Ý nghĩa:

| Status | Hiển thị cho khách |
|---|---|
| `PENDING` | Đã gửi |
| `CONFIRMED` | Nhà hàng đã nhận |
| `PREPARING` | Đang chuẩn bị |
| `READY` | Đã sẵn sàng |
| `SERVED` | Đã phục vụ |
| `CANCELLED` | Đã hủy |

### 3.13.4. Quy tắc đồng bộ Order Status từ Order Item

Order chuyển sang `IN_PROGRESS` khi:

- Có ít nhất một Order Item ở `PREPARING`, `READY` hoặc `SERVED`; và
- Order chưa hoàn thành.

Order chuyển sang `COMPLETED` khi:

```text
Tất cả Order Item
        ↓
SERVED hoặc CANCELLED
        ↓
Order = COMPLETED
```

Ví dụ:

```text
Order O001 = IN_PROGRESS

Phở bò x2
→ SERVED

Nem rán x1
→ PREPARING

Coca x2
→ SERVED
```

Order vẫn là `IN_PROGRESS` vì Nem rán chưa hoàn tất.

### 3.13.5. Chức năng

- Xem Order
- Xem chi tiết Order
- Xem danh sách món
- Theo dõi trạng thái Order tổng quát
- Theo dõi trạng thái từng món
- Xem thời gian đặt
- Xem chi nhánh
- Xem bàn
- Xem món đang chuẩn bị
- Xem món đã sẵn sàng
- Xem món đã phục vụ
- Xem món đã hủy
- Xem ETA nếu có
- Nhận cập nhật realtime
- Nhận push notification
- Hủy Order nếu chính sách cho phép
- Hủy món nếu chính sách cho phép
- Gọi thêm món

---

## 3.14. Theo dõi Order realtime

Order Tracking được chuẩn hóa theo **2 tầng**:

```text
Tầng 1: Order Status
        ↓
PLACED → CONFIRMED → IN_PROGRESS → COMPLETED

Tầng 2: Order Item Status
        ↓
PENDING → CONFIRMED → PREPARING → READY → SERVED
```

### 3.14.1. Flow realtime chuẩn

```text
Khách Place Order
        ↓
Order = PLACED
        ↓
Các món = PENDING
        ↓
Nhà hàng xác nhận Order
        ↓
Order = CONFIRMED
        ↓
Các món được xác nhận
        ↓
Order Item = CONFIRMED
        ↓
Bếp bắt đầu xử lý từng món
        ↓
Order = IN_PROGRESS
        ↓
Order Item:
PREPARING
   ↓
READY
   ↓
SERVED
        ↓
Tất cả Item = SERVED / CANCELLED
        ↓
Order = COMPLETED
```

### 3.14.2. Timeline Order trên UI

Timeline tổng quát:

```text
✓ Đã đặt
    ↓
✓ Nhà hàng xác nhận
    ↓
● Đang xử lý
    ↓
○ Hoàn thành
```

Không hiển thị `PREPARING` và `SERVING` ở Order level nữa vì các trạng thái này thuộc từng món.

### 3.14.3. Trạng thái từng món trên UI

Ví dụ:

```text
Order #VT001
Đang xử lý

─────────────────────────
✓ Phở bò x2
  Đã phục vụ

● Nem rán x1
  Đang chuẩn bị

◉ Coca x2
  Đã sẵn sàng
─────────────────────────
```

Khách có thể nhìn được mỗi món đang ở trạng thái nào.

### 3.14.4. Dining Session có nhiều Order

Màn `Current Orders` tổng hợp các Order đang thuộc Dining Session hiện tại.

Ví dụ:

```text
Bàn B12

Order #1
✓ Hoàn thành
Phở bò x2
Coca x2

Order #2
● Đang xử lý
Nem rán x1

Order #3
● Đang xử lý
Trà đào x2
```

Mỗi Order vẫn có màn `Order Tracking` riêng.

### 3.14.5. Pre-order trong Order Tracking

Sau khi khách check-in:

```text
PRE_ORDER
    ↓
Được đưa vào Dining Session
    ↓
Theo dõi như một Order trong phiên dùng bữa
```

Ví dụ:

```text
Đặt trước
────────────────────
Phở bò x2
● Đang chuẩn bị

Gọi thêm lúc 19:15
────────────────────
Coca x4
✓ Nhà hàng xác nhận
```

Không tạo lại Pre-order thành Order mới.

### 3.14.6. Realtime Update

Khi trạng thái thay đổi, UI cập nhật realtime:

- Order được xác nhận
- Order bắt đầu xử lý
- Món bắt đầu chuẩn bị
- Món đã sẵn sàng
- Món đã phục vụ
- Món bị hủy
- Order hoàn thành
- Order mới được thêm vào Dining Session

### 3.14.7. Push Notification

Có thể gửi notification cho các mốc quan trọng:

- Nhà hàng đã xác nhận Order
- Món đang được chuẩn bị
- Món đã sẵn sàng / sắp được phục vụ
- Order đã hoàn thành
- Order bị hủy

Không cần push notification cho mọi thay đổi nhỏ nếu gây quá nhiều thông báo.

### 3.14.8. ETA

Có thể hiển thị:

- ETA của toàn Order; hoặc
- ETA theo từng món nếu nhà hàng hỗ trợ.

Ví dụ:

```text
Dự kiến hoàn tất: 10–15 phút
```

ETA chỉ mang tính tham khảo.

---

## 3.15. Gọi nhân viên

Chỉ hoạt động khi Dining Session = ACTIVE.

Yêu cầu:

- Gọi phục vụ
- Thêm dụng cụ
- Xin nước
- Hỗ trợ món
- Hủy món
- Yêu cầu thanh toán
- Yêu cầu khác

Request status:

- `REQUESTED`
- `ACCEPTED`
- `COMPLETED`
- `CANCELLED`

---

## 3.16. Thanh toán

Thanh toán gắn với **Dining Session**, không thanh toán riêng từng Order.

Một Dining Session có thể phát sinh nhiều Order trong suốt bữa ăn. Khi khách yêu cầu thanh toán, hệ thống tổng hợp toàn bộ Order thành một hóa đơn cuối.

### 3.16.1. Flow thanh toán chuẩn

```text
Dining Session = ACTIVE
        ↓
Khách chọn "Thanh toán"
        ↓
Kiểm tra Order chưa hoàn tất
        │
        ├── Có
        │     ↓
        │  Hiển thị cảnh báo
        │     ↓
        │  Khách chọn:
        │     ├── Tiếp tục dùng bữa
        │     └── Vẫn yêu cầu thanh toán
        │
        └── Không
              ↓
Dining Session = PAYMENT_REQUESTED
        ↓
Tổng hợp toàn bộ Order
        ↓
Tạo / hiển thị Invoice
        ↓
Khách kiểm tra hóa đơn
        ↓
Chọn Voucher (tùy chọn)
        ↓
Sử dụng điểm Loyalty (tùy chọn)
        ↓
Tính Final Amount
        ↓
Chọn hình thức thanh toán
        │
        ├──────────────────────────┐
        ↓                          ↓
Thanh toán online           Thanh toán tại nhà hàng
        │                          │
        ↓                          ↓
Chọn phương thức            Gửi yêu cầu thanh toán
        │                          │
        ↓                          ↓
Xác nhận                    Nhân viên nhận yêu cầu
        │                          │
        ↓                          ↓
PROCESSING                   Thanh toán tại bàn/quầy
        │                          │
        └─────────────┬────────────┘
                      ↓
               PAYMENT SUCCESS
                      ↓
          Dining Session = PAID
                      ↓
          Dining Session = CLOSED
                      ↓
                    REVIEW
```

### 3.16.2. Kiểm tra trước khi thanh toán

Khi khách bấm **Thanh toán**, hệ thống kiểm tra các Order trong Dining Session.

Nếu vẫn có món ở trạng thái:

- `PLACED`
- `CONFIRMED`
- `PREPARING`
- `SERVING`

UI cần cảnh báo:

> Một số món trong đơn vẫn đang được xử lý. Bạn có muốn tiếp tục yêu cầu thanh toán không?

Các lựa chọn:

- Tiếp tục dùng bữa
- Vẫn yêu cầu thanh toán

Khi yêu cầu thanh toán được xác nhận:

```text
Dining Session:
ACTIVE
   ↓
PAYMENT_REQUESTED
```

Khi Dining Session đang `PAYMENT_REQUESTED`, khách **không thể tạo Order mới**.

Nếu khách muốn gọi thêm món:

```text
PAYMENT_REQUESTED
        ↓
Hủy yêu cầu thanh toán
        ↓
ACTIVE
        ↓
Cho phép gọi thêm món
```

### 3.16.3. Hóa đơn – Invoice

Invoice tổng hợp toàn bộ Order thuộc cùng Dining Session.

Ví dụ:

```text
Dining Session DS001

Order #1      200.000đ
Order #2      150.000đ
Order #3       50.000đ
----------------------
Tạm tính      400.000đ
Phí dịch vụ    20.000đ
Thuế            0.000đ
Voucher        -30.000đ
Điểm loyalty   -20.000đ
----------------------
Thanh toán     370.000đ
```

Màn hình hóa đơn cần hiển thị:

- Chi nhánh
- Bàn
- Các Order trong phiên
- Danh sách món
- Số lượng
- Đơn giá
- Tạm tính
- Phí dịch vụ
- Thuế nếu có
- Voucher giảm giá
- Điểm Loyalty đã sử dụng
- Tổng thanh toán cuối cùng

### 3.16.4. Voucher trong thanh toán

Voucher **không tự động áp dụng**.

```text
Invoice
   ↓
Chọn "Voucher"
   ↓
Xem voucher khả dụng
   ↓
Chọn voucher
   ↓
Kiểm tra điều kiện
   │
   ├── Hợp lệ → Áp dụng
   └── Không hợp lệ → Hiển thị lý do
   ↓
Cập nhật Final Amount
```

Khách có thể:

- Chọn voucher
- Đổi voucher
- Bỏ voucher

Voucher có thể:

- Áp dụng toàn hệ thống
- Chỉ áp dụng tại chi nhánh cụ thể

### 3.16.5. Loyalty trong thanh toán

Điểm Loyalty là lựa chọn chủ động của khách.

```text
Invoice
   ↓
Chọn "Dùng điểm"
   ↓
Hiển thị:
- Điểm hiện có
- Số điểm có thể sử dụng
- Giá trị quy đổi
   ↓
Khách chọn số điểm
   ↓
Xác nhận
   ↓
Cập nhật Final Amount
```

Không tự động sử dụng toàn bộ điểm của khách.

### 3.16.6. Hình thức và phương thức thanh toán

Hệ thống tách rõ 3 khái niệm:

```text
1. Hình thức thanh toán
2. Phương thức thanh toán
3. Địa điểm thanh toán
```

#### A. Hình thức thanh toán

Khách chọn một trong hai hình thức:

```text
CHỌN HÌNH THỨC THANH TOÁN
        │
        ├────────────────────────────┐
        ↓                            ↓
THANH TOÁN ONLINE          THANH TOÁN TẠI NHÀ HÀNG
```

---

#### B. Thanh toán online

Các phương thức online:

- `QR_PAYMENT`
- `E_WALLET`
- `ONLINE_CARD`

Ý nghĩa trên UI:

| Mã | Hiển thị |
|---|---|
| `QR_PAYMENT` | QR Payment |
| `E_WALLET` | Ví điện tử |
| `ONLINE_CARD` | Thẻ ngân hàng / thẻ quốc tế |

Flow:

```text
Final Amount
    ↓
Chọn "Thanh toán online"
    ↓
Chọn phương thức
    │
    ├── QR Payment
    ├── Ví điện tử
    └── Thẻ online
    ↓
Xác nhận
    ↓
Payment = PENDING
    ↓
PROCESSING
    │
    ├── SUCCESS
    │      ↓
    │   Dining Session = PAID
    │      ↓
    │   CLOSED
    │
    └── FAILED
           ↓
        Hiển thị lỗi
           ↓
        Khách chọn:
        ├── Thử lại
        ├── Đổi phương thức
        └── Chuyển sang thanh toán tại nhà hàng
```

> Với phạm vi đồ án, có thể mô phỏng cổng thanh toán thay vì tích hợp cổng thật.

---

#### C. Thanh toán tại nhà hàng

Khi chọn thanh toán tại nhà hàng, khách tiếp tục chọn:

**Phương thức thanh toán:**

- `CASH`
- `CARD_AT_RESTAURANT`
- `QR_AT_RESTAURANT`

Ý nghĩa trên UI:

| Mã | Hiển thị |
|---|---|
| `CASH` | Tiền mặt |
| `CARD_AT_RESTAURANT` | Thẻ |
| `QR_AT_RESTAURANT` | QR / chuyển khoản |

Sau đó chọn **địa điểm thanh toán**:

- `AT_TABLE`
- `AT_COUNTER`

UI hiển thị:

- Tại bàn
- Tại quầy

Flow:

```text
Invoice
    ↓
Chọn "Thanh toán tại nhà hàng"
    ↓
Chọn phương thức
    │
    ├── Tiền mặt
    ├── Thẻ
    └── QR / chuyển khoản
    ↓
Chọn địa điểm
    │
    ├── Tại bàn
    └── Tại quầy
    ↓
Xác nhận yêu cầu
    ↓
Dining Session = PAYMENT_REQUESTED
    ↓
Nhân viên nhận yêu cầu
    ↓
Thực hiện thanh toán
    ↓
Nhân viên / hệ thống xác nhận
    ↓
Payment = SUCCESS
    ↓
Dining Session = PAID
    ↓
CLOSED
```

Khách cần có thể xem trạng thái yêu cầu:

- Đã gửi yêu cầu
- Nhân viên đã nhận
- Đang xử lý thanh toán
- Thanh toán thành công

---

### 3.16.7. Phạm vi triển khai đề xuất

Để tránh mở rộng quá mức trong đồ án, có thể chia:

#### MVP

```text
Thanh toán online
└── QR Payment

Thanh toán tại nhà hàng
├── Tiền mặt
├── Thẻ
└── QR / chuyển khoản
```

#### Phase 2

```text
Thanh toán online
├── Ví điện tử
└── Thẻ online
```

Như vậy MVP vẫn có đủ flow thanh toán hoàn chỉnh nhưng không phụ thuộc quá nhiều vào tích hợp cổng thanh toán thật.

### 3.16.8. Payment Status

```text
PENDING
   ↓
PROCESSING
   ↓
SUCCESS
```

Các trạng thái ngoại lệ:

- `FAILED`
- `CANCELLED`
- `EXPIRED`
- `REFUNDED`

### 3.16.9. Sau khi thanh toán thành công

```text
Payment = SUCCESS
        ↓
Dining Session = PAID
        ↓
Dining Session = CLOSED
        ↓
Hiển thị Payment Success
        ↓
Cho phép Review
```

Màn hình thành công hiển thị:

- Tổng tiền
- Hình thức thanh toán
- Phương thức thanh toán
- Địa điểm thanh toán nếu thanh toán tại nhà hàng
- Thời gian thanh toán
- Chi nhánh
- Bàn
- Mã hóa đơn
- Điểm Loyalty nhận được nếu có
- CTA:
  - Xem hóa đơn
  - Đánh giá
  - Về trang chủ

---

## 3.17. Chia hóa đơn – Split Bill

Tùy chọn Phase 3.

- Chia đều theo số người
- Chia theo món
- Chia theo số tiền
- Theo dõi phần đã thanh toán
- Đảm bảo tổng các phần bằng tổng hóa đơn

---

## 3.18. Quản lý Reservation

### Lịch sử

Tabs:

- Sắp tới
- Đang diễn ra
- Hoàn thành
- Đã hủy

Thông tin:

- Reservation ID
- Chi nhánh
- Ngày
- Giờ
- Số người
- Khu vực
- Bàn
- Tiền cọc
- Pre-order
- Trạng thái

### Chi tiết

- Xem chi tiết
- Đổi ngày
- Đổi giờ
- Đổi số người
- Đổi bàn nếu cho phép
- Hủy
- Xem chính sách
- Xem tiền hoàn cọc
- Đặt món trước
- Check-in khi đến nơi

---

## 3.19. Xử lý hết bàn

```text
Chọn ngày / giờ / số người
        ↓
Không còn bàn
        ↓
Gợi ý:
   ├── Khung giờ gần nhất
   ├── Ngày khác
   └── Chi nhánh gần nhất còn bàn
        ↓
Khách chọn phương án
```

---

## 3.20. Lịch sử Order

Tabs:

- Đang xử lý
- Hoàn thành
- Đã hủy

Thông tin:

- Order ID
- Order type
- Chi nhánh
- Dining Session
- Reservation nếu có
- Thời gian
- Bàn
- Danh sách món
- Số lượng
- Đơn giá
- Tổng tiền
- Thanh toán
- Trạng thái

---

## 3.21. Yêu thích

- Món yêu thích
- Chi nhánh yêu thích
- Bỏ yêu thích
- Đặt lại món
- Đi tới chi nhánh

---

## 3.22. Đánh giá & Review

Sau khi Dining Session hoặc Order hoàn thành:

- Đánh giá món
- Đánh giá chi nhánh
- Đánh giá dịch vụ
- Đánh giá không gian
- Chấm sao
- Nhận xét
- Upload ảnh
- Sửa đánh giá

---

## 3.23. Voucher & khuyến mãi

- Xem voucher
- Nhận voucher
- Lưu voucher
- Áp dụng voucher
- Kiểm tra điều kiện
- Voucher toàn hệ thống
- Voucher theo chi nhánh
- Hạn sử dụng
- Lịch sử dùng
- Chương trình khuyến mãi

---

## 3.24. Thông báo

### Reservation

- Đặt bàn thành công
- Reservation được xác nhận
- Sắp đến giờ
- Thay đổi Reservation
- Hủy Reservation

### Order

- Order được xác nhận
- Order bắt đầu xử lý
- Món đang chuẩn bị
- Món đã sẵn sàng
- Món đã phục vụ
- Order hoàn thành
- Order / món bị hủy

### Payment

- Thanh toán thành công
- Thanh toán thất bại
- Hoàn tiền

### Promotion

- Voucher mới
- Khuyến mãi
- Khuyến mãi theo chi nhánh

---

## 3.25. Hồ sơ cá nhân

- Thông tin cá nhân
- Avatar
- Email
- Số điện thoại
- Địa chỉ
- Chi nhánh mặc định
- Reservation
- Order
- Món yêu thích
- Chi nhánh yêu thích
- Voucher
- Review
- Loyalty
- Notification
- Settings
- Logout

---

## 3.26. Cài đặt

- Chỉnh sửa thông tin
- Đổi mật khẩu
- Notification settings
- Ngôn ngữ
- Điều khoản sử dụng
- Chính sách bảo mật
- Trung tâm hỗ trợ
- Đăng xuất

---

## 3.27. Hỗ trợ khách hàng

- FAQ
- Hotline chi nhánh
- Gọi điện
- Chat hỗ trợ
- Gửi phản hồi
- Báo vấn đề Order
- Báo vấn đề Reservation
- Báo vấn đề thanh toán

---

# 4. Loyalty & Membership

- Tích điểm
- Điểm dùng chung toàn hệ thống
- Xem điểm hiện tại
- Lịch sử điểm
- Đổi điểm
- Hạng thành viên
- Ưu đãi theo hạng
- Điểm phát sinh có lưu chi nhánh

---

# 5. Recommendation & Personalization

## 5.1. Recommendation

Gợi ý dựa trên:

- Lịch sử Order
- Món yêu thích
- Món thường gọi
- Danh mục thường chọn
- Món bán chạy
- Món liên quan
- Chi nhánh
- Thời gian trong ngày
- Promotion

Phase đầu:

```text
Rule-based Recommendation
```

Có thể nâng cấp:

```text
AI / ML Recommendation
```

## 5.2. Cá nhân hóa

- Cá nhân hóa Home
- Gợi ý món
- Gợi ý combo
- Gợi ý món gọi thêm
- Gợi ý voucher
- Gợi ý theo chi nhánh

---

# 6. Flow tổng thể chuẩn hóa

```text
                         CUSTOMER APP
                              │
                              ↓
                       CHỌN CHI NHÁNH
                              │
              ┌───────────────┴────────────────┐
              │                                │
              ↓                                ↓
        ĐẶT BÀN TRƯỚC                    ĐẾN TRỰC TIẾP
              │                                │
              ↓                                │
         RESERVATION                           │
              │                                │
              ↓                                │
       Reservation Confirmed                   │
              │                                │
              ↓                                │
        Có Pre-order?                          │
        ├── Có → PRE_ORDER                     │
        └── Không                              │
              │                                │
              └───────────────┬────────────────┘
                              ↓
                       ĐẾN NHÀ HÀNG
                              ↓
                          SCAN QR
                              ↓
                 XÁC ĐỊNH BRANCH + TABLE
                              ↓
                KIỂM TRA RESERVATION
                              │
               ┌──────────────┴──────────────┐
               │                             │
              Có                           Không
               │                             │
               ↓                             ↓
           CHECK-IN                       WALK-IN
               │                             │
               └──────────────┬──────────────┘
                              ↓
                    CREATE / LOAD
                     DINING SESSION
                              ↓
                         SESSION ACTIVE
                              ↓
              ┌───────────────┴───────────────┐
              │                               │
        LOAD PRE-ORDER                    XEM MENU
          NẾU CÓ                              │
              │                               │
              └───────────────┬───────────────┘
                              ↓
                          PLACE ORDER
                              ↓
                       ORDER CONFIRMED
                              ↓
                          PREPARING
                              ↓
                           SERVING
                              ↓
                          COMPLETED
                              │
                              ├─────────────┐
                              │             │
                        GỌI THÊM MÓN    GỌI NHÂN VIÊN
                              │             │
                              └──────┬──────┘
                                     ↓
                              TIẾP TỤC DÙNG BỮA
                                     ↓
                               YÊU CẦU THANH TOÁN
                                     ↓
                         PAYMENT_REQUESTED
                                     ↓
                           TỔNG HỢP ORDER
                                     ↓
                                  INVOICE
                                     ↓
                        CHỌN VOUCHER (OPTIONAL)
                                     ↓
                      DÙNG LOYALTY (OPTIONAL)
                                     ↓
                              FINAL AMOUNT
                                     ↓
                    CHỌN HÌNH THỨC THANH TOÁN
                            ┌────────┴────────┐
                            ↓                 ↓
                         ONLINE          TẠI NHÀ HÀNG
                            │                 │
                            └────────┬────────┘
                                     ↓
                              PAYMENT SUCCESS
                                     ↓
                         DINING SESSION = PAID
                                     ↓
                         DINING SESSION CLOSED
                                     ↓
                                  REVIEW
```

---

# 7. Các use case chuẩn hóa

## 7.1. Case 1 – Đặt bàn + Pre-order

```text
Mở app
→ Chọn chi nhánh
→ Chọn ngày
→ Chọn giờ
→ Chọn số người
→ Chọn khu vực
→ Chọn bàn
→ Đặt cọc nếu cần
→ Xác nhận Reservation
→ Chọn Pre-order
→ Chọn món
→ Xác nhận Pre-order
→ Đến nhà hàng
→ Scan QR
→ Validate Reservation
→ Check-in
→ Tạo Dining Session
→ Load Pre-order
→ Theo dõi món
→ Gọi thêm món nếu cần
→ Yêu cầu thanh toán
→ Xem Invoice
→ Chọn Voucher / Loyalty nếu muốn
→ Chọn hình thức thanh toán
→ Thanh toán thành công
→ Đóng Dining Session
→ Đánh giá
```

## 7.2. Case 2 – Đặt bàn nhưng không Pre-order

```text
Mở app
→ Chọn chi nhánh
→ Đặt bàn
→ Xác nhận
→ Đến nhà hàng
→ Scan QR
→ Validate Reservation
→ Check-in
→ Create Dining Session
→ Xem menu
→ Đặt món
→ Theo dõi Order
→ Gọi thêm món
→ Yêu cầu thanh toán
→ Xem Invoice
→ Chọn Voucher / Loyalty nếu muốn
→ Chọn hình thức thanh toán
→ Thanh toán thành công
→ Đóng Dining Session
→ Đánh giá
```

## 7.3. Case 3 – Walk-in

```text
Đến nhà hàng
→ Nhân viên xếp bàn
→ Scan QR bàn
→ Không có Reservation
→ Create Dining Session
→ Xem menu
→ Đặt món
→ Theo dõi Order
→ Gọi thêm món
→ Yêu cầu thanh toán
→ Xem Invoice
→ Chọn Voucher / Loyalty nếu muốn
→ Chọn hình thức thanh toán
→ Thanh toán thành công
→ Đóng Dining Session
→ Đánh giá
```

## 7.4. Case 4 – Reservation scan nhầm bàn

```text
Khách có Reservation B12
→ Scan QR B15
→ Backend phát hiện Reservation không khớp bàn
→ Không auto check-in
→ Hiển thị cảnh báo
→ Yêu cầu:
   - Scan đúng QR
   hoặc
   - Nhân viên xác nhận đổi bàn
```

## 7.5. Case 5 – Reservation tại chi nhánh khác

```text
User có Reservation Branch A
→ Scan QR Branch B
→ Backend phát hiện không khớp chi nhánh
→ Hiển thị Reservation hiện có
→ Không liên kết Reservation
→ Có thể tiếp tục Walk-in tại Branch B nếu nhà hàng cho phép
```

## 7.6. Case 6 – Bàn đang có Dining Session

```text
Scan QR
→ Table có Dining Session ACTIVE
→ Kiểm tra quyền tham gia session
→ Nếu hợp lệ:
      Load session
→ Nếu không hợp lệ:
      Không tạo session mới
      Yêu cầu nhân viên hỗ trợ
```

## 7.7. Case 7 – Gọi thêm món

```text
Dining Session = ACTIVE
        ↓
Xem menu
        ↓
Thêm món vào Cart
        ↓
Place Order
        ↓
Tạo Order mới
        ↓
Order = PLACED
        ↓
Nhà hàng xác nhận
        ↓
Order = CONFIRMED
        ↓
Xử lý từng Order Item
        ↓
Order = IN_PROGRESS
        ↓
Order Item:
PREPARING → READY → SERVED
        ↓
Tất cả Item hoàn tất
        ↓
Order = COMPLETED
```

Order cũ không bị thay đổi khi khách gọi thêm món.

Ví dụ:

```text
Dining Session DS001
├── Order O001 = COMPLETED
├── Order O002 = IN_PROGRESS
└── Order O003 = PLACED
```

Khách theo dõi tất cả Order tại màn `Current Orders` và có thể mở từng Order để xem chi tiết realtime.


## 7.8. Case 8 – Thanh toán

```text
Dining Session = ACTIVE
        ↓
Khách bấm "Thanh toán"
        ↓
Kiểm tra Order chưa hoàn tất
        │
        ├── Có → Cảnh báo
        │         ├── Tiếp tục dùng bữa
        │         └── Vẫn yêu cầu thanh toán
        │
        └── Không
              ↓
Dining Session = PAYMENT_REQUESTED
        ↓
Tổng hợp toàn bộ Order
        ↓
Tạo / hiển thị Invoice
        ↓
Khách kiểm tra hóa đơn
        ↓
Chọn Voucher (optional)
        ↓
Dùng điểm Loyalty (optional)
        ↓
Final Amount
        ↓
Chọn hình thức thanh toán
        │
        ├──────────────────────────────┐
        ↓                              ↓
Thanh toán online            Thanh toán tại nhà hàng
        │                              │
        ↓                              ↓
Chọn phương thức             Chọn phương thức
├── QR Payment               ├── Tiền mặt
├── Ví điện tử               ├── Thẻ
└── Thẻ online               └── QR / chuyển khoản
        │                              ↓
        │                    Chọn địa điểm
        │                    ├── Tại bàn
        │                    └── Tại quầy
        │                              │
        └──────────────┬───────────────┘
                       ↓
                Thực hiện thanh toán
                       ↓
                 PAYMENT SUCCESS
                       ↓
             Dining Session = PAID
                       ↓
                     CLOSED
                       ↓
                     REVIEW
```

### Trường hợp thanh toán online thất bại

```text
PROCESSING
    ↓
FAILED
    ↓
Hiển thị lỗi
    ↓
Khách chọn:
├── Thử lại
├── Đổi phương thức online
└── Chuyển sang thanh toán tại nhà hàng
```

### Trường hợp muốn gọi thêm món sau khi đã yêu cầu thanh toán

```text
Dining Session = PAYMENT_REQUESTED
        ↓
Khách chọn gọi thêm món
        ↓
Thông báo:
"Bạn đã yêu cầu thanh toán"
        ↓
Khách chọn:
├── Tiếp tục thanh toán
└── Hủy yêu cầu thanh toán
        ↓
Nếu hủy:
Dining Session = ACTIVE
        ↓
Cho phép tạo Order mới
```

---

# 8. State machine

## 8.1. Reservation

```text
PENDING
    │
    ├── deposit required → PENDING_DEPOSIT
    │                           │
    │                           ↓
    └────────────────────→ CONFIRMED
                                │
                    ┌───────────┼────────────┐
                    ↓           ↓            ↓
              CHECKED_IN    CANCELLED     NO_SHOW
                    │
                    ↓
                COMPLETED
```

## 8.2. Dining Session

Flow chính:

```text
CREATED
   ↓
ACTIVE
   ↓
PAYMENT_REQUESTED
   ↓
PAID
   ↓
CLOSED
```

Nếu khách hủy yêu cầu thanh toán trước khi Payment thành công:

```text
PAYMENT_REQUESTED
        ↓
Cancel Payment Request
        ↓
ACTIVE
```

Khi ở `PAYMENT_REQUESTED`, UI không cho tạo Order mới cho đến khi khách hủy yêu cầu thanh toán hoặc thanh toán hoàn tất.

## 8.3. Order

### Order Status

```text
PLACED
   ↓
CONFIRMED
   ↓
IN_PROGRESS
   ↓
COMPLETED
```

Ngoại lệ:

```text
PLACED / CONFIRMED
        ↓
CANCELLED
```

### Order Item Status

```text
PENDING
   ↓
CONFIRMED
   ↓
PREPARING
   ↓
READY
   ↓
SERVED
```

Ngoại lệ:

```text
PENDING / CONFIRMED / PREPARING
              ↓
          CANCELLED
```

### Quy tắc hoàn thành Order

```text
Tất cả Order Item
        ↓
SERVED hoặc CANCELLED
        ↓
Order = COMPLETED
```


## 8.4. Payment

```text
PENDING
   ↓
PROCESSING
   ↓
SUCCESS
```

Ngoại lệ:

```text
FAILED
CANCELLED
EXPIRED
REFUNDED
```

---

# 9. Quan hệ nghiệp vụ

```text
User
 │
 ├──────── Reservation
 │             │
 │             └──────── Pre-order
 │
 └──────── Dining Session
                │
                ├──────── Order
                │            │
                │            └──────── Order Item
                │
                ├──────── Service Request
                │
                ├──────── Invoice
                │
                └──────── Payment
```

Về mặt chức năng, Order cần phân biệt hai loại:

- **PRE_ORDER:** món được đặt trước cho một Reservation.
- **DINE_IN:** món được gọi trong lúc khách đang dùng bữa.

Khi khách check-in, pre-order cần xuất hiện trong Dining Session để khách có thể theo dõi cùng các món gọi thêm.
---


# 10. Kiến trúc điều hướng UI/UX

Hệ thống chỉ có **một Home chính**.

```text
Home
= Trang chủ chính của Customer App

Dining Session
= Màn trung tâm của phiên dùng bữa tại bàn
```

Không sử dụng khái niệm "Home thứ hai".

---

## 10.1. Bottom Navigation chính

Sau khi đăng nhập và chọn chi nhánh, ứng dụng sử dụng Bottom Navigation gồm 5 mục:

```text
┌──────────┬───────────┬─────────┬──────────┬──────────┐
│   Home   │  Đặt bàn  │ Scan QR │ Đơn hàng│    Tôi   │
│    🏠    │    📅     │   QR    │    🧾    │    👤    │
└──────────┴───────────┴─────────┴──────────┴──────────┘
```

Các tab:

1. `Home`
2. `Reservation`
3. `Scan QR`
4. `Orders`
5. `Profile`

---

## 10.2. Home

`Home` là trang chủ duy nhất của ứng dụng.

Home gồm:

- Chi nhánh đang chọn
- Đổi chi nhánh
- Search
- Banner / Promotion
- Món nổi bật
- Món bán chạy
- Recommendation
- Reservation sắp tới
- Dining Session đang active nếu có
- Order đang xử lý
- Truy cập nhanh:
  - Đặt bàn
  - Scan QR
  - Menu
  - Current Orders

Flow:

```text
Home
├── Branch Selector
├── Search
├── Menu
├── Food Detail
├── Reservation Detail
├── Dining Session đang active
└── Notification
```

---

## 10.3. Reservation tab

Tab `Đặt bàn` quản lý toàn bộ nghiệp vụ Reservation.

Root:

```text
Reservation
├── Đặt bàn mới
└── Reservation History
```

### Đặt bàn mới

```text
Reservation
    ↓
Select Date
    ↓
Select Time
    ↓
Guest Count
    ↓
Select Area
    ↓
Table Layout
    ↓
Reservation Note
    ↓
Deposit nếu cần
    ↓
Reservation Confirmation
    ↓
Reservation Detail
```

### Reservation History

```text
Reservation History
    ↓
Reservation Detail
    ├── Change Reservation
    ├── Cancel Reservation
    └── Pre-order
```

---

## 10.4. Scan QR tab

`Scan QR` là action chính để bắt đầu hoặc quay lại trải nghiệm tại bàn.

```text
Scan QR
    ↓
QR Validation
    ↓
Xác định Branch + Table
    ↓
Kiểm tra Reservation
    │
    ├── Có
    │    ↓
    │ Check-in Confirmation
    │
    └── Không
         ↓
      Walk-in Confirmation
    ↓
Dining Session
```

Nếu QR không khớp Reservation:

```text
QR Validation
    ↓
Reservation Mismatch
```

---

## 10.5. Orders tab

Tab `Đơn hàng` dùng để xem Order hiện tại và lịch sử.

Root:

```text
Orders
├── Current Orders
└── Order History
```

Nếu có Dining Session đang active:

```text
Current Orders
    ↓
Order Tracking
    ↓
Order Detail
    ↓
Order Item Status Detail
```

Order History:

```text
Order History
    ↓
Order Detail
```

---

## 10.6. Profile tab

Tab `Tôi`:

```text
Profile
├── Edit Profile
├── Favorites
├── Voucher
├── Loyalty
├── Review History
├── Settings
└── Help / Support
```

---

## 10.7. Dining Session – trung tâm phiên dùng bữa

`Dining Session` không phải Home và không phải Bottom Navigation tab độc lập.

Đây là màn context của **phiên dùng bữa đang diễn ra**.

Flow vào Dining Session:

```text
Scan QR / Check-in
        ↓
Dining Session
```

Dining Session gồm:

```text
Dining Session
├── Thông tin chi nhánh
├── Bàn
├── Reservation nếu có
├── Pre-order nếu có
├── Dine-in Menu
├── Current Orders
├── Call Staff
└── Thanh toán
```

Điều hướng:

```text
                  DINING SESSION
                        │
          ┌─────────────┼──────────────┐
          ↓             ↓              ↓
      Gọi món      Current Orders   Gọi nhân viên
          │             │              │
          ↓             ↓              ↓
   Dine-in Menu    Order Tracking   Call Staff
          ↓
   Dine-in Cart
          ↓
 Order Confirmation
```

Thanh toán:

```text
Dining Session
    ↓
Thanh toán
    ↓
Payment Request / Checkout Summary
```

---

## 10.8. Active Dining Session entry

Khi có Dining Session `ACTIVE`, người dùng phải có cách quay lại nhanh từ các tab khác.

UI hiển thị một **Active Dining Session Card / Persistent Banner**.

Ví dụ:

```text
┌──────────────────────────────────────┐
│ Bàn B12 • Đang dùng bữa             │
│ 2 Order đang xử lý                  │
│                                      │
│ [Gọi món] [Theo dõi] [Thanh toán]   │
└──────────────────────────────────────┘
```

Card có thể xuất hiện tại:

- Home
- Orders
- Profile nếu cần

Tap vào card:

```text
Active Dining Session Card
        ↓
Dining Session
```

---

## 10.9. Navigation khi Dining Session ACTIVE

Bottom Navigation vẫn giữ nguyên:

```text
Home | Đặt bàn | Scan QR | Đơn hàng | Tôi
```

Không thay Bottom Navigation bằng một navigation khác.

Dining Session là một màn nằm phía trên navigation hierarchy.

Nếu user rời Dining Session:

```text
Dining Session
    ↓
Home / Orders / Profile
```

phiên dùng bữa vẫn tiếp tục `ACTIVE`.

User có thể quay lại bằng:

- Active Dining Session Card
- Current Orders
- Scan QR lại đúng bàn

---

## 10.10. Back behavior

### Flow dạng wizard

Các flow sau dùng Back để quay lại bước trước:

- Reservation
- Pre-order
- Checkout / Payment
- QR validation

Ví dụ:

```text
Select Area
    ↑ Back
Guest Count
```

### Sau khi đã xác nhận

Không quay Back vào wizard đã hoàn tất.

Ví dụ:

```text
Reservation Confirmation
    ↓
Reservation Detail
```

Back từ `Reservation Detail`:

```text
Reservation History
hoặc
Home
```

tùy entry point.

### Dining Session

Back từ Dining Session:

```text
Dining Session
    ↓
Tab / màn trước đó
```

Không kết thúc Dining Session.

---

## 10.11. Navigation sau Place Order

Sau khi khách xác nhận Order:

```text
Dine-in Cart
    ↓
Order Confirmation
    ↓
Order Tracking
```

Từ Order Tracking:

```text
Order Tracking
├── Dining Session
├── Current Orders
└── Gọi thêm món
```

Nếu chọn `Gọi thêm món`:

```text
Order Tracking
    ↓
Dine-in Menu
```

---

## 10.12. Navigation sau thanh toán

Thanh toán thành công:

```text
Payment Success
    ↓
Dining Session = CLOSED
    ↓
Review
```

Tại `Payment Success` có CTA:

- `Xem hóa đơn`
- `Đánh giá`
- `Về trang chủ`

Flow mặc định:

```text
Payment Success
    ↓
Review (optional)
    ↓
Home
```

Sau khi Dining Session đã `CLOSED`:

- Active Dining Session Card biến mất.
- Current Orders chuyển các Order liên quan sang Order History.
- Scan QR có thể bắt đầu Dining Session mới nếu bàn hợp lệ.

---

## 10.13. Navigation sau Pre-order

```text
Reservation Detail
    ↓
Pre-order Menu
    ↓
Pre-order Cart
    ↓
Pre-order Confirmation
    ↓
Pre-order Detail
    ↓
Reservation Detail
```

Pre-order không chuyển user vào Dining Session vì khách chưa check-in.

---

## 10.14. Đổi chi nhánh

Điểm đổi chi nhánh chính:

```text
Home
    ↓
Branch Selector
    ↓
Branch List / Branch Map
    ↓
Chọn Branch
    ↓
Home
```

Nếu có Cart:

```text
Đổi chi nhánh
    ↓
Cart có món?
    ├── Không → đổi branch
    └── Có → cảnh báo
```

Nếu đang có Dining Session `ACTIVE`:

```text
Đổi branch context trên Home
≠
Đổi branch của Dining Session
```

Dining Session luôn giữ nguyên branch đã được xác định khi check-in.

---

## 10.15. Navigation hierarchy tổng thể

```text
Splash
  ↓
Welcome / Authentication
  ↓
Branch Selection
  ↓
MAIN APP
  │
  ├── Home
  │     ├── Menu
  │     ├── Search
  │     ├── Food Detail
  │     ├── Notification
  │     └── Active Dining Session
  │
  ├── Reservation
  │     ├── New Reservation
  │     ├── Reservation History
  │     ├── Reservation Detail
  │     └── Pre-order
  │
  ├── Scan QR
  │     └── Check-in
  │           ↓
  │       Dining Session
  │           ├── Dine-in Menu
  │           ├── Current Orders
  │           ├── Call Staff
  │           └── Payment
  │
  ├── Orders
  │     ├── Current Orders
  │     └── Order History
  │
  └── Profile
        ├── Favorites
        ├── Voucher
        ├── Loyalty
        ├── Settings
        └── Support
```

---

# 11. Các màn hình UI/UX chuẩn hóa

## Authentication

1. Splash
2. Welcome
3. Login
4. Register
5. Forgot Password
6. Reset Password

## Branch

7. Branch List
8. Branch Detail
9. Branch Map / Nearby
10. Change Branch

## Home

11. Home
12. Search
13. Search Result
14. Notification
15. Active Dining Session Card / Entry

## Food

16. Menu
17. Category
18. Food Detail
19. Favorites

## Reservation

20. Select Date
21. Select Time
22. Guest Count
23. Select Area
24. Table Layout
25. Reservation Note
26. Deposit
27. Reservation Confirmation
28. Reservation Detail
29. Reservation History
30. Change Reservation
31. Cancel Reservation
32. No Table Suggestion

## Pre-order

33. Pre-order Menu
34. Pre-order Cart
35. Pre-order Confirmation
36. Pre-order Detail

## Check-in & QR

37. Scan QR
38. QR Validation
39. Check-in Confirmation
40. Reservation Mismatch
41. Walk-in Confirmation

## Dining Session

42. Dining Session
43. Dine-in Menu
44. Dine-in Cart
45. Order Confirmation
46. Call Staff
47. Service Request Status

## Order

48. Current Orders
49. Order Tracking
50. Order Detail
51. Order Item Status Detail
52. Order History

## Payment

53. Payment Request / Checkout Summary
54. Invoice
55. Voucher Selection
56. Loyalty Point Selection
57. Payment Type Selection
58. Online Payment Method
59. On-site Payment Method
60. Payment Location Selection
61. On-site Payment Request Status
62. Payment Processing
63. Payment Success
64. Payment Failed
65. Split Bill

## Customer

66. Profile
67. Edit Profile
68. Voucher
69. Review
70. Loyalty
71. Settings
72. Help / Support
---

# 12. Phân chia phạm vi phát triển

## Phase 1 – MVP

1. Authentication
2. Branch Selection
3. Home + Bottom Navigation
4. Menu
5. Food Detail
6. Search
7. Reservation
8. Reservation History
9. Pre-order
10. Cart
11. QR Scan
12. Check-in
13. Dining Session
14. Dine-in Order
15. Order Tracking theo Order + Order Item
16. Current Orders / Order History
17. Profile

## Phase 2 – Hoàn thiện

18. Payment: QR Payment + thanh toán tại nhà hàng
19. Deposit
20. Cancellation Policy
21. Change Reservation
22. Voucher
23. Favorites
24. Review
25. Notification
26. Call Staff
27. Support
28. Payment Retry / Refund
29. Offline QR Order Retry
30. Ví điện tử / Thẻ online

## Phase 3 – Điểm nhấn đồ án

31. Realtime Order Tracking
32. Advanced Push Notification
33. Recommendation
34. Loyalty
35. Membership
36. Personalization
37. Split Bill
38. Customer Behavior Analytics
39. Smart Food Recommendation

---

# 13. Các rule nghiệp vụ quan trọng

## Rule 1 – Cart chỉ thuộc một branch

Không cho phép trộn món của nhiều chi nhánh trong một Cart.

## Rule 2 – Reservation luôn thuộc một branch

Reservation không được tự động chuyển sang branch khác.

## Rule 3 – Pre-order bắt buộc thuộc Reservation

Không có Reservation thì không có Pre-order.

Khách không đặt bàn sử dụng Dine-in Order.

## Rule 4 – QR xác định branch + table

QR không trực tiếp tạo Order.

QR dùng để tìm hoặc tạo Dining Session.

## Rule 5 – Một Dining Session có nhiều Order

Mỗi lần "Đặt món" là một Order mới.

## Rule 6 – Invoice theo Dining Session

Hóa đơn tổng hợp toàn bộ Order trong Dining Session.

## Rule 7 – Review sau khi hoàn thành

Chỉ cho phép Review khi giao dịch phù hợp đã hoàn tất.

## Rule 8 – Scan nhầm bàn không auto check-in

Bắt buộc xác minh Reservation hoặc nhân viên xác nhận.

## Rule 9 – Table không có hai active session

Một bàn tại cùng thời điểm không được có hai Dining Session `ACTIVE`.

## Rule 10 – Pre-order được link vào Dining Session khi check-in

Không tạo lại món.

## Rule 11 – Thanh toán theo Dining Session

Không thanh toán riêng từng Order trong cùng một phiên dùng bữa. Invoice tổng hợp tất cả Order thuộc Dining Session.

## Rule 12 – Voucher và Loyalty do khách chủ động chọn

Không tự động áp Voucher hoặc sử dụng điểm Loyalty.

## Rule 13 – PAYMENT_REQUESTED hạn chế Order mới

Khi Dining Session đang `PAYMENT_REQUESTED`, khách không thể đặt thêm món. Muốn gọi thêm món phải hủy yêu cầu thanh toán để Session quay lại `ACTIVE`.

## Rule 14 – Chỉ đóng Dining Session sau khi thanh toán thành công

```text
Payment SUCCESS
→ Dining Session PAID
→ Dining Session CLOSED
```

Thanh toán thất bại không được đóng Dining Session.

## Rule 15 – Order Status và Order Item Status tách riêng

Order Status chỉ thể hiện trạng thái tổng quát.

Trạng thái chuẩn bị / sẵn sàng / đã phục vụ được quản lý ở từng Order Item.

## Rule 16 – Gọi thêm món tạo Order mới

Không thêm trực tiếp món mới vào Order đã gửi xuống nhà hàng.

```text
Gọi thêm món
→ Create Order mới
```

## Rule 17 – Order hoàn thành khi tất cả món hoàn tất

```text
Tất cả Order Item = SERVED / CANCELLED
→ Order = COMPLETED
```

## Rule 18 – Pre-order không bị tạo lại khi check-in

Pre-order được đưa vào Dining Session hiện tại và tiếp tục tracking như một Order đã tồn tại.

## Rule 19 – Tách hình thức, phương thức và địa điểm thanh toán

Không dùng `Tại bàn / Tại quầy` như phương thức thanh toán.

```text
Hình thức
├── Online
└── Tại nhà hàng

Phương thức
├── QR Payment
├── Ví điện tử
├── Thẻ online
├── Tiền mặt
├── Thẻ tại nhà hàng
└── QR / chuyển khoản tại nhà hàng

Địa điểm
├── Tại bàn
└── Tại quầy
```

## Rule 20 – Địa điểm thanh toán chỉ áp dụng cho thanh toán tại nhà hàng

Thanh toán online không yêu cầu chọn `Tại bàn / Tại quầy`.

## Rule 21 – MVP ưu tiên QR Payment và thanh toán tại nhà hàng

MVP có thể chỉ triển khai:

- QR Payment online
- Tiền mặt tại nhà hàng
- Thẻ tại nhà hàng
- QR / chuyển khoản tại nhà hàng

Ví điện tử và thẻ online có thể đưa sang Phase 2.


## Rule 22 – Chỉ có một Home chính

`Home` là trang chủ duy nhất của Customer App.

`Dining Session` là màn trung tâm của phiên dùng bữa và không được gọi là Home.

## Rule 23 – Bottom Navigation cố định 5 mục

```text
Home | Đặt bàn | Scan QR | Đơn hàng | Tôi
```

Dining Session và Payment không phải Bottom Navigation tab.

## Rule 24 – Dining Session ACTIVE phải có entry quay lại nhanh

Khi Dining Session đang `ACTIVE` hoặc `PAYMENT_REQUESTED`, UI phải cung cấp Active Dining Session Card / Entry để user quay lại phiên dùng bữa.

## Rule 25 – Rời màn Dining Session không kết thúc phiên dùng bữa

Back hoặc chuyển tab chỉ thay đổi màn hình.

Dining Session chỉ kết thúc theo flow:

```text
Payment SUCCESS
→ PAID
→ CLOSED
```

## Rule 26 – Sau Payment Success điều hướng về Review/Home

Sau thanh toán thành công:

```text
Payment Success
→ Review (optional)
→ Home
```

Active Dining Session Card phải biến mất khi Session `CLOSED`.

## Rule 27 – Pre-order không điều hướng vào Dining Session

Pre-order hoàn tất quay về Reservation context.

Dining Session chỉ được mở khi khách thực sự check-in / scan QR hợp lệ.

---

# 14. Các thực thể nghiệp vụ cần thể hiện trong flow/UI

| Khái niệm nghiệp vụ | Phạm vi | Ý nghĩa trong chức năng / UI |
|---|---|---|
| User | Toàn hệ thống | Dùng chung mọi chi nhánh |
| Loyalty Account | Toàn hệ thống | Gắn User |
| Membership | Toàn hệ thống | Gắn User |
| Brand Menu | Toàn hệ thống | Menu gốc |
| Branch Menu Override | Chi nhánh | Giá, visible, availability |
| Branch | Chi nhánh | Nhà hàng cụ thể |
| Area | Chi nhánh | Khu vực bàn |
| Table | Chi nhánh | Bàn |
| Reservation | Chi nhánh | Giữ bàn trước |
| Dining Session | Chi nhánh + bàn | Phiên dùng bữa thực tế |
| Order | Chi nhánh | PRE_ORDER / DINE_IN |
| Order Item | Order | Snapshot giá và option |
| Invoice | Dining Session | Tổng hợp Order |
| Payment | Invoice | Giao dịch thanh toán |
| Voucher | Global / Branch | Theo campaign |
| Review | Chi nhánh / món | Theo giao dịch thực |
| Service Request | Dining Session | Gọi nhân viên |
| Cart | Session + Branch | Chỉ 1 branch |

---


# 15. Flow ngắn gọn dùng khi bảo vệ đồ án

Về UI/UX, hệ thống chỉ có **một Home chính** và sử dụng Bottom Navigation:

```text
Home | Đặt bàn | Scan QR | Đơn hàng | Tôi
```

`Dining Session` là màn trung tâm của phiên dùng bữa, không phải Home thứ hai.

Hệ thống phân biệt rõ ba khái niệm:

```text
Reservation
= giữ bàn trước

Dining Session
= khách thực tế đang dùng bàn

Order
= một lần khách gửi món xuống nhà hàng
```

Do đó:

```text
Đặt bàn trước
    ↓
Reservation

Đến nhà hàng + Scan QR
    ↓
Dining Session

Chọn món + Đặt món
    ↓
Order
```

Nếu khách đặt món trước:

```text
Reservation
    ↓
PRE_ORDER
    ↓
Check-in
    ↓
Dining Session
    ↓
Link PRE_ORDER
```

Nếu khách walk-in:

```text
Scan QR
    ↓
Dining Session
    ↓
DINE_IN ORDER
```

Đây là flow chuẩn được sử dụng xuyên suốt Customer App.
