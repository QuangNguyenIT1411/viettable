# Rà soát chức năng còn thiếu / dở dang — 15/10/2026

> Chỉ rà, **chưa sửa**. Hạn chót thêm tính năng: 01/11/2026 (sau đó chỉ sửa lỗi). Demo cho thầy: 01–10/11/2026.
> Code rà: branch `qa/vt-ho-01` tại `44dbb2a` (Production đang chạy `fcd83a9`).

## Cách rà

- Tìm trong toàn bộ code Android (`app/src/main`), 3 web staff, gateway và 5 service:
  - chữ "đang phát triển" / "chưa hỗ trợ" / "chưa được tích hợp" / "coming soon";
  - TODO / FIXME / HACK;
  - endpoint trả 501 / `NotImplementedException`;
  - `href="#"`, click listener rỗng, nút luôn bị khóa.
- Đọc từng màn hình nghi ngờ:
  - Android: Cài đặt, Thanh toán online, Chia hóa đơn, Chi tiết món trong đơn, Tìm kiếm, Bản đồ chi nhánh, Hồ sơ, Trang chủ, Hỗ trợ;
  - menu và route của web admin;
  - nguồn dữ liệu của báo cáo admin.
- So với `SelfRestaurant-master/docs/B0_STATUS.md` … `B7_STATUS.md`, `MOBILE_B0_TRACEABILITY.md`, `docs/acceptance/FULL_ACCEPTANCE_MATRIX.csv` và `docs/acceptance/REMAINING_ISSUES.md`.

**Kết quả chung:**

- Không có TODO/FIXME nào trong code chạy thật.
- Không có endpoint 501.
- Tài liệu B0–B7 ghi đủ API cho cả 8 giai đoạn.
- Chỗ thiếu chủ yếu là:
  - (a) giao diện ghi chữ "chưa tích hợp" hoặc nút bấm không có tác dụng;
  - (b) tính năng khách gửi đi nhưng phía nhân viên không thấy;
  - (c) web admin thiếu trang dù backend đã có, hoặc có thể dễ dàng có, API.

## Bảng tổng hợp (P1 trước)

Mức độ: **P1** = thầy dễ thấy khi demo · **P2** = nên có · **P3** = để sau.
Công: nhỏ (≤ ½ ngày) · vừa (1–2 ngày) · lớn (> 2 ngày).

| # | Chức năng | Ở đâu | Hiện trạng | Mức độ | Công | Rủi ro làm hỏng chức năng đang chạy |
|---|---|---|---|---|---|---|
| 1 | Nút "Hủy món" / "HỦY" ở cột **Sẵn sàng** (web bếp) | `selfrestaurant-chef-web/src/pages/DashboardPage.tsx` | **Đã sửa ở `be88d4e`, chưa deploy.** Backend cho hủy món/đơn đã READY nên giữ nút, nhưng hộp xác nhận ghi rõ "đã nấu xong" + nút "Xác nhận hủy món" / "Không hủy". Playwright đã có test | P1 | nhỏ (chỉ còn deploy) | Thấp |
| 2 | "Giục bếp làm nhanh" và "Báo hủy món" từ app | Android `ui/order/OrderItemDetailFragment.java`; web thu ngân (khung "Yêu cầu phục vụ"); web bếp | App báo "Đã gửi yêu cầu ưu tiên món **tới Bếp trưởng**!" / "**tới bếp**", nhưng thực tế chỉ tạo một yêu cầu phục vụ loại `OTHER`. Yêu cầu này chỉ hiện ở khung **thu ngân**, **bếp không thấy**. "Báo hủy món" không hủy món; nhân viên phải tự hủy | P1 | nhỏ (sửa câu thông báo cho đúng: "đã gửi tới nhân viên") · vừa (hiện các yêu cầu này trên bảng bếp qua realtime) | Thấp (sửa câu) · Vừa (thêm vào bảng bếp) |
| 3 | Thanh toán online: Ví MoMo, Ví ZaloPay, Thẻ Quốc tế/ATM | Android `res/layout/fragment_online_payment.xml` | 3 lựa chọn hiện ra với dòng chữ **"Chưa được tích hợp"**; chỉ có VNPAY Sandbox | P1 | nhỏ (ẩn 3 lựa chọn) · lớn (tích hợp thật) | Thấp (ẩn) |
| 4 | Thanh toán VNPAY Sandbox trên Production | Biến môi trường Vercel `viettable` + IPN công khai | `B6_STATUS.md`: thanh toán thật cần `TmnCode`, `HashSecret` sandbox và URL IPN HTTPS công khai. **Chưa xác minh** Production mới đã cấu hình chưa (mình không được đọc biến môi trường). Nếu thầy bấm "Thanh toán ngay" mà chưa cấu hình thì sẽ lỗi | P1 | nhỏ (chủ dự án kiểm tra cấu hình + thử 1 giao dịch) · vừa (nếu phải cấu hình) | Thấp |
| 5 | Báo cáo doanh thu của Admin khác doanh thu thu ngân | `OrdersController.cs` `GET api/admin/reports/revenue`; web admin "Báo cáo" | Admin tính doanh thu = tổng tiền **món đã gọi** (không tính món hủy), **kể cả đơn chưa thanh toán**, trước giảm giá/voucher/điểm. Thu ngân tính **tiền thực thu** (Bills + Invoices). Hai con số cùng ngày sẽ lệch nhau | P1 | vừa | Vừa (đổi câu truy vấn báo cáo; phải đối chiếu số liệu) |
| 6 | Chọn ngôn ngữ "English" | Android `ui/profile/SettingsFragment.java` | Chọn English chỉ lưu tùy chọn và hiện "Đã chọn ngôn ngữ"; app **không có bản tiếng Anh** (không có `values-en`), giao diện không đổi | P1 | nhỏ (ẩn mục hoặc ghi "Chỉ hỗ trợ tiếng Việt") · lớn (dịch toàn app) | Thấp (ẩn) |
| 7 | Mật khẩu yếu dùng chung cho tài khoản staff trên web công khai (G14) | `admin`, `chef_hung`, `cashier_lan` trên Production | Đã ghi ở `REMAINING_ISSUES.md` G14. Web staff mở công khai; nếu thầy/lớp biết mật khẩu demo thì vào được Admin | P1 | nhỏ (chủ dự án đổi mật khẩu trước demo) | Thấp |
| 8 | Đăng nhập Google trên Samsung | Android `ui/auth/*Google*`; `gradle.properties` đã có `VIETTABLE_GOOGLE_WEB_CLIENT_ID` | `B1_STATUS.md`: đăng nhập Google thật **chưa được thử trên máy thật** | P1 | nhỏ (thử trên Samsung) · vừa (nếu phải sửa cấu hình Google Console) | Thấp |
| 9 | Công tắc **Sinh trắc học**, **Thông báo SMS**, **Thông báo đẩy** | Android `SettingsFragment.java` | Chỉ lưu tùy chọn lên server. Không có đăng nhập vân tay (`BiometricPrompt`), không gửi SMS, không có Firebase push. Bật công tắc **không có tác dụng nhìn thấy được** | P2 | nhỏ (ẩn hoặc ghi chú "lưu tùy chọn") · lớn (làm thật) | Thấp (ẩn) |
| 10 | Khách gửi **yêu cầu hỗ trợ** (B7) — nhân viên không xem được | Android `ui/profile/SupportFragment.java`; Customers API chỉ có tạo (`POST customers/{id}/support/requests`) | Khách gửi được và app báo thành công, nhưng **không có màn hình nào** cho admin/nhân viên đọc | P2 | vừa (API danh sách + trang admin) | Thấp (thêm mới, không sửa luồng cũ) |
| 11 | **Đánh giá** của khách — admin không xem được | Customers API `reviews` (theo từng khách); web admin không có trang | Khách tạo/sửa/xóa đánh giá được; admin không có trang xem hay ẩn đánh giá | P2 | vừa | Thấp |
| 12 | **Đặt bàn hôm nay** trên web admin | Gateway `GET /api/admin/reservations/today` (đã có) | API có nhưng **web admin chưa có trang** (chỉ thu ngân có danh sách đặt bàn) | P2 | nhỏ–vừa | Thấp |
| 13 | Chia hóa đơn "Theo món" | Android `res/layout/fragment_split_bill.xml` dòng 97 | Lựa chọn hiện chữ **"Theo món (chưa hỗ trợ)"**; chỉ chia theo số tiền | P2 | nhỏ (ẩn) · lớn (làm thật: Billing + UI) | Thấp (ẩn) · Vừa (làm thật) |
| 14 | Nút "Sắp xếp theo đánh giá cao" trong kết quả tìm kiếm | Android `ui/home/SearchResultFragment.java`; `MobileRepository.java` dòng ~1214 | Điểm đánh giá và số đã bán của món **luôn = 0** (viết cứng khi đọc API) nên nút sắp xếp **không thay đổi gì** (vẫn hiện thông báo "Đã sắp xếp…") | P2 | nhỏ (ẩn nút) · vừa (tính điểm món từ dữ liệu thật) | Thấp |
| 15 | Quản lý **voucher** | Billing (voucher có sẵn trong DB); web admin không có trang | Không tạo/sửa/tắt voucher được từ giao diện; chỉ có dữ liệu sẵn | P2 | lớn (API CRUD Billing + trang admin) | Vừa (đụng Billing) |
| 16 | Điều khoản dịch vụ nói về **tiền cọc** | Android `SettingsFragment.java` (mục Điều khoản) | Câu "Tiền cọc sẽ được hoàn lại…" mâu thuẫn với chính sách hiện tại `NOT_REQUIRED` / 0 đ (`B3_STATUS.md`) | P3 | nhỏ | Thấp |
| 17 | Quản lý **chi nhánh** | Catalog chỉ có `GET api/branches`; web admin không có trang | CLAUDE.md ghi Admin "Quản lý chi nhánh" nhưng không có API tạo/sửa chi nhánh lẫn giao diện (2 chi nhánh có sẵn từ dữ liệu mẫu) | P3 | lớn | Vừa (Catalog) |
| 18 | Khu vực bàn khi đặt bàn | Android `ui/reservation/ResAreaFragment.java` | Dữ liệu chưa có phân loại khu vực, chỉ có `GENERAL` (`B3_STATUS.md`) | P3 | vừa (thêm dữ liệu + chọn khu vực) | Vừa (đặt bàn) |
| 19 | Bản đồ chi nhánh / chi nhánh gần đây | Android `ui/branch/BranchMapFragment.java` | Màn hình **không có lối vào** (không màn nào mở tới), nếu mở thì ghi "Bản đồ chi nhánh chưa khả dụng". Chưa có tọa độ chi nhánh | P3 | nhỏ (xóa code thừa) · lớn (bản đồ thật) | Thấp |
| 20 | Khuyến mãi trên trang chủ | Android `ui/home/HomeFragment.java` dòng 136–137 | Banner khuyến mãi luôn ẩn vì chưa có nguồn dữ liệu | P3 | vừa | Thấp |
| 21 | Quản lý câu hỏi thường gặp (FAQ) | Customers `GET support/faqs` | Chỉ đọc; admin không sửa được | P3 | vừa | Thấp |
| 22 | Tài khoản ngân hàng VietQR của thu ngân viết cứng | `selfrestaurant-cashier-web/src/lib/vietQr.ts` | Ngân hàng + số tài khoản nằm trong code web, không cấu hình được theo chi nhánh | P3 | nhỏ | Thấp |
| 23 | Tên mẫu "Nguyễn Văn A" | Android `fragment_profile.xml`, `fragment_edit_profile.xml` | Chữ mẫu trong layout; chỉ lộ ra nếu chưa có thông tin người dùng | P3 | nhỏ | Thấp |
| 24 | Link mạng xã hội ở chân trang web khách | `selfrestaurant-customer-web/src/components/SiteFooter.tsx` | `href="#"`; web khách **không** chạy trên Production | P3 | nhỏ | Thấp |

## Tồn đọng kiểm thử / hạ tầng đã có sẵn (không phải tính năng mới)

Các mục này đã có trong `docs/acceptance/REMAINING_ISSUES.md`. Mình không lặp lại chi tiết ở đây.

| Nhóm | Mục |
|---|---|
| Kiểm thử | G1 (APK CloudPreview), G2 (cô lập sự kiện realtime giữa 2 khách), G3 (recovery), G5/G6 (lịch sử trên Samsung, chạy lại B3–B7), G8, G9, G11 |
| Hiệu năng | G4, G7, G15 |
| Giao diện | G16 (hủy bàn cho phiên từ đặt bàn), G17 (thanh bên Admin trên điện thoại) |

## Gợi ý chọn cho đợt trước 01/11

**Nhóm việc nhỏ, rủi ro thấp, thầy dễ thấy:**

- #1: deploy `be88d4e`.
- #2: sửa câu thông báo cho đúng.
- #3: ẩn MoMo/ZaloPay/Thẻ.
- #6: ẩn English.
- #9: ẩn hoặc ghi chú các công tắc.
- #13: ẩn "Theo món".
- #14: ẩn nút sắp xếp theo đánh giá.
- #16: sửa câu điều khoản.

**Việc chủ dự án tự kiểm tra:**

- #4: cấu hình VNPAY trên Production.
- #7: đổi mật khẩu staff trước demo.
- #8: thử đăng nhập Google trên Samsung.

**Nếu còn thời gian (công vừa):**

- #5: thống nhất doanh thu Admin với thu ngân.
- #10, #11, #12: trang admin xem yêu cầu hỗ trợ, đánh giá, đặt bàn hôm nay.

## Cập nhật 15/10/2026 — Đợt 1 (chủ dự án chọn; đã làm, **chưa deploy**)

| # | Trạng thái |
|---|---|
| 1 | Xong ở `be88d4e` (chờ deploy) |
| 2 | **Bản đầy đủ:** app gửi yêu cầu loại `KITCHEN_RUSH` ("Giục bếp") / `KITCHEN_CANCEL` ("Xin hủy món"), ghi chú = tên món; câu trên app: "Đã gửi yêu cầu tới bếp". Bảng bếp nhận qua realtime: thông báo nổi góc phải + dấu trên thẻ đơn cùng bàn; nút "Đã xem" đóng yêu cầu (không hủy món). Khung thu ngân hiện nhãn "Khách giục bếp" / "Khách xin hủy món". Danh sách yêu cầu cho nhân viên không còn hiện yêu cầu của phiên đã kết thúc. Không đổi cấu trúc database |
| 3, 4 | Ẩn MoMo/ZaloPay/Thẻ. Máy chủ có API mới `GET /api/mobile/v1/payment-options` (từ cấu hình VNPAY của Billing); app chỉ hiện "Thanh toán Online" khi danh sách không rỗng. Production hiện không có biến VNPAY nên chỉ còn thanh toán tại quầy |
| 6 | Ẩn mục chọn ngôn ngữ |
| 8 | Xong: chủ dự án đã thử đăng nhập Google trên Samsung ngày 13/10/2026, đạt |
| 9 | Ẩn cả nhóm "Thông báo & Bảo mật" (công tắc thông báo đẩy, SMS, vân tay) |
| 13 | Ẩn nút "Theo món (chưa hỗ trợ)" |
| 14 | Ẩn nút "Sắp xếp theo đánh giá cao" |
| 16 | Câu điều khoản: bỏ phần tiền cọc, ghi "Đặt bàn hiện không cần đặt cọc" |
| Thêm | Màn trạng thái yêu cầu trên app hiện nhãn tiếng Việt ("Gọi nhân viên", "Giục bếp"…) thay vì mã như `CALL_STAFF` |

Đợt 2 (chờ chủ dự án duyệt đợt 1): #5 thống nhất doanh thu Admin theo tiền thực thu. #10–#12 để chủ dự án quyết sau đợt 2.

## Cập nhật 15/10/2026 — Thanh toán chỉ còn Tiền mặt + Chuyển khoản, và Đợt 2 (**chưa deploy**)

**Thanh toán (chủ dự án yêu cầu):**
- App và web thu ngân chỉ hiện 2 cách: **Tiền mặt** và **Chuyển khoản** (VietQR). VNPAY/MoMo/ZaloPay/Thẻ/POS bị ẩn, code giữ nguyên.
- Mã VietQR do Billing tạo theo chuẩn EMVCo/Napas (chuỗi + CRC16) và vẽ ảnh PNG ngay trong máy chủ (thư viện QRCoder), **không gọi dịch vụ ngoài**. Đã kiểm bằng bộ đọc QR độc lập (zxing-cpp): giải mã ra đúng chuỗi.
- Nội dung chuyển khoản: thu ngân thanh toán đơn → `VT B<chi nhánh> DON<mã đơn>` (vd `VT B1 DON46`); khách trả qua app → `VT B<chi nhánh> TT<mã giao dịch>`.
- App: chọn Chuyển khoản → hiện mã QR, ngân hàng, chủ TK, số TK, số tiền, nội dung, nút "Lưu ảnh QR"; chờ thu ngân xác nhận.
- Web thu ngân: nút Tiền mặt / Chuyển khoản; Chuyển khoản hiện QR trên màn hình quầy, nút "Đã nhận chuyển khoản" (bấm tay sau khi kiểm tra app ngân hàng). Khung giao dịch từ app có nút "Hiện mã QR".
- Hóa đơn, lịch sử, báo cáo ghi "Tiền mặt" / "Chuyển khoản"; báo cáo ngày tách tổng theo 2 phương thức (và "Khác" cho dữ liệu cũ nếu có).
- Tài khoản nhận nằm trong biến môi trường, không ghi cứng (đã xóa số tài khoản và dịch vụ `img.vietqr.io` khỏi code web thu ngân):

| Biến (Vercel → project `viettable` → Settings → Environment Variables, môi trường Production) | Giá trị |
|---|---|
| `Payments__BankTransfer__BankBin` | `970418` (mã BIN Napas của BIDV) |
| `Payments__BankTransfer__BankName` | `BIDV` |
| `Payments__BankTransfer__AccountNumber` | `8830150124` |
| `Payments__BankTransfer__AccountName` | tên chủ tài khoản, IN HOA KHÔNG DẤU (đúng như ngân hàng hiển thị) |

Thiếu bất kỳ biến nào → app và web thu ngân chỉ cho thanh toán **Tiền mặt** (không lỗi). Thêm/sửa biến xong phải **deploy lại** thì máy chủ mới đọc giá trị mới.

**Đợt 2 — #5 xong:** doanh thu Admin (báo cáo theo ngày và "Doanh thu hôm nay" ở Tổng quan) = tiền **thực thu**, dùng chung một hàm với báo cáo chi nhánh của thu ngân (hóa đơn đã thanh toán, sau giảm giá), có cột Tiền mặt / Chuyển khoản. Playwright so khớp Admin = thu ngân.
