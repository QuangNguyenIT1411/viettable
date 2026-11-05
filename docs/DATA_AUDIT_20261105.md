# Rà dữ liệu minh bạch (bước 0), 05/11/2026

Nguyên tắc (chủ dự án 05/11): mọi số liệu hiển thị phải đến từ Supabase qua API; không viết cứng, không tính riêng ở client,
không "tick cho có", không dữ liệu giả; không có dữ liệu thì ẩn hoặc hiện trạng thái trống.

Cách rà: grep app Android (`app/src/main`) và 3 web (thu ngân, admin, bếp) tìm số/đơn vị viết cứng (`đ`, `%`, `điểm`, `phút`,
`★`, mã kiểu `#RES-…`), chữ mẫu trong layout, phép tính tiền ở client, chỗ chỉ lưu trên máy; đối chiếu với code gọi API.
Script dò: `android:text` có số trên view có id, kiểm tra code có `setText` cho view đó không.

## Đã sửa trong việc này

| # | Chỗ | Vấn đề | Sửa |
|---|---|---|---|
| 1 | App → Đánh giá (`MobileRepository.createReview`) | 3 tiêu chí Món ăn / Phục vụ / Không gian **chép bằng đúng số sao tổng** ("tick cho có") | Khách tự chấm từng tiêu chí; thiếu tiêu chí nào thì không gửi được (`ReviewDraft`, test `ReviewPayloadTest`) |
| 2 | App → Hoá đơn lúc thanh toán (`fragment_invoice.xml`) | Luôn hiện "Phí phục vụ 0đ", "Thuế VAT 0đ" dù server chưa từng tính; `strings.xml` có "Phí phục vụ (5%)", "Thuế VAT (8%)" viết cứng | Số và % lấy từ báo giá; 0% thì ẩn dòng; xoá 2 chuỗi cứng |
| 3 | App → Sửa hồ sơ | Ô Ngày sinh hiện cứng "15/08/1995", ô Khẩu vị hiện cứng "Ít cay, dị ứng hải sản tôm cua" (không lưu đi đâu) | Ngày sinh lấy từ hồ sơ server, chọn bằng lịch, lưu lên server; ô Khẩu vị ẩn (server chưa có cột) |
| 4 | App → Sửa hồ sơ → Lưu | **Lỗi mất dữ liệu**: server ghi đè cả hồ sơ bằng request; app chỉ gửi tên/SĐT/email nên mỗi lần Lưu là **xoá ngày sinh, giới tính, địa chỉ** | App gửi kèm lại ngày sinh/giới tính/địa chỉ đang có |
| 5 | App → Sửa hồ sơ → "Đổi ảnh đại diện" | Bấm chỉ hiện thông báo, không làm gì | Ẩn (chưa có chức năng tải ảnh đại diện) |
| 6 | App → 29 view trong layout (mã "#RES-8821", "178.000đ", "3 món", "Tầng 1 • Khu Trong nhà • 4 người"…) | Chữ mẫu là `android:text`: nếu một nhánh code quên ghi đè thì số giả hiện ra | Chuyển sang `tools:text` (chỉ hiện trong Android Studio, chạy thật không bao giờ hiện) |
| 7 | App → Dùng điểm, câu báo lỗi | "tối đa 10% hóa đơn" viết cứng | Lấy `maxPointsDiscountPercent` từ server; không biết thì không ghi số |
| 8 | App → Điều khoản / đặt cọc | "giữ bàn trong vòng 15 phút" và mặc định "2 giờ" hoàn cọc viết cứng | Không ghi số khi server không gửi; số giờ hoàn cọc lấy từ chính sách cọc của server |
| 9 | App → Chi tiết món | Điểm sao lấy từ trường `rating` của danh mục (luôn 0, không có nguồn) | Lấy trung bình + số lượt + nhận xét mới nhất từ đánh giá thật (`/dishes/{id}/reviews/summary`); chưa có thì "Chưa có đánh giá" |
| 10 | App → Lịch sử hoá đơn | Tên chi nhánh tự ghép "Chi nhánh 1" khi không tìm thấy; dòng tiền tự cộng ở app | Mọi dòng từ snapshot hoá đơn trên server (`InvoiceViews.Map`); hoá đơn cũ không có snapshot thì lấy tên chi nhánh hiện tại từ danh sách chi nhánh của server, không bịa |
| 11 | Backend → hoá đơn | Hoá đơn app không lưu điểm dùng/voucher/phí/VAT/chi nhánh/bàn/giờ/thu ngân; hoá đơn quầy không lưu danh sách món | Snapshot đầy đủ khi thanh toán (`InvoiceSnapshot`, cột mới `Bills.ItemsJson`…) |
| 12 | Web thu ngân → khung thanh toán | Không hiện phí/VAT (chưa có) | Hiện "Phí phục vụ (x%)", "VAT (y%)" từ `checkout/preview` khi > 0 |

## Giữ nguyên, có lý do

| Chỗ | Vì sao giữ |
|---|---|
| App → giỏ món trước khi gửi ("Tạm tính đợt gọi này") | Cộng đơn giá server × số lượng khách chọn, chỉ để xem trước; server tính lại khi gửi đơn và khi báo giá |
| App → khung giờ đặt bàn 10:00–21:30, bước 30 phút (`ReservationSchedule`) | Là quy tắc nghiệp vụ viết ở cả server và app (server kiểm lại khi đặt); chưa có bảng giờ mở cửa theo chi nhánh trong DB. Làm bảng giờ theo chi nhánh là việc riêng |
| App → chi nhánh đã chọn, tên chào, chế độ Sáng/Tối, giỏ món chưa gửi | Lưu trên máy là đúng (lựa chọn của thiết bị / bộ nhớ đệm); dữ liệu thật vẫn ở server |
| Web admin → "Tổng doanh thu" khi lọc báo cáo, "số ghế trung bình" | Chỉ cộng/chia các số server trả về cho đúng các dòng đang lọc; không tạo số mới |
| Web thu ngân → ô "giảm %" | Đổi % thu ngân gõ thành số tiền rồi gửi; server tính lại toàn bộ bằng `CheckoutPricing` |
| Web thu ngân → kết quả sau khi thanh toán | Hiện đúng số server trả về; phiếu đầy đủ (có phí/VAT) in từ Lịch sử → "In / Xuất" |

## Chưa làm (cần quyết định riêng)

- Khẩu vị / dị ứng của khách: cần cột mới ở Identity nếu muốn lưu.
- Ảnh đánh giá: không làm (xem báo cáo cuối: chưa có đường tải ảnh của khách lên Supabase Storage có giới hạn và kiểm duyệt).
- Giờ mở cửa / khung đặt bàn theo chi nhánh trong DB.
