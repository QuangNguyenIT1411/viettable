# 06/11/2026 khuya — 3 sửa + deploy + test "Sẽ đến" và nhãn thu ngân

Chủ dự án duyệt trước cả chuỗi (sửa → deploy → cài Samsung → test → dọn → push).

## 1. Sửa

| Commit | Nội dung |
|---|---|
| `0481b46` | Thông báo "Có bàn cho bạn" tự xoá khi lời mời đã nhận / bỏ qua / hết hạn / đóng (lời mời hết hạn mà máy đã báo thì được thay bằng 1 thông báo "Lời mời đã hết hạn", chỉ 1 lần). Câu "bàn đã được mời cho khách kế tiếp" chỉ khi server thấy còn người khác chờ khung giờ đó; không có thì "bàn đã được nhả" (lý do lưu ở `ClosedReason`, hộp thông báo và app dùng chung). |
| `0d56446` | Gợi ý món: món chính có khẩu phần lớn hơn số người còn lại chỉ được chọn khi không còn món vừa hơn. Production 4 người / 800.000đ / 1 chay giờ ra Đậu hũ + Bò né + Phở bò + Cơm bò lúc lắc (645.000đ), không còn lẩu 4 người. |
| `f2114f8`, `c95ff97` | (từ đợt trước, nay đã deploy) bàn phím Gợi ý món, chữ sơ đồ khi có ghép bàn, nhãn thu ngân "Chờ đặt cọc" / "Bàn 16 + 17 (ghép bàn)", bước 5 app "Bàn 16 + 17". |

Test: CloudHost 417/417 (thêm test câu hết hạn có/không có người kế tiếp, 3 người không nhận lẩu 4 người, lẩu chỉ khi không có món nhỏ hơn),
contract 186 (+1 skip), app 317/317 (thêm test câu hết hạn, danh sách thông báo cần xoá), 3 bản app + 4 web build OK, image `89e4e5f1df9b`.

## 2. Deploy

Thư mục `prepare-staff-preview.ps1`, link `viet-table/viettable`, xoá `.env.local`. Vòng thử lại của Claude kiểm tra sai điều kiện nên
**deploy 3 lần cùng một code** (đều Ready `[syd1]`): `dpl_5Y4yRpbcjc6ERyLwc5TQ1UZxQ3GK` (22:38), `dpl_HC8F1B1FqNRnMdYRd5ebEGP84azz`
(22:40), **`dpl_8rkgZQBvzq275Hn12M2C8H4NqxtN`** (22:41, `https://viettable-be8r9hyr4-viet-table.vercel.app`) — alias
`viettable-beta.vercel.app` trỏ bản cuối (`vercel inspect` alias). Gọi 1 lần: `/healthz` 200 0,9 s, `/readyz` 200 1,4 s, `/` 200,
`/app/cashier/` 200, `/app/chef/` 200, `/app/admin/` 200, `/branches` 200, `/reservations/hours` 200, `/menu/suggestions` 200.
Bản quay lui: `dpl_9d7EVP7SzGa1fsvsYZfhA8kZxx1s`.

## 3. Samsung

**CHƯA cài**: lúc cài (22:4x) `adb devices` không thấy Samsung (máy đã rút cáp). APK `cloudProd` bản thường SHA-256 bắt đầu
`8C959DFABAEEC4F6` đã build sẵn. Giao diện Tối + máy bay tắt: đã đặt ở lần trước (21:2x), không kiểm lại được lần này.

## 4. Test trên máy ảo `bs_check` (Test B = "Quang2")

- **"Sẽ đến"**: sau 21:30 không còn giờ đặt trong ngày, nên đặt #41 (Quận 1, 07/11 10:00, Bàn 3) và cài **riêng trên máy ảo** bản
  `cloudProd` build với `-PVIETTABLE_REMINDER_MINUTES=663` (nhắc trước 663 phút → bật 22:57). Thông báo "Nhắc lịch đặt bàn – Còn 663 phút
  nữa tới giờ hẹn 10:00 … Bàn 3" có nút "Sẽ đến", bấm → "Đã báo nhà hàng bạn sẽ đến. Hẹn gặp bạn!"; thu ngân: #41 "Chờ xác nhận ·
  **Khách xác nhận sẽ đến**". **Đạt.** Sau đó cài lại bản `cloudProd` thường lên máy ảo.
- **Nhãn thu ngân sau deploy**: Test B đặt #42 (07/11 19:00, 10 khách, "Ghép Bàn 16 + 17 · 12 chỗ", bước 5 app ghi "Bàn 16 + 17");
  thu ngân: "**Chờ đặt cọc** · Chưa phản hồi … **Bàn 16 + 17 (ghép bàn)**"; bảng cũ #38 cũng hiện "Bàn 16 + 17 + 18 (ghép bàn)". **Đạt.**

## 5. Dọn

#41, #42 huỷ trong app (Test B). Thu ngân: không còn đặt bàn chờ xác nhận, không lượt chờ đang mở; cọc #38, #42 "Đã hủy (chưa trả)" (không
có tiền). Giữ 2 tài khoản thử. Máy ảo đã tắt (Test B vẫn đăng nhập).
