# VietTable — kịch bản bảo vệ 8–12 phút

Sơ đồ nói ngắn: **Android → Gateway `:5100` → Catalog, Orders, Customers, Identity, Billing → mỗi service sở hữu database riêng**. RabbitMQ vận chuyển event; SignalR báo thay đổi để Android tải lại trạng thái chuẩn qua REST. Samsung dùng `adb reverse` tới Gateway local, không gọi service/SQL trực tiếp.

| Bước | Thao tác | Nói gì / bằng chứng |
|---|---|---|
| 1. Đăng nhập | Mở VietTable, login demo customer. | JWT do Identity phát hành qua Gateway; refresh rotation và Android Keystore bảo vệ phiên. |
| 2. Chi nhánh/menu | Home → Branch → Menu → Category → Item. | Catalog trả danh mục, món, giá, ảnh thực; không có production scenario fallback. |
| 3. QR/check-in | Dùng `demo-find-table.ps1` ngay trước demo; nhập `QrCode` đã xác minh rồi xác nhận walk-in. Camera có thể thử thêm nếu có QR vật lý. | Rehearsal 2026-09-24 dùng manual QR qua Gateway; camera mở được nhưng decode vật lý chưa nghiệm thu trong lượt này. Không tự dựng session. |
| 4. Phiên phục vụ | Mở Dining Session. | Bàn và phiên được lưu trong Orders. Một bàn không thể có hai phiên đang hoạt động. |
| 5. Gọi món | Thêm món → Cart → Submit → Order Tracking. | Giá/snapshot và trạng thái đơn do backend quyết định; có thể gọi nhiều lượt. |
| 6. Gọi nhân viên | Tạo service request. | Event qua RabbitMQ/SignalR giúp app cập nhật; REST vẫn là nguồn dữ liệu chính. |
| 7. Thanh toán | Từ Dining Session mở kiểm tra trước thanh toán; nếu món chưa hoàn thành, xác nhận tiếp tục một cách có chủ ý. Xem **quote** rồi chọn ONSITE/CASH/CASHIER. | Billing tính tổng trên server; màn “Hóa đơn thanh toán” trước payment là quote, chưa phải invoice đã phát hành. Mobile chỉ thấy PENDING. |
| 8. Thu tiền | Trên Cashier web đúng chi nhánh, đối chiếu payment code, bàn, số tiền rồi xác nhận đúng hàng một lần. | Cashier có quyền mới xác nhận; Android tự nhận SUCCEEDED qua polling, invoice thật được phát hành và session đóng. |
| 9. Đánh giá | Từ invoice/payment success tạo review. | Review chỉ gắn với giao dịch đủ điều kiện, lưu ở backend. |

Nếu còn thời gian: Split Bill hai phần (chỉ đóng phiên sau khi cả hai phần được thanh toán), FAQ/Support, Favorites, Notifications. Reservation là nhánh demo bổ sung: chọn ngày/giờ, kiểm tra availability, tạo và xem lại reservation thật; không tạo trước nếu fixture có thể xung đột.

Main flow trên đã được rehearsal trên Samsung + Cashier UI ngày 2026-09-24 với manual QR và ONSITE non-split. Đừng coi physical QR, SignalR trigger hay Split Bill là đã PASS trong lượt này. Xem [DEMO_ACCEPTANCE.md](DEMO_ACCEPTANCE.md) để biết evidence và giới hạn.

**Câu hỏi hay gặp**

- “Có dữ liệu giả không?” Luồng production Android không gọi `applyScenario`/MockData; dữ liệu chính lấy qua Gateway. Test fixture chỉ phục vụ kiểm thử. Chỉ khẳng định sau khi preflight và màn demo đã tải dữ liệu thật.
- “Bảo mật?” JWT, refresh rotation/revoke, Android Keystore AES-GCM; release policy yêu cầu HTTPS. Demo local qua USB không phải production HTTPS.
- “Vì sao 5 database?” Catalog=`RESTAURANT_CATALOG`, Orders=`RESTAURANT_ORDERS`, Customers=`RESTAURANT_CUSTOMERS`, Identity=`RESTAURANT_IDENTITY`, Billing=`RESTAURANT_BILLING`; Gateway không sở hữu DB.
- “Google/VNPAY/HTTPS?” Adapter Google/VNPAY đã có nhưng E2E external còn phụ thuộc credential/callback; production Compose/Caddy đã chuẩn bị nhưng chưa có VPS/domain/TLS public. ONSITE là payment flow demo chính.
