# 06/11/2026 tối — Nhập cấu hình demo + test tay với 2 tài khoản thử (Production)

Chủ dự án duyệt `docs/DEMO_CONFIG_20261106.md`, đăng nhập Admin và thu ngân (`cashier_lan`, Quận 1) trên trình duyệt trong app
Claude, tự đăng ký 2 tài khoản thử (Claude không tạo tài khoản / không nhập mật khẩu trên Production):
**Test A** = "Quang1" trên Samsung SM-A528B, **Test B** = "Quang2" trên máy ảo `bs_check` (APK `cloudProd`).

## 1. Cấu hình đã nhập (giao diện Admin, không SQL)

Chi tiết trong `docs/DEMO_CONFIG_20261106.md` (phần "ĐÃ CHỐT"). Tóm tắt: 44/54 món có loại; bỏ chay ID 9, 53; cay 32/34 Không, 26 Có;
khẩu phần 48 = 4, 49 = 4, 37 = 3; Trà đá "Trà đá giải khát"; thêm ID 58 "Đậu hũ sốt cà chua" 55.000đ và ID 59 "Cơm chiên chay" 50.000đ
(cả 3 chi nhánh); 20 bàn Quận 1 chia 4 khu + 13 cặp ghép; toạ độ Quận 1. **Chưa nhập toạ độ Gò Vấp, Bình Thạnh**: admin đang dùng
chỉ quản lý Quận 1. Kiểm tra lại bằng API công khai sau khi nhập: khớp hết (2 ô tích chay phải bấm lại lần 2 mới nhận).

## 2. Kết quả test

| # | Mục | Kết quả | Bằng chứng |
|---|---|---|---|
| 2 | Chỉ đường | **Đạt** | Chi tiết Quận 1 có "Chỉ đường"; intent `geo:10.774300,106.703600?q=…(Chi nhánh Quận 1)`; chi tiết đặt bàn cũng có nút. |
| 7 | Gợi ý món 4 người, 800.000đ, 1 chay | **Đạt** | Server: Đậu hũ sốt cà chua (chay) + Bò né + Lẩu thái (4 người) + Gỏi ngó sen + Canh chua + 4 Trà tắc + 2 Flan = 685.000đ (phí/VAT Quận 1 đang 0%). Đổi Bò né → Phở bò: 675.000đ. Dòng "Tổng dự kiến, chưa gồm khuyến mãi/voucher/điểm" có. "Thêm tất cả vào giỏ (11 món)" → giỏ 11 món 685.000đ; đã xoá giỏ. |
| 5 | Ghép bàn 10 người Quận 1 | **Đạt** | App gợi ý "Ghép Bàn 16 + 17 · 12 chỗ" (2 bàn 6, ít bàn nhất, thừa 2 ghế). Test A đặt #37 (07/11 19:00, 10 khách, chờ cọc 500.000đ, "Bàn 16 + 17"). Thu ngân: "Bàn 16 (bàn chính), Bàn 17", nhóm đặt cọc. |
| 4 | Danh sách chờ | **Đạt (vào chờ → huỷ → mời → nhận)** | Test B 17 khách 07/11 19:00 (chỉ vừa 16+17+18, đang bị #37 giữ): "Hết bàn" gợi ý 16:30/17:00/21:00/21:30 → "Vào danh sách chờ" 19:00–20:00 → "Đang chờ · vị trí thứ 1". Test A huỷ #37 → Test B "Có bàn cho bạn · Bàn lúc 19:00 · còn 19:38" (giữ **20 phút**) → "Nhận bàn" → đặt bàn #38 "Bàn 16 + 17 + 18", chờ cọc 850.000đ. Thu ngân ghi "giữ bàn … trong 20 phút". |
| 4b | Lời mời quá hạn + xếp lại | **Đạt** | Test A vào chờ cùng khung; Test B huỷ #38 lúc 20:57 → Test A có lời mời (thông báo "Có bàn cho bạn … Xác nhận trước 21:17"). Để quá hạn; 21:19 mở app → hộp thoại **"Lời mời đã hết hạn"** + "Xếp lại hàng chờ" / "Để sau". Bấm "Xếp lại hàng chờ" → lượt mới; vì bàn đang trống và không ai chờ nên được mời lại ngay; lượt cũ hiện "Lời mời đã hết hạn · Hết thời gian xác nhận". |
| 1 | Nhắc lịch Sẽ đến / Huỷ lịch | **Huỷ lịch: Đạt. Sẽ đến: CHƯA test** | #35 (Test A, Bàn 1) và #36 (Test B, Bàn 2) hôm nay 21:30. 21:15 cả 2 máy có "Nhắc lịch đặt bàn – Còn 15 phút nữa … Bàn 1/Bàn 2" với 2 nút "Sẽ đến", "Huỷ lịch" (`dumpsys notification`). Máy ảo bấm "Huỷ lịch" → thông báo "Đã huỷ đặt bàn. Bàn đã được nhả cho khách khác."; thu ngân: #36 "Đã hủy · Khách huỷ từ lời nhắc". Samsung: thông báo nhắc mất khi Claude mở khay bằng lệnh + vuốt (nghi do thao tác vuốt), không bấm được "Sẽ đến"; giờ nhắc đã qua nên không có lại → #35 vẫn "Chưa phản hồi". Cần test lại với một đặt bàn mới (nhắc 60/15 phút trước). |

## 3. Lỗi tìm thấy khi test (đã sửa trong code, CHƯA deploy / CHƯA cài)

| Commit | Lỗi |
|---|---|
| `f2114f8` | Gợi ý món: bàn phím không tắt sau "Gợi ý cho tôi", che kết quả (thao tác tiếp gõ nhầm vào ô ngân sách). Sơ đồ bàn: đã có nút ghép nhưng dòng dưới vẫn ghi "chưa còn bàn phù hợp". |
| `c95ff97` | Thu ngân: trạng thái hiện chữ thô "PendingDeposit"; đặt bàn ghép hiện "Bàn 16 (bàn chính), Bàn 17" + nhãn "Cần sắp xếp/ghép bàn" → nay "Chờ đặt cọc", "Bàn 16 + 17 (ghép bàn)". App bước 5 ghi "Bàn 16" → nay "Bàn 16 + 17". |

Ghi nhận (chưa sửa): gợi ý món chọn Bò né + Lẩu thái (4 người) cho 3 người không ăn chay → dư suất; tổng vẫn trong ngân sách.

## 4. Dọn dữ liệu và trả máy (21:2x)

- Đặt bàn: #35 huỷ trong app (Test A), #36 huỷ qua nhắc lịch, #37 huỷ (Test A, để thử danh sách chờ), #38 huỷ (Test B). Không còn đặt bàn
  chờ xác nhận; 2 khoản cọc #37, #38 "Đã hủy (chưa trả)" — không có tiền chuyển.
- Danh sách chờ: 3 lượt đều đóng (Test A "Khách huỷ" + "Hết giờ giữ bàn", Test B "Đã nhận bàn").
- Giỏ: Test A đã "Xóa giỏ" (11 món từ gợi ý), Test B không thêm gì.
- Giữ 2 tài khoản thử. Samsung: đăng xuất Test A, app giao diện Tối, máy bay tắt (`airplane_mode_on=0`), về màn hình chính.
  Máy ảo `bs_check`: đã tắt (Test B vẫn đăng nhập trong máy ảo).
- Trong lúc test, ứng dụng chọn chi nhánh trên tài khoản Vinh bị đổi tạm sang Bình Thạnh rồi đổi lại Quận 1 (lựa chọn lưu trên máy).

## 5. Ghi nhận thêm (chưa sửa)

- Thông báo "Có bàn cho bạn" vẫn nằm trên máy sau khi khách đã nhận bàn / lời mời đã hết hạn.
- Câu "bàn đã được mời cho khách kế tiếp" hiện cả khi không còn ai chờ (bàn chỉ được nhả).
- Sau khi bỏ lượt mới, lượt cũ hết hạn lại hiện nút "Xếp lại hàng chờ" (đúng luật, nhưng có thể gây rối).
- Nhãn "Đã hủy (chưa trả)" ở bảng cọc: "chưa trả" nghĩa là khách chưa chuyển cọc.
