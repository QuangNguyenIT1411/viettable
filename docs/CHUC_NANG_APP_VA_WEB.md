# VietTable — Chức năng của app khách và liên hệ với web nhân viên

> Cập nhật 30/10/2026, theo mã nguồn branch `qa/vt-ho-01` và bản đang chạy `https://viettable-beta.vercel.app`.

## 1. Hệ thống gồm những gì

| Thành phần | Ai dùng | Ở đâu |
|---|---|---|
| **App khách (Android)** | Khách hàng | File APK, cài trên điện thoại |
| **Web Bếp** | Đầu bếp | `https://viettable-beta.vercel.app/app/chef/` |
| **Web Thu ngân** | Thu ngân | `https://viettable-beta.vercel.app/app/cashier/` |
| **Web Quản trị** | Quản lý | `https://viettable-beta.vercel.app/app/admin/` |

App và 3 web **không gọi trực tiếp cho nhau**. Tất cả dùng chung **một máy chủ** (API trên Vercel) và **một cơ sở dữ liệu** (Supabase).
Khi một bên thay đổi dữ liệu, máy chủ **đẩy thông báo tức thời (realtime)** cho các bên còn lại, nên màn hình tự cập nhật mà không cần tải lại.

```
 App khách ──┐                        ┌── Web Bếp
             ├──►  Máy chủ VietTable ◄─┼── Web Thu ngân
             │     (API + realtime)    └── Web Quản trị
             │            │
             │      Cơ sở dữ liệu
             └── nhận thông báo tức thời (trạng thái món, thanh toán, đặt bàn…)
```

**Luồng chính của một bữa ăn:**

1. Khách mở app → chọn chi nhánh → (tuỳ chọn) đặt bàn.
2. Đến quán → **quét QR trên bàn** → mở **phiên bàn**.
3. Khách **gọi món** trên app → món hiện ngay ở **Web Bếp**.
4. Bếp bấm **Bắt đầu nấu → Hoàn thành** → app khách thấy tiến độ từng món.
5. Khách **yêu cầu thanh toán** trên app (chọn voucher / điểm, tiền mặt hoặc chuyển khoản).
6. **Thu ngân xác nhận** đã thu tiền (hoặc chuyển khoản được hệ thống tự xác nhận) → app hiện **hoá đơn + điểm thưởng**, bàn được trả về trống.
7. Khách **đánh giá** bữa ăn.

---

## 2. Chức năng app khách (theo nhóm) và bên web liên quan

### 2.1. Tài khoản và đăng nhập

| Chức năng | Mô tả | Liên hệ web |
|---|---|---|
| Chào mừng, Đăng ký, Đăng nhập | Bằng tên đăng nhập/mật khẩu | Quản trị → **Khách hàng** thấy tài khoản mới, sửa thông tin, khoá tài khoản, xem nhật ký |
| Đăng nhập Google | Dùng tài khoản Google | Như trên |
| Quên / đặt lại mật khẩu | Gửi yêu cầu, đặt mật khẩu mới | — |
| Đổi mật khẩu | Trong mục Tôi | — |
| Khoá app bằng vân tay | Mở app phải xác thực vân tay | Chỉ trên điện thoại |
| Đăng xuất | Xoá phiên trên máy | — |

### 2.2. Chi nhánh, thực đơn, tìm kiếm

| Chức năng | Mô tả | Liên hệ web |
|---|---|---|
| Danh sách / chi tiết / bản đồ chi nhánh, đổi chi nhánh | Chọn quán để xem thực đơn | Quản trị quản lý chi nhánh |
| Trang chủ | Lời chào, banner khuyến mãi đang chạy, danh mục, món nổi bật, thẻ "đang ở bàn" | Khuyến mãi do Quản trị tạo |
| Thực đơn, danh mục, chi tiết món | Giá, mô tả, ảnh, kích cỡ | Quản trị → **Danh mục, Món ăn**; Bếp → **Thực đơn: "Tạm ngưng bán"** thì app không cho gọi món đó |
| Tìm kiếm món | Tìm theo tên | — |
| Món yêu thích | Lưu / bỏ lưu món | — |

### 2.3. Đặt bàn, đặt cọc, đặt món trước

| Chức năng | Mô tả | Liên hệ web |
|---|---|---|
| Đặt bàn 5 bước | Chọn ngày → giờ → số khách → bàn trên sơ đồ → ghi chú | Thu ngân → tab **Đặt bàn**: thấy đặt bàn mới, **Xác nhận / Từ chối (có lý do) / Khách không đến** |
| Không còn bàn | Báo hết bàn, gợi ý giờ khác | — |
| Lịch sử, chi tiết, đổi, huỷ đặt bàn | Khách tự đổi giờ / huỷ | Thu ngân thấy thay đổi ngay (realtime) |
| **Đặt cọc** (đang bật) | Từ **6 khách**: 50.000đ/khách, chuyển khoản VietQR trong **15 phút**, quá hạn tự huỷ và nhả bàn; trả trước ≥ 2 giờ thì được hoàn | Thu ngân → **Đặt bàn → Đặt cọc**: theo dõi cọc, bấm **"Đã hoàn cọc"** sau khi chuyển trả |
| Đặt món trước | Chọn món kèm đặt bàn | Khi thu ngân **Nhận khách**, món đặt trước tự thành đơn gửi bếp |
| Nhắc lịch | Thông báo trên máy trước giờ đặt bàn | Chỉ trên điện thoại |

### 2.4. Vào bàn (mở phiên)

| Chức năng | Mô tả | Liên hệ web |
|---|---|---|
| Quét QR trên bàn / nhập mã tay | Ví dụ mã `QR-Q1-B11` | Mã QR in từ Quản trị → **Bàn & QR** |
| Khách vãng lai | Mở phiên mới, chọn số khách | Thu ngân thấy bàn chuyển **"Có khách"** |
| Khách đã đặt bàn | Check-in theo đặt bàn (báo nếu đặt bàn không khớp bàn/giờ) | Thu ngân cũng có thể **Nhận khách** từ tab Đặt bàn |
| Trang phiên bàn | Tổng tạm tính, số món, số đợt gọi, nút gọi thêm / tiến độ / gọi nhân viên / thanh toán, **Huỷ bàn** khi chưa gọi món | — |

Mỗi bàn chỉ có **một phiên** đang mở; mỗi khách chỉ mở **một phiên** một lúc.

### 2.5. Gọi món tại bàn và theo dõi

| Chức năng | Mô tả | Liên hệ web |
|---|---|---|
| Gọi món nhiều đợt | Thực đơn tại bàn → giỏ → ghi chú cho bếp → **Gửi đơn tới Bếp** | Đơn hiện ngay ở **Web Bếp** cột "Chờ chế biến" |
| Đơn hiện tại, chi tiết đơn | Xem từng đợt gọi | — |
| **Tiến độ món** | "x/y món đã xong", trạng thái từng món, thanh 4 bước | Cập nhật khi Bếp bấm **Bắt đầu nấu / Hoàn thành** |
| **Giục bếp làm nhanh** | Trong chi tiết món | Bếp thấy cờ **"Khách giục món"** |
| **Xin huỷ món** | Gửi yêu cầu, chỉ huỷ khi bếp/thu ngân đồng ý | Bếp thấy cờ **"Khách xin hủy món"**, quyết định **Hủy món** |

### 2.6. Gọi phục vụ

| Chức năng | Mô tả | Liên hệ web |
|---|---|---|
| Gọi nhân viên, xin nước, xin dụng cụ, yêu cầu khác | Gửi từ trang phiên bàn, theo dõi trạng thái | Thu ngân → tab **Gọi phục vụ**: **Tiếp nhận → Hoàn thành**, app thấy trạng thái đổi |

### 2.7. Thanh toán

| Chức năng | Mô tả | Liên hệ web |
|---|---|---|
| Kiểm tra trước thanh toán | Cảnh báo nếu còn món chưa xong (vẫn cho thanh toán) | — |
| **Hoá đơn** | Tổng tiền món, **Khuyến mãi tự động**, voucher, điểm, đã đặt cọc, tổng thanh toán | Thu ngân thấy **đúng các con số này** (lấy từ máy chủ) |
| **Khuyến mãi tự động** | Tự áp nếu đủ điều kiện (ví dụ "blackday": giảm 50%, tối đa 50.000đ, đơn từ 300.000đ); có nút **"Không dùng khuyến mãi"** | Quản trị → **Khuyến mãi** |
| **Voucher** | Chọn voucher; voucher không dùng được hiện mờ kèm lý do; **"Bỏ voucher"** | Quản trị → **Voucher** (giới hạn lượt mỗi khách / tổng) |
| **Dùng điểm** | Đổi điểm thành tiền giảm | Thu ngân cùng quy tắc đổi điểm |
| Chia hoá đơn | Chia theo người / phần | Mỗi phần thanh toán riêng ở thu ngân |
| **Tiền mặt** (tại bàn / tại quầy) | Gửi yêu cầu → màn "Đang chờ thanh toán" | Thu ngân → tab **Chờ thu tiền** → **Xác nhận đã thu tiền** |
| **Chuyển khoản VietQR** | Mã QR có sẵn số tiền + nội dung; nút sao chép số tài khoản, số tiền, nội dung | Ngân hàng báo về (SePay) → **hệ thống tự xác nhận**; lệch thì vào tab **Chuyển khoản** để thu ngân xử lý tay |
| Thanh toán online VNPAY | **Đang tắt** (ẩn trên app) | — |
| Thanh toán thành công / thất bại | Hoá đơn, phương thức, bàn và chi nhánh, **"+X điểm"** | — |

Sau khi đã **yêu cầu thanh toán**, hoá đơn bị **khoá**: không đổi voucher / điểm nữa (muốn đổi phải huỷ yêu cầu).
Khi thanh toán xong: bàn trở về **Trống**, đơn ở bếp tự ghi **Hoàn tất**, doanh thu cộng vào báo cáo thu ngân và quản trị.

### 2.8. Sau bữa ăn, ưu đãi, thông báo, hồ sơ

| Chức năng | Mô tả | Liên hệ web |
|---|---|---|
| Lịch sử đơn, lịch sử hoá đơn | Xem lại các lần ăn | Thu ngân → **Lịch sử**; Quản trị → **Báo cáo** |
| **Đánh giá** | Chỉ khi đã có hoá đơn; xem lại đánh giá đã viết | — |
| **Ưu đãi** | Danh sách khuyến mãi đang chạy, chi tiết | Quản trị → **Khuyến mãi** (tạo mới sẽ gửi "Ưu đãi mới" vào hộp thông báo khách) |
| Ví voucher | "Có thể dùng" / "Đã dùng – Hết hạn" | Quản trị → **Voucher** |
| **Thẻ thành viên / điểm** | Số điểm, hạng (Thành viên / Bạc / Vàng / Kim cương), tiến độ lên hạng | Quản trị → **Khách hàng** xem / sửa điểm |
| Hộp thông báo | Món đã xong, đã thanh toán, điểm cộng, ưu đãi mới, đặt bàn đổi trạng thái | Sinh ra từ thao tác của bếp / thu ngân / quản trị |
| Hồ sơ, cài đặt | Sửa thông tin, chi nhánh mặc định, ngôn ngữ, bật/tắt thông báo | — |
| Hỗ trợ | Câu hỏi thường gặp (FAQ), gửi yêu cầu hỗ trợ | — |

---

## 3. Chức năng các web nhân viên

### Web Bếp (`/app/chef/`)
- **Đơn hàng**: 3 cột *Chờ chế biến → Đang chế biến → Sẵn sàng*; bấm **Bắt đầu nấu / Hoàn thành** cho cả đơn hoặc từng món; **Hủy món** (có lý do).
- Cờ yêu cầu từ khách: **Khách giục món**, **Khách xin hủy món**.
- **Thực đơn**: **Tạm ngưng bán** món (app khách không gọi được).
- **Kho nguyên liệu**, **Lịch sử & Tài khoản**.

### Web Thu ngân (`/app/cashier/`)
Dải số liệu (đơn hôm nay, doanh thu, bàn có khách, chờ thu tiền) và **5 nút lớn có số đếm việc đang chờ**:

| Tab | Việc |
|---|---|
| **Quầy thanh toán** | Chọn bàn → xem đơn → giảm giá / điểm → thu tiền mặt hoặc chuyển khoản → in hoá đơn |
| **Chờ thu tiền** | Giao dịch từ app: **Xác nhận đã thu tiền**, **Hủy giao dịch chờ**, hiện mã QR |
| **Đặt bàn** | Đặt bàn chờ duyệt (xác nhận / từ chối / khách không đến), đặt bàn theo ngày, **Nhận khách**, theo dõi và hoàn **đặt cọc** |
| **Gọi phục vụ** | Tiếp nhận / hoàn thành yêu cầu của khách |
| **Chuyển khoản** | Khoản chuyển cần xử lý tay |

Ngoài ra: **Báo cáo theo ngày**, **Lịch sử & Tài khoản**.

### Web Quản trị (`/app/admin/`)
- **Tổng quan**: nhân viên, đơn đang làm, trạng thái bàn, doanh thu hôm nay.
- **Danh mục, Món ăn, Đơn vị**; **Bàn & QR** (in mã QR cho bàn).
- **Khách hàng**: thêm, sửa, khoá, nhật ký hoạt động, điểm thưởng.
- **Nhân viên**: thêm, sửa, lịch sử.
- **Nguyên liệu, Kho**: nhập kho, xuất kho, lô hàng, lịch sử biến động.
- **Khuyến mãi**, **Voucher**.
- **Báo cáo**: doanh thu, món gọi nhiều.

---

## 4. Thông báo tức thời (realtime) giữa app và web

| Sự kiện | Ai gây ra | Ai thấy ngay |
|---|---|---|
| Đơn mới / món đổi trạng thái | App gọi món; Bếp nấu / hoàn thành / huỷ | Bếp, Thu ngân, app khách (tiến độ món) |
| Yêu cầu phục vụ | App gửi; Thu ngân tiếp nhận / hoàn thành | Thu ngân, Bếp (cờ giục / xin huỷ), app khách |
| Thanh toán | App yêu cầu; Thu ngân xác nhận; ngân hàng báo về | Thu ngân (số "Chờ thu tiền"), app khách (màn thành công) |
| Đặt bàn đổi trạng thái | App đặt / đổi / huỷ; Thu ngân xác nhận / từ chối | Thu ngân, app khách |
| Thông báo mới (điểm, ưu đãi…) | Máy chủ | Hộp thông báo app khách |

Nếu mất kết nối realtime, app và web tự **hỏi lại định kỳ**, nên dữ liệu vẫn cập nhật (chậm hơn vài giây).

---

## 5. Quy tắc nghiệp vụ chính

- **Điểm thưởng**: tích **1 điểm / 1.000đ** đã trả. Đổi **1 điểm = 10đ**, theo bước **1.000 điểm**, tối đa **10% hoá đơn**. App và thu ngân dùng chung quy tắc.
- **Khuyến mãi và voucher không cộng dồn**: hệ thống chọn cái **lợi hơn cho khách**.
- **Voucher**: mỗi lượt được tính khi khách **yêu cầu thanh toán**; huỷ yêu cầu thì **trả lại lượt**.
- **Đặt cọc**: từ 6 khách, 50.000đ/khách; huỷ trước ≥ 2 giờ hoặc nhà hàng từ chối thì hoàn; huỷ muộn / không đến thì mất cọc. Tiền cọc tự trừ vào hoá đơn.
- **Đánh giá** chỉ mở khi đã có hoá đơn.

## 6. Lưu ý khi thử

- Toàn bộ dữ liệu hiện tại là **dữ liệu thử**, mọi thao tác đều ghi vào cơ sở dữ liệu thật đang dùng cho demo.
- Thanh toán online **VNPAY đang tắt**.
- Máy chủ dùng gói Vercel miễn phí: lần mở đầu sau một lúc không dùng có thể chậm vài giây (máy chủ "thức dậy").
