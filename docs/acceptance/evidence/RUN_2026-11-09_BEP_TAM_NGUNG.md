# Thử bếp tạm ngưng / mở bán lại món trên Production — 09/11/2026

Chủ dự án đăng nhập web bếp Quận 1 (`chef_hung`) trong trình duyệt của app, bảo thử trên máy ảo. Production `dpl_Cdm1AFyRBxgD4BRdNrPEnWUNdLuv`
(sửa `6dde112`). App khách: máy ảo `bs_check`, APK `cloudProd` `13F46936877A27BC`, chế độ khách, Chi nhánh Quận 1.

Món thử: **Đậu hũ sốt cà chua** (55.000đ) — món thêm sau, đúng trường hợp từng báo "Món không thuộc thực đơn chi nhánh hôm nay".

| Bước | Web bếp | App khách (mở lại Thực đơn) |
|---|---|---|
| Trước | "Đang bán", tóm tắt "54 món · 54 đang bán" | Có món, nút "+" bấm được (`chef_0_truoc.png`) |
| Bấm **Tạm ngưng** | Không báo lỗi; món "Tạm ngưng", nút "Tiếp tục"; "54 món · 53 đang bán" | Nhãn đỏ **"Hết món"**, nút "+" xám, `enabled=false` (`chef_1_tam_ngung.png`) |
| Bấm **Tiếp tục** | Món "Đang bán"; "54 món · 54 đang bán" | Nhãn "Hết món" mất, nút "+" `enabled=true` (`chef_2_mo_ban_lai.png`) |

Ảnh: `screenshots\fix_20261108\`. Món đã mở bán lại (dữ liệu như trước khi thử).

Ghi nhận phụ (chưa sửa): ô tìm chi nhánh trong app phân biệt dấu ("Quan 1" không ra "Chi nhánh Quận 1").
Web bếp không hiện câu báo "Đã ẩn món…" sau khi bấm (trạng thái thẻ đổi đúng).
