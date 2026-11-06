# 06/11/2026 tối — Deploy đặt bàn đợt 2 + gợi ý món, cài Samsung, test tay

Chủ dự án chạy `SelfRestaurant-master/docs/BOOKING_V2_MIGRATION_20261106.sql` trên Supabase (phần 4: 12 cột đều `true`,
chủ dự án báo) rồi duyệt trước cả chuỗi: sửa → deploy → cài APK → đề xuất cấu hình → test tay → push.

## 1. Sửa theo góp ý chủ dự án (trước deploy)

| Commit | Nội dung |
|---|---|
| `1376e06` | Giữ bàn danh sách chờ **20 phút** khi chưa có FCM, **10 phút** khi `Fcm:Enabled=true`; `Waitlist:OfferMinutes` (1–60) ghi đè. Lời mời quá hạn: thẻ ghi "Lời mời đã hết hạn" + lý do; thông báo điện thoại (với lời mời máy đã báo); khi mở app hiện hộp thoại một lần; nút **"Xếp lại hàng chờ"** (cùng chi nhánh/khung giờ/số khách, đứng cuối hàng, bấm 2 lần không tạo 2 lượt). Endpoint mới `POST /api/mobile/v1/reservations/waitlist/{id}/rejoin`. Hộp thông báo đổi tiêu đề "Lời mời đã hết hạn". |
| `780f668` | Trợ lý: "sáng/trưa/chiều/tối/đêm" theo đúng buổi ("8 giờ sáng" = 08:00, "12 giờ đêm" = 00:00); gõ không dấu thì "toi" chỉ là "tối" khi đọc như buổi tối ("toi nay", "7h toi"), không nhầm với "tôi". Giờ ngoài giờ nhận đặt bàn: báo rõ + gợi ý giờ gần nhất (nút bấm), không tự đổi buổi. Giờ nhận đặt bàn lấy từ server (`GET /api/mobile/v1/reservations/hours`), không ghi cứng trong trợ lý. Giờ nói sau ("8 giờ") giữ buổi nói trước ("sáng mai"). |
| `4c761d1` | Gợi ý món: dòng "Tổng dự kiến, chưa gồm khuyến mãi/voucher/điểm" dưới tổng tiền. |
| `ab9ddc5` | Sửa phát hiện khi test trên Samsung (chỉ app): ô nhập của Trợ lý bị bàn phím che (edge-to-edge bỏ qua adjustResize) → đẩy lên trên bàn phím; màn "Hết bàn?" khi giờ đó vẫn còn bàn không còn ghi "Rất tiếc, đã hết bàn phù hợp". |

Test (06/11 19:2x, trước deploy): CloudHost **413/413**, contract **186** (+1 skip), app **315/315** (sau `ab9ddc5` chạy lại: 315/315),
build debug/cloudPreview/cloudProd OK, 4 web build OK, image Production `78a3a4d6cce4` (gồm test web dùng chung + bundle).
Câu trợ lý trong test: 59 câu tham số + 11 tình huống hội thoại.

## 2. Deploy

- Thư mục từ `scripts\prepare-staff-preview.ps1`, link `viet-table/viettable` (orgId `team_nCVol8ciJW2LgutrUDqgQUQU`, không phải team cũ),
  xoá `.env.local` CLI tự tạo. Code backend = `4c761d1`.
- 2 lần upload đầu báo `fetch failed` (debug: `missing_files`), không tạo deployment (`vercel ls` xác nhận). Lần 3 thành công.
- **`dpl_9d7EVP7SzGa1fsvsYZfhA8kZxx1s`** (`https://viettable-esg2psg5g-viet-table.vercel.app`, 06/11 19:27), `vercel inspect`:
  Ready, production, alias `https://viettable-beta.vercel.app`, `[syd1]`.
- Gọi 1 lần: `/healthz` 200 1,1 s; `/readyz` 200 1,1 s; `/` 200; `/app/cashier/` 200; `/app/chef/` 200; `/app/admin/` 200;
  `/api/mobile/v1/branches` 200; `/api/mobile/v1/reservations/hours` 200 `{"openFrom":"10:00","lastSlot":"21:30","slotMinutes":30}`;
  `POST /api/mobile/v1/menu/suggestions` 200 (feasible=false: "Chưa có món chính nào được gắn loại món", "52 món chưa được gắn loại");
  `/api/mobile/v1/reservations/waitlist` 401 khi ẩn danh (đúng).
- Không cần quay lui. Bản quay lui: `dpl_7Nt2UL9UKZMTDocuHRmKWGKTV3Wt` (`https://viettable-a2zuah5eo-viet-table.vercel.app`).

## 3. APK

`cloudProd` (gồm `ab9ddc5`), SHA-256 bắt đầu `A0DC24F86F512732`, `adb -s R5CRC14LFYD install -r` lên Samsung SM-A528B,
`dumpsys` lastUpdateTime **2026-11-06 19:41:46** (cài lần 1 lúc 19:30:39 bản `4c761d1`, cài lại 2 lần sau 2 sửa).

## 4. Test tay trên Samsung (adb)

Máy đang đăng nhập tài khoản của chủ dự án ("Chào Vinh"). Claude **không có mật khẩu tài khoản thử và không được tự đăng nhập /
tạo tài khoản trên Production**, nên chỉ làm phần **không ghi dữ liệu**: không tạo đặt bàn, không vào danh sách chờ → **không có
dữ liệu cần dọn**. Ảnh lưu tại `screenshots\datban_v2_samsung\` (chỉ trên máy tính, **không commit** vì có thanh thông báo và bong
bóng Zalo của chủ dự án). Khi bong bóng Zalo nhảy lên trước, công cụ tự dừng, không chụp, Claude không chạm vào Zalo.

| # | Mục | Kết quả | Ghi chú |
|---|---|---|---|
| 1 | Nhắc lịch "Sẽ đến"/"Huỷ lịch" | **Chưa test** | Cần tạo đặt bàn sắp tới bằng tài khoản thử. |
| 2 | Chỉ đường | **Đạt (phần ẩn nút)** | Chi tiết chi nhánh Bình Thạnh không có nút "Chỉ đường" vì chưa có toạ độ. Phần mở bản đồ: chờ nhập toạ độ (mục 3 file đề xuất). |
| 3 | Gợi ý giờ / chi nhánh | **Đạt** | Quận 1, 07/11 19:00, 4 khách, nút "Hết bàn?": server trả 18:00/18:30/19:30/20:00 (còn 15 bàn) + Bình Thạnh (10 bàn), Gò Vấp (12 bàn); bấm Bình Thạnh → sơ đồ bàn đúng ngày/giờ/số khách. Phát hiện + sửa tiêu đề mâu thuẫn (`ab9ddc5`), kiểm lại đạt. Chưa test lúc thật sự hết bàn (cần đặt kín 1 khung giờ). |
| 4 | Danh sách chờ | **Đạt (màn trống)** | "Danh sách chờ của tôi" mở được, câu hướng dẫn đúng. Vào chờ / có bàn / hết hạn / xếp lại: cần tài khoản thử thứ 2 để giải phóng bàn. |
| 5 | Ghép bàn | **Chưa test** | Cần cấu hình khu vực + cặp ghép (mục 2 file đề xuất). Đã đọc sơ đồ 20 bàn Quận 1. |
| 6 | Trợ lý đặt bàn | **Đạt** | "sang mai 8h 2 nguoi quan 1" → "Nhà hàng chỉ nhận đặt bàn từ 10:00 đến 21:30, nên chưa đặt được lúc 08:00. Giờ gần nhất là 10:00" + nút 10:00 → bấm → server "Còn bàn! … Thứ Bảy 07/11 · 10:00 · 2 người. Bạn xác nhận đặt bàn nhé?". Dừng, **không bấm "Đặt bàn"**. Phát hiện + sửa ô nhập bị bàn phím che (`ab9ddc5`), kiểm lại: ô nhập y=1262 (trước 2272). |
| 7 | Gợi ý món | **Đạt (chưa có thuộc tính)** | 4 người, 1 chay, 800k → "Chưa có món chính nào được gắn loại món để gợi ý." + "52 món chưa được gắn loại…". Phần món/tổng/"Tổng dự kiến, chưa gồm…"/đổi món/thêm giỏ: chờ nhập thuộc tính (mục 1 file đề xuất); dòng chữ đã có test đơn vị + ảnh máy ảo. |

Cuối buổi: app chuyển giao diện **Tối** (Cài đặt → Giao diện → Tối, "tvThemeValue" = Tối), chế độ máy bay **tắt** (`airplane_mode_on=0`),
máy ở màn hình chính.

## 5. Đề xuất cấu hình demo (chưa nhập)

`docs/DEMO_CONFIG_20261106.md`: thuộc tính 52 món (42 món có loại, 10 món "gọi thêm" để trống), khu vực + cặp ghép 20 bàn Quận 1
(giả định số liền nhau = kê sát, vì sơ đồ hiện chỉ xếp theo số chỗ), danh sách 3 chi nhánh + địa chỉ để chủ dự án tự lấy toạ độ.
Phát hiện dữ liệu: Gỏi cuốn (tôm) và Cải thìa dầu hào đang đánh dấu chay; sau khi sửa thì không còn món chính chay; Trà đá ghi
"miễn phí" nhưng giá 10.000đ. Nhập cần đăng nhập Admin (Claude không nhập mật khẩu).
