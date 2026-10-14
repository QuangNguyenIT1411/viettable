# Rà soát giao diện web staff — 13/10/2026 (giai đoạn 1, chưa sửa)

Nguồn: chạy LOCAL (docker, gateway `localhost:5100`, code branch `design/staff-redesign` = `qa/vt-ho-01` HEAD `8490469` +
sửa Dockerfile local), Chrome 1366×768. Ảnh chụp toàn trang: `docs/design/baseline-2026-10-13/`.
Dữ liệu là seed local + dữ liệu test, không phải Production.

## 1. Danh sách màn hình

| Web | Màn hình | Đường dẫn |
|---|---|---|
| Chung | Đăng nhập nhân viên (cả 3 vai trò), Quên/Đặt lại mật khẩu | `/app/chef/Staff/Account/Login`, `…/ForgotPassword`, `…/ResetPassword` |
| Bếp | Bảng bếp 3 cột (Chờ chế biến / Đang chế biến / Sẵn sàng), tab Thực đơn, hộp Thành phần món, hộp Hủy món/đơn | `/app/chef/Staff/Chef/Index` |
| Bếp | Tài khoản & Lịch sử bếp (thông tin, đổi mật khẩu, 100 đơn gần nhất) | `/app/chef/Staff/Chef/History` |
| Bếp | Kho nguyên liệu (từ nút trên thanh đầu) | trong Dashboard |
| Thu ngân | Quầy thu ngân: thống kê ngày, bảng "Thanh toán ứng dụng tại chỗ", Yêu cầu phục vụ, Đặt bàn hôm nay, Chọn bàn, Chi tiết đơn, Thanh toán (tiền mặt/QR), hộp Thanh toán thành công, hộp Nhận khách | `/app/cashier/Staff/Cashier/Index` |
| Thu ngân | Tài khoản & Lịch sử hóa đơn | `/app/cashier/Staff/Cashier/History` |
| Thu ngân | Báo cáo doanh thu theo ngày (+ In) | `/app/cashier/Staff/Cashier/Report` |
| Admin | Tổng quan | `/Admin/Dashboard/Index` |
| Admin | Danh mục (danh mục, đơn vị, trạng thái) | `/Admin/Categories/*`, `/Admin/Units/*` |
| Admin | Nguyên liệu; Quản lý kho (tổng quan, nhập, xuất, lô & hạn, lịch sử) | `/Admin/Ingredients/*`, `/Admin/Inventory/*` |
| Admin | Món ăn (danh sách, thêm, sửa, thành phần) | `/Admin/Dishes/*` |
| Admin | Bàn & QR (danh sách, sửa, trang mã QR) | `/Admin/TablesQR/*` |
| Admin | Nhân viên (danh sách, thêm, sửa, nhật ký) | `/Admin/Employees/*` |
| Admin | Khách hàng (danh sách, thêm, sửa, nhật ký) | `/Admin/Customers/*` |
| Admin | Báo cáo doanh thu, Top món | `/Admin/Reports/*` |
| Admin | Cài đặt tài khoản | `/Admin/Settings/Index` |

## 2. Điểm chưa đẹp / chưa nhất quán

### Chung cho cả 3 web
1. **Ba phong cách khác nhau**: Bếp nền tối + cam/xanh dương gradient; Thu ngân đầu trang tối + thân trắng + xanh lá/xanh dương;
   Admin nền sáng + xanh dương. Không có bộ màu chung (CSS riêng mỗi app: bếp 2.698 dòng/88 mã màu, thu ngân 1.634 dòng/67 mã màu,
   admin 441 dòng/44 mã màu; mỗi app chỉ ~10 biến CSS, admin 0).
2. **Thương hiệu sai**: trang đăng nhập ghi "Self Restaurant", tiêu đề tab "Self Restaurant Chef/Cashier/Admin"; app khách và trang
   chủ Production đã là "VietTable".
3. **Font không thống nhất**: bếp dùng font hệ thống của Bootstrap, thu ngân/admin dùng Segoe UI; cỡ chữ và độ đậm khác nhau giữa các app.
4. **Ngày giờ nhiều kiểu**: `2026-10-09` (Admin báo cáo), `13/10/2026`, `09:12 09-10` (Bếp lịch sử), `19:27:57 13/10/2026`.
5. **Khả năng truy cập**: icon Bootstrap Icons lọt vào tên nút (trình đọc màn hình đọc ký tự lạ trước "Món ăn"); nút đăng xuất ở
   bảng bếp chỉ có icon, không có nhãn; ô nhập dùng nhãn nổi (placeholder) nên khó đọc khi đã nhập.
6. Bootstrap 5.3 + Bootstrap Icons tải từ CDN, chồng lên CSS riêng → nhiều quy tắc ghi đè nhau.

### Bếp
7. **Nút mờ khó đọc**: "HỦY MÓN", "HỦY" (chữ trắng trên nền hồng nhạt), tab "Thực Đơn" (chữ xám trên nền trắng) gần như không đọc được.
8. Thẻ đơn trắng trên nền tối, chữ "ĐƠN #9016" nhỏ (≈12px) trong khi đứng bếp cần đọc từ xa; giờ chờ không nổi bật (không đổi màu khi chờ lâu).
9. Nút "Bắt đầu" (từng món) và "BẮT ĐẦU NẤU" (cả đơn) giống nhau → dễ bấm nhầm; chữ hoa/thường lẫn lộn.
10. Trang Lịch sử: bảng hẹp, cột ngày giờ xuống dòng, nút "Quay về màn hình bếp" chữ xanh trên nền xanh (gần như không thấy).

### Thu ngân
11. **Bố cục đẩy việc chính xuống dưới**: bảng "Thanh toán ứng dụng tại chỗ" hiện toàn bộ lịch sử giao dịch (cả "Đã thu tiền"),
    với dữ liệu thật dài hàng nghìn px → "Chọn bàn / Chi tiết đơn / Thanh toán" bị đẩy xuống cuối trang.
12. Bảng đó hiện mã dài `PAY-2026…`, `DS-2026…`, `CASH / CASHIER` (chưa tiếng Việt), "Bàn #5" dùng ID chứ không phải số bàn.
13. Ô chọn bàn nhỏ (6 ô/cột hẹp), trạng thái "Trống/Có khách" chữ nhỏ; nút "Xác nhận thanh toán" mờ khi chưa chọn bàn nhưng không nói lý do.
14. Trang Lịch sử và Báo cáo khác hẳn Dashboard (xanh dương sáng, nút xanh lá đổ bóng).

### Admin
15. Bảng có 4 nút cùng cỡ mỗi dòng ("Sửa", "QR", "Ẩn bàn", "Xóa"), "Ẩn bàn" đỏ đậm như "Xóa" → thao tác phá hủy không được phân biệt.
16. Tiêu đề trùng lặp: "Quản lý bàn & mã QR" xuất hiện 2 lần (đầu trang + thẻ), "Báo cáo doanh thu" 3 lần.
17. Tổng quan: 3 cột hẹp, bảng "Nhân viên gần đây" bị bóp chữ xuống nhiều dòng; thẻ "Chi nhánh" khoảng trắng lớn.
18. Báo cáo chỉ là bảng số, không có biểu đồ hay tổng nổi bật.

## 3. Lỗi chức năng phát hiện khi viết test (KHÔNG thuộc phạm vi redesign)

| # | Lỗi | Bằng chứng |
|---|---|---|
| F1 | Hóa đơn của phiên app (thu ngân xác nhận tại chỗ **hoặc** bấm Thanh toán trên dashboard) **không hiện** trong "Lịch sử hóa đơn" và "Báo cáo doanh thu theo ngày" của thu ngân: thanh toán app tạo `Invoices`, hai trang này chỉ đọc `Bills` (`CashierBillsController`). | Test `cashier.spec.ts` "hóa đơn của phiên app hiện trong Lịch sử…" (`test.fixme`); ảnh Báo cáo ngày 13/10 = 0 hóa đơn |
| F2 | `Dockerfile` gateway local không chép `src/Frontend/shared/` → build local hỏng từ khi có `shared/`. **Đã sửa** trên branch này (chỉ ảnh hưởng docker local, Production dùng `Dockerfile.staff-preview`). | `docker compose build` lỗi TS2307 trước khi sửa |
