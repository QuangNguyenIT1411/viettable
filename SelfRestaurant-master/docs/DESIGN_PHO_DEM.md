# VietTable — Phong cách giao diện "Phố đêm" (app Android khách)

Tài liệu này mô tả phong cách đã được chủ dự án chọn để làm lại giao diện app khách.
Bản vẽ tham khảo trên Figma: https://www.figma.com/design/xJ1c6rsnfsTprIQnMOdPtO
(trang "Phong cách 2 · Phố đêm", 6 màn: Chào mừng, Trang chủ, Chi tiết món, Tiến độ món, VietQR, Thành công).
Figma chỉ là tham khảo hình ảnh. Tài liệu này là nguồn chính. Chỗ nào Figma khác chức năng thật của app thì
**chức năng thật thắng**: giữ đủ mọi chức năng, nút, luồng hiện có; chỉ đổi cách trình bày.

## 1. Tinh thần

Hàng quán Việt ban đêm: nền nâu than ấm, ánh đèn vàng nghệ, đỏ ớt, lá rau bạc hà, chữ màu kem.
Chữ tiêu đề to, đậm, hơi chật. Thẻ bo tròn lớn. Ảnh món đặt trong "đĩa" tròn có viền kem, tràn khỏi mép thẻ.
Giá tiền như nhãn sticker dán nghiêng. Thanh điều hướng nổi, nút Quét QR tròn vàng nhô lên ở giữa.
Không dùng gradient sặc sỡ, không emoji trang trí, không bóng đổ xám kiểu Material mặc định.

## 2. Màu (định nghĩa trong res/values/colors.xml, dùng qua theme attributes)

| Token | Hex | Dùng cho |
|---|---|---|
| pd_bg | #15100D | Nền màn hình |
| pd_surface | #211915 | Thẻ, ô nhập, bottom sheet |
| pd_surface_2 | #2E241F | Thẻ phụ, nút tròn, thanh điều hướng, nền chip không chọn |
| pd_line | #3D322B | Viền 1–1.5dp, đường kẻ |
| pd_cream | #FBF0E2 | Chữ chính, icon chính, chip đang chọn (nền) |
| pd_muted | #B9AA9A | Chữ phụ, mô tả |
| pd_dim | #7F7166 | Placeholder, icon không chọn, trạng thái vô hiệu. KHÔNG dùng cho chữ cần đọc |
| pd_yellow | #FFC53D | Màu nhấn chính: nút chính, giá, nút Quét QR, chấm tab đang chọn, "Đang nấu / Chờ" |
| pd_red | #FF5B3A | Nút phụ nổi bật (Thêm vào giỏ, CTA thứ hai), thẻ "Món của bếp", "Hủy / Từ chối / Lỗi" |
| pd_mint | #7FD3AA | Thành công: "Sẵn sàng", "Đã phục vụ", "Đã xác nhận", "Đã thanh toán", màn thành công |
| pd_plum | #3B2331 | Thẻ đặc biệt: thẻ thành viên, điểm thưởng, ưu đãi, thẻ món thứ hai xen kẽ |
| pd_on_color | #1A120C | Chữ/icon đặt trên nền vàng, đỏ, bạc hà, kem |

Quy tắc:
- Chữ trên nền vàng/đỏ/bạc hà/kem luôn dùng pd_on_color (không dùng trắng).
- Chữ thường trên nền tối dùng pd_cream (chính) hoặc pd_muted (phụ). Kiểm tra tương phản ≥ 4.5:1.
- Trạng thái không chỉ phân biệt bằng màu: luôn có chữ (và icon) đi kèm.
- Thanh trạng thái và thanh điều hướng hệ thống: nền pd_bg, icon sáng.
- Quầng sáng trang trí: hình tròn mờ (blur lớn) màu đỏ/vàng/bạc hà, độ mờ 15–30%, đặt sau tiêu đề ở góc trên. Tối đa 1–2 quầng mỗi màn.

## 3. Chữ

Hai font Google Fonts, giấy phép OFL, đều hỗ trợ đầy đủ tiếng Việt. Đóng gói trong res/font (kèm file giấy phép),
không tải qua mạng. Kiểm tra hiển thị dấu: "Đặt bàn thành công · Phở bò tái · Chè ba màu · Ưu đãi".

- **Unbounded** (Bold, SemiBold, Medium): tiêu đề, giá tiền, nhãn chữ in hoa.
- **Bricolage Grotesque** (Regular, Medium, SemiBold, Bold): nội dung, nút, ô nhập.

| Style | Font | Cỡ / dòng (sp) | Ghi chú |
|---|---|---|---|
| Display.XL | Unbounded Bold | 34 / 40 | Tiêu đề màn lớn ("Tối nay ăn gì đây?"), letterSpacing -0.02 |
| Display.L | Unbounded SemiBold | 24 / 30 | Tên món ở chi tiết, số tiền lớn |
| Display.M | Unbounded SemiBold | 17 / 23 | Tiêu đề mục ("Gọi nhiều nhất") |
| Price | Unbounded Bold | 18 / 22 | Giá tiền |
| Label | Unbounded Medium | 10 / 14 | CHỮ IN HOA, letterSpacing +0.08 ("ĐỘ CAY", "BÀN B4 · ĐƠN #0412") |
| Body | Bricolage Regular | 15 / 22 | Nội dung |
| Body.Strong | Bricolage SemiBold | 15 / 22 | Tên món trong thẻ, dòng quan trọng |
| Caption | Bricolage Medium | 13 / 18 | Thời gian, phụ đề |
| Button | Bricolage Bold | 16 / 22 | Chữ trên nút |

Cho phép người dùng phóng to chữ (sp). Tiêu đề dài phải xuống dòng, không bị cắt; chữ dọc từng ký tự là lỗi.

## 4. Hình khối, khoảng cách, bóng

- Lưới 4dp. Lề màn hình 24dp. Khoảng giữa các mục 24–32dp, giữa các phần tử trong thẻ 8–16dp.
- Bo góc: thẻ lớn 28–32dp; thẻ nhỏ/hàng danh sách 20–22dp; ô nhập 18–27dp (dạng viên thuốc); nút 30dp (viên thuốc); chip và nhãn trạng thái 999dp; nút tròn 44dp.
- Bóng: chỉ dùng bóng tối ấm, rõ, ngắn (đen 35–50%, lệch xuống 6–16dp, blur 12–32dp) cho đĩa món, nhãn giá, thanh điều hướng nổi. Thẻ thường không bóng, phân lớp bằng màu nền (bg → surface → surface_2).
- Viền: 1–1.5dp pd_line cho ô nhập, nút ghost, chip không chọn, thanh điều hướng.
- Vùng chạm tối thiểu 48dp.

## 5. Thành phần

**Nút chính (viên thuốc có mũi tên)**: cao 60dp, bo 30dp, chữ Button căn trái (padding 26dp), bên phải một vòng tròn 44dp
nền pd_on_color chứa icon mũi tên màu của nút. Biến thể: Vàng (mặc định), Đỏ, Ghost (nền trong suốt, viền pd_line, chữ kem,
vòng tròn nền surface_2). Nút nhỏ trong hàng ngang có thể ẩn vòng mũi tên và căn giữa chữ.
Trạng thái nhấn: thu nhỏ 0.97 trong 100ms. Vô hiệu: độ mờ 40%.

**Nhãn giá sticker**: nền vàng, chữ Price màu on_color, bo 12dp, padding 14×8dp, xoay -6° đến +10°, bóng ngắn.
Dùng trên ảnh món, thẻ món nổi bật, chi tiết món.

**Đĩa món (ảnh món)**: ảnh món thật của app cắt tròn, viền kem #FBF0E2 dày ~10% đường kính, bóng dưới.
Trong thẻ lưới: đĩa nằm trên, tràn ra khỏi mép trên thẻ ~20dp. Ảnh lỗi/chưa có: nền surface_2 + icon bát đĩa kem.

**Thẻ món (lưới 2 cột)**: nền surface (xen kẽ plum), bo 28dp, đĩa tràn mép trên, tên món Body.Strong kem, giá Price vàng,
nút "+" tròn 36dp nền vàng icon on_color ở góc dưới phải. Món hết hàng: đĩa xám, nhãn "Hết món" và nút + vô hiệu.

**Thẻ nổi bật "Món của bếp"**: nền đỏ, sọc chéo mờ (on_color 7%), chữ on_color, đĩa lớn tràn góc phải, nhãn giá, nút viên thuốc tối "Gọi ngay +".

**Chip lọc**: viên thuốc, padding 18×10dp. Đang chọn: nền kem, chữ on_color. Không chọn: viền pd_line, chữ muted.

**Nhãn trạng thái (status pill)**: viên thuốc, icon 14dp + chữ Caption.
- Bạc hà: Sẵn sàng, Đã phục vụ, Đã xác nhận, Đã thanh toán, Đã check-in
- Vàng: Đang nấu, Chờ xác nhận, Chờ thanh toán
- Đỏ: Đã hủy, Bị từ chối, Thanh toán thất bại, Vắng mặt
- surface_2 + chữ muted: Đã gửi bếp, Nháp

**Ô nhập / tìm kiếm**: nền surface, viền pd_line 1dp, bo 27dp, cao 54dp, icon dim bên trái, placeholder dim, chữ kem.
Lỗi: viền đỏ + dòng Caption đỏ dưới ô. Đang focus: viền vàng.

**Thanh điều hướng nổi**: viên thuốc cao 64dp, cách đáy 16dp, lề 24dp, nền surface_2, viền pd_line, bóng lớn.
4 tab icon (Trang chủ, Đặt bàn, Đơn hàng, Tôi) + ở giữa nút Quét QR tròn 64dp nền vàng, icon on_color, nhô lên 12dp,
có vòng nền pd_bg 5dp tách khỏi thanh. Tab đang chọn: icon kem + chấm vàng 6dp dưới icon. Không chọn: icon dim.
Giữ đúng các đích điều hướng hiện có (nếu app có 5 tab hiện tại, giữ đủ chức năng, Quét QR là nút giữa).
Nội dung cuộn phải có padding đáy đủ để không bị thanh che.

**Bottom sheet / màn chi tiết**: nền pd_bg, bo trên 36dp, tay nắm 44×5dp màu line.

**Bộ đếm số lượng**: viên thuốc nền surface_2, hai nút tròn 44dp (− nền surface chữ kem, + nền kem icon on_color), số giữa dạng Price.

**Hộp thoại**: nền surface, bo 28dp, tiêu đề Display.M, nút Vàng / Ghost. Hành động nguy hiểm (Hủy đặt bàn, Hủy giao dịch) dùng nút Đỏ.

**Snackbar/Toast**: nền surface_2, chữ kem, viền trái không dùng; câu ngắn tiếng Việt, hiện đủ (không bị cắt "…").

## 6. Áp dụng theo màn hình (giữ nguyên chức năng)

- **Chào mừng / Đăng nhập / Đăng ký / Quên mật khẩu**: chữ VIET TABLE Display lớn, quầng sáng vàng, đĩa món làm điểm nhấn;
  ô nhập kiểu mục 5; nút chính Vàng, nút phụ Ghost; nút Google dạng Ghost có logo.
- **Màn khóa vân tay**: "Xin chào, q***@…" Display.M, icon vân tay lớn trong vòng tròn vàng (nhịp thở nhẹ), "Dùng mật khẩu", "Đăng xuất" dạng chữ.
- **Trang chủ**: hàng trên: chip chi nhánh/bàn hiện tại (chấm bạc hà nếu đang ở bàn) + nút chuông tròn có số chưa đọc (chấm đỏ có số);
  lời chào muted + tiêu đề Display.XL; ô tìm kiếm; thẻ "Đặt bàn sắp tới" (nền surface, viền vàng mảnh, nhãn trạng thái);
  lối tắt (Đặt bàn, Quét QR, Thực đơn, Đơn hiện tại) dạng ô vuông bo 22dp; chip danh mục; thẻ "Món của bếp"; lưới "Gợi ý / Bán chạy".
- **Thực đơn / danh mục / tìm kiếm**: chip lọc trên, lưới thẻ món 2 cột.
- **Chi tiết món**: nửa trên nền vàng có vòng tròn đồng tâm mờ, đĩa món lớn ở giữa, nhãn giá lớn; nút back và tim tròn nền on_color;
  bottom sheet: tên món Display.L, hàng thông tin (sao, thời gian, độ cay), mô tả, lựa chọn (chip), ghi chú cho bếp, điều chỉnh nguyên liệu (giữ chức năng hiện có),
  đáy: bộ đếm + nút Đỏ "Thêm · <tổng tiền>".
- **Giỏ hàng**: hàng món (đĩa nhỏ 44dp, tên, ghi chú, bộ đếm), tổng tiền Price vàng, nút Vàng "Gửi bếp".
- **Tiến độ món / Đơn hiện tại**: nhãn "BÀN B4 · ĐƠN #…" Label; tiêu đề Display.XL; thẻ tiến độ "3/5 món" (số Display vàng, thanh tiến độ đỏ→vàng, nút tròn ở đầu thanh);
  danh sách món với nhãn trạng thái (mục 5); nút Ghost "Giục bếp" + nút Vàng "Thanh toán". Yêu cầu phục vụ dạng thẻ surface.
- **Chọn phương thức thanh toán**: 2 thẻ lớn (Tiền mặt / Chuyển khoản VietQR), thẻ chọn có viền vàng 2dp.
- **Màn VietQR / chờ**: số tiền Display.XL; "vé" nền kem bo 32dp chứa QR trắng, logo ngân hàng, STK, chủ TK, nội dung CK (có nút sao chép tròn on_color),
  đường răng cưa giữa vé; khung "Đang chờ…" với chấm bạc hà tỏa sóng; dòng "Hệ thống sẽ tự xác nhận khi nhận được tiền" (khi SePay bật); nút Ghost "Lưu ảnh mã QR".
  Trạng thái hủy/thất bại hiện thẻ đỏ có lời giải thích tiếng Việt.
- **Thanh toán thành công**: quầng bạc hà, huy hiệu tích tròn bạc hà có vòng gạch xoay chậm, giấy màu rơi, tiêu đề "Ngon miệng nhé, <tên>!",
  thẻ biên nhận (mã HĐ, thời gian, tổng Price vàng), thẻ plum "+X điểm…", nút Vàng "Về trang chủ".
- **Hóa đơn / Lịch sử hóa đơn / Chia hóa đơn**: thẻ surface, các dòng tiền căn phải, tổng Price vàng; chia hóa đơn dùng chip chọn người/món.
- **Đặt bàn (5 bước)**: thanh tiến độ "1/5…5/5" dạng các đoạn viên thuốc (đã xong: vàng; hiện tại: kem; chưa: surface_2).
  Ngày: chip ngày ngang (hôm nay ghi "Hôm nay"). Giờ: lưới chip 3 cột, giờ đã qua mờ. Số khách: bộ đếm lớn.
  Chọn bàn (sơ đồ, mục 5 của đặt bàn): lưới ô bàn bo 18dp, nhóm theo số ghế, chú thích màu:
  Trống = surface + chấm bạc hà; Đang chọn = nền vàng chữ on_color; Đã được đặt = surface_2 chữ dim + icon khóa; Không đủ ghế = viền gạch đứt dim; Tạm ngưng = mờ 40%.
  Ghi chú & liên hệ: chip dịp đặc biệt + ô nhập. Màn thành công: huy hiệu, thẻ mã đặt bàn, nhãn trạng thái "Chờ xác nhận".
- **Lịch sử / chi tiết đặt bàn**: tab (Sắp tới / Đã xong / Đã hủy …) dạng chip; thẻ đặt bàn có nhãn trạng thái + lý do từ chối (đỏ nhạt);
  nút chỉ hiện khi được phép (đúng logic hiện tại). Đặt cọc (khi có): thẻ vàng nhạt.
- **Thông báo**: tab chip (Tất cả / Đặt bàn / Đơn hàng / Thanh toán / Ưu đãi), hàng thông báo nền surface, icon tròn theo loại
  (đặt bàn vàng, đơn đỏ, thanh toán bạc hà, ưu đãi plum), chấm vàng = chưa đọc, nút chữ "Đã đọc tất cả".
- **Tôi / Hồ sơ / Thẻ thành viên / Cài đặt**: thẻ thành viên nền plum có hạng, điểm, thanh tiến độ lên hạng vàng;
  danh sách cài đặt dạng hàng surface có icon; công tắc: bật = vàng, tắt = surface_2.
- **Trạng thái rỗng / lỗi / đang tải**: icon line lớn màu dim, câu tiếng Việt Body muted, nút Ghost "Thử lại". Đang tải: khối skeleton surface_2 nhấp nháy nhẹ.

## 7. Chuyển động

Mục tiêu: sống động nhưng nhanh, không chặn thao tác. Thời lượng 150–400ms, vào màn ≤ 600ms tổng. Easing: vào = decelerate/overshoot nhẹ, ra = accelerate.

- Vào màn: các khối chính trượt lên 24dp + hiện dần, lệch nhau 40–60ms.
- Lưới thẻ món: trượt lên lần lượt (stagger 50ms, tối đa 8 thẻ đầu).
- Đĩa món ở chi tiết: xoay -30°→0° + phóng 0.8→1 khi vào (400ms, overshoot nhẹ). Nhãn giá: bật 0→1 (overshoot).
- Bottom sheet chi tiết: trượt từ dưới lên 300ms.
- Thanh điều hướng: trượt lên khi vào trang chủ; đổi tab: chấm vàng trượt sang tab mới.
- Nút "+" / Thêm vào giỏ: nảy nhẹ 1→1.15→1 và rung nhẹ (haptic) khi thêm.
- Tiến độ món: thanh tiến độ chạy tới giá trị mới (600ms); icon lửa ở món "Đang nấu" phập phồng chậm (1.2s, lặp).
- VietQR: vạch quét chạy lên xuống trên QR (1.8s, lặp); chấm "Đang chờ" tỏa sóng (1.2s, lặp).
- Thành công: tích bật (overshoot), vòng gạch xoay chậm, giấy màu rơi 1 lần (≤ 2s), haptic thành công.
- Thông báo mới (realtime): hàng mới trượt vào từ trên.
- Tôn trọng cài đặt "Xóa hiệu ứng động" của Android (ANIMATOR_DURATION_SCALE = 0): tắt toàn bộ animation lặp và hiệu ứng vào màn.
- Không dùng thư viện animation nặng (Lottie) nếu không cần; dùng ViewPropertyAnimator / MotionLayout / AnimatedVectorDrawable.

## 8. Cách làm (yêu cầu kỹ thuật)

- Chỉ đổi giao diện: theme, styles, colors, fonts, drawables, layout XML, animation. KHÔNG đổi logic, API, luồng, tên ID mà test đang dùng.
  Nếu bắt buộc đổi ID thì cập nhật test tương ứng.
- Theme gốc: Material 3 tối (Theme.Material3.Dark.NoActionBar) ghi đè bằng token mục 2; app luôn dùng Phố đêm (không theo chế độ sáng của hệ thống).
- Tạo bộ style dùng chung (TextAppearance.*, Widget.VietTable.Button.Primary/Red/Ghost, Chip, StatusPill, Card…) rồi áp cho từng màn, tránh hard-code màu trong layout.
- Làm theo từng nhóm màn, mỗi nhóm một commit, chụp ảnh trước/sau trên Samsung để chủ dự án duyệt:
  1) Theme + style dùng chung + thanh điều hướng; 2) Trang chủ, thực đơn, chi tiết món, giỏ; 3) Đơn hiện tại, thanh toán, VietQR, thành công, hóa đơn;
  4) Đặt bàn (5 bước, sơ đồ bàn, lịch sử, chi tiết); 5) Thông báo, Tôi, hồ sơ, thẻ thành viên, cài đặt; 6) Đăng nhập/đăng ký/khóa vân tay, trạng thái rỗng/lỗi.
- Sau mỗi nhóm: build cả 3 bản, chạy unit test, kiểm tra không có chữ bị cắt/chữ dọc/che bởi thanh điều hướng, kiểm tra tiếng Việt có dấu.
- Không đụng web chef/cashier/admin.
