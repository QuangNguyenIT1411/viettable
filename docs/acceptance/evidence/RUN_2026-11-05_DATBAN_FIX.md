# Test tay sửa lỗi đặt bàn, 05/11/2026

- Thiết bị: Samsung SM-A528B (`R5CRC14LFYD`). Chủ dự án đổi từ `bs_check` sang Samsung giữa chừng; không có kết quả trên `bs_check`.
- APK: `cloudProd`, SHA-256 bắt đầu `660B39B57079C050`, cài 05/11 15:39:19 (`dumpsys` lastUpdateTime).
- Backend: Production `https://viettable-beta.vercel.app`, không đổi và không deploy.
- Tài khoản: tài khoản đang đăng nhập trên máy, hiện tên "Vinh" (chủ dự án xác nhận là tài khoản test).
- Ảnh: `screenshots/datban_fix/`.

| Bước | Thao tác | Kết quả thấy trên máy | Ảnh |
|---|---|---|---|
| 1 | Đặt bàn #30, 17:00 07/11, Bàn 15, 4 khách, có ghi chú | Màn xác nhận "Đã gửi yêu cầu đặt bàn", 17:00 | `01_dat_ban_co_ghi_chu.png` |
| 2 | Chi tiết → Đổi đặt bàn, chỉ đổi giờ sang 18:30 | Form điền sẵn ghi chú; sau khi lưu Chi tiết hiện 18:30, ghi chú còn nguyên | `02c_form_ghi_chu_dien_san.png`, `02_doi_gio_ghi_chu_con_nguyen.png` |
| 3 | Back về màn xác nhận | Hiện 18:30 | `03_ve_xac_nhan_gio_moi.png` |
| 4 | Huỷ #30, Back về màn xác nhận | "Đã huỷ đặt bàn", icon đỏ, không còn nút Xem chi tiết, có "Đặt bàn mới" và "Về trang chủ" | `04_ve_xac_nhan_da_huy.png` |
| 5 | Bấm "Đặt bàn mới" | Mở bước 1 đặt bàn | `05a_dat_ban_moi.png` |
| 6 | Đặt bàn #31 17:00, đổi sang 18:30 | Trang chủ và Lịch sử đều hiện 18:30, ghi chú còn | `06a_…`, `06b_trang_chu_sau_doi.png`, `06c_lich_su_sau_doi.png` |
| 7 | Huỷ #31 | Lịch sử mục "Sắp tới" trống, Trang chủ không còn thẻ đặt bàn sắp tới | `07a_lich_su_sau_huy.png`, `07b_trang_chu_sau_huy.png` |

Dữ liệu test: #30 và #31 đều đã huỷ (trạng thái "Đã hủy" trên Chi tiết). Logcat crash trống.

**Chưa test tay:** trạng thái lỗi mạng + "Thử lại" (không tắt mạng của máy); vòng tải chưa chụp được rõ trong ảnh.
Hai ảnh `03a_dang_tai.png`, `04c_dang_tai.png` chụp lúc chuyển màn, không phải bằng chứng của trạng thái tải.

**Backend (chưa sửa):** `CustomersController.cs:380` gán `reservation.Note = NormalizeOptional(request.Note, 1000)`, nên request đổi lịch
thiếu `note` sẽ xoá ghi chú cũ. App hiện luôn gửi ghi chú nên không còn dính lỗi này.
