# Cấu hình demo (06/11/2026) — ĐÃ CHỐT, ĐÃ NHẬP (trừ toạ độ Gò Vấp, Bình Thạnh)

> **Chốt 06/11/2026 (chủ dự án duyệt) và đã nhập qua giao diện Admin Production lúc 20:1x** (tài khoản admin chủ dự án
> đăng nhập sẵn; không chạy SQL). Quyết định của chủ dự án so với đề xuất bên dưới:
> - Bỏ dấu chay: Gỏi cuốn (ID 9), Cải thìa xào dầu hào (ID 53) — **đã nhập**.
> - Cay: ID 32 Không, ID 34 Không, ID 26 Có — **đã nhập**.
> - Khẩu phần: ID 48 = 4, ID 49 = 4, ID 37 = 3 — **đã nhập**.
> - Trà đá (ID 15): giữ 10.000đ, mô tả đổi thành "Trà đá giải khát" (bỏ "miễn phí") — **đã nhập**.
> - Thêm 2 món chính chay cho cả 3 chi nhánh, không ảnh (ảnh mặc định của app), mô tả "Món chính chay.":
>   **ID 58 "Đậu hũ sốt cà chua" 55.000đ** và **ID 59 "Cơm chiên chay" 50.000đ** — danh mục Món chính, đơn vị Phần,
>   Chay Có, Cay Không, Khẩu phần 1 — **đã nhập**, có trên thực đơn cả 3 chi nhánh (API `/branches/{1,2,3}/menu`).
> - Khu vực + ghép bàn Quận 1: đúng bảng mục 2 — **đã nhập** (20 bàn, tên khu "Khu A - Cửa sổ", "Khu B - Sảnh giữa",
>   "Khu C - Sảnh trong", "Khu D - Bàn nhóm"; dùng dấu "-" thay "–").
> - Toạ độ: Quận 1 = 10.7743, 106.7036 — **đã nhập**. Gò Vấp = 10.8345, 106.6660; Bình Thạnh = 10.8015, 106.7108 —
>   **CHƯA nhập**: tài khoản admin đang dùng chỉ quản lý chi nhánh Quận 1 (trang "Vị trí chi nhánh" chỉ liệt kê Quận 1), cần
>   admin của 2 chi nhánh kia.
> Kiểm tra sau khi nhập (API công khai): 44/54 món có loại (10 món "gọi thêm" để trống), mọi món khớp bảng dưới + quyết định trên.

## Đề xuất ban đầu (giữ để đối chiếu)

Trạng thái: **chỉ là đề xuất**. Chưa ghi gì vào DB. Chủ dự án duyệt (sửa trực tiếp trong file này nếu muốn), rồi Claude nhập
qua trang/API admin (không chạy SQL).

Nguồn dữ liệu: API công khai của Production lúc 06/11/2026 19:3x (`/api/mobile/v1/branches`, `/api/mobile/v1/branches/1/menu`),
sơ đồ bàn chi nhánh Quận 1 xem trên app (Samsung, màn "4. Chọn bàn", 07/11 19:00). Thực đơn 3 chi nhánh dùng chung 52 món;
thuộc tính món gắn theo món (áp cho mọi chi nhánh).

**Lưu ý để nhập:** API admin cần đăng nhập tài khoản quản trị. Claude không nhập mật khẩu, nên khi duyệt xong chủ dự án
đăng nhập Admin trên trình duyệt trong app Claude (hoặc tự nhập theo bảng dưới ở các trang "Thuộc tính món", "Khu vực & ghép bàn",
"Vị trí chi nhánh").

## 1. Thuộc tính món (52 món)

Quy ước: **Khẩu phần trống = 1 người**. "Cay" trống = chưa biết (khách bật "Không ăn cay" thì món đó bị bỏ).
Món **Loại trống** thì không bao giờ được gợi ý. "Chay" lấy theo cờ đang có trong DB, trừ chỗ có ⚠.

**Phát hiện quan trọng:** sau khi sửa 2 cờ chay sai (⚠), **không còn món chính chay nào** → khách có người ăn chay sẽ nhận câu
"Chưa có món chính nào được gắn là món chay". Muốn demo phần ăn chay cần thêm ít nhất 1 món chính chay (ví dụ đậu hũ, cơm chay).

| ID | Món | Nhóm trên thực đơn | Giá | Loại đề xuất | Chay | Cay | Khẩu phần | Lý do / ghi chú |
|---|---|---|---|---|---|---|---|---|
| 29 | Bánh mì ốp la | Món chính | 35.000đ | Món chính | Không | Không | — (1 người) | Món sáng, 1 phần/người. |
| 30 | Bò né | Món chính | 85.000đ | Món chính | Không | Không | — (1 người) |  |
| 4 | Bún Bò Huế | Món chính | 55.000đ | Món chính | Không | Có | — (1 người) | Mô tả ghi "Món cay đậm vị". |
| 2 | Bún Chả Hà Nội | Món chính | 60.000đ | Món chính | Không | Không | — (1 người) |  |
| 31 | Canh cải thịt bằm | Món chính | 55.000đ | Canh / súp | Không | Không | — (1 người) | Tên/mô tả là món canh, dùng chung với cơm. |
| 32 | Canh chua cá | Món chính | 70.000đ | Canh / súp | Không | — (để trống) | — (1 người) | Cay để trống: canh chua thường có ớt nhưng mô tả không nói. |
| 33 | Cơm bò lúc lắc | Món chính | 75.000đ | Món chính | Không | Không | — (1 người) |  |
| 34 | Cơm cá kho tộ | Món chính | 65.000đ | Món chính | Không | — (để trống) | — (1 người) | Cay để trống: cá kho tộ hay có tiêu/ớt, mô tả không nói. |
| 35 | Cơm chiên hải sản | Món chính | 70.000đ | Món chính | Không | Không | — (1 người) |  |
| 7 | Cơm Gà Xối Mỡ | Món chính | 70.000đ | Món chính | Không | Không | — (1 người) |  |
| 3 | Cơm Sườn Bì Chả | Món chính | 65.000đ | Món chính | Không | Không | — (1 người) |  |
| 36 | Cơm sườn nướng | Món chính | 65.000đ | Món chính | Không | Không | — (1 người) |  |
| 37 | Gà nướng muối ớt | Món chính | 120.000đ | Món chính | Không | Có | — (1 người) | Mô tả ghi "Món cay". Khẩu phần để trống: mô tả "nhóm đông" (0,45 kg gà) nhưng không ghi mấy người. |
| 5 | Hủ Tiếu Nam Vang | Món chính | 50.000đ | Món chính | Không | Không | — (1 người) |  |
| 6 | Mì Xào Bò | Món chính | 60.000đ | Món chính | Không | Không | — (1 người) |  |
| 1 | Phở Bò Đặc Biệt | Món chính | 75.000đ | Món chính | Không | Không | — (1 người) |  |
| 50 | Cháo gà | Món nước | 40.000đ | Món chính | Không | Không | — (1 người) | Nhóm "Món nước", 1 tô/người. |
| 51 | Phở gà | Món nước | 55.000đ | Món chính | Không | Không | — (1 người) | Nhóm "Món nước", 1 tô/người. |
| 22 | Chả Cá | Món phụ | 15.000đ | Món phụ | Không | Không | — (1 người) | Giá 15.000đ/dĩa, ăn kèm. |
| 9 | Gỏi Cuốn | Món phụ | 35.000đ | Khai vị | Không | Không | — (1 người) | ⚠ DB đang đánh dấu CHAY nhưng thành phần có TÔM → đề xuất bỏ dấu chay. |
| 8 | Nem Rán | Món phụ | 40.000đ | Khai vị | Không | Không | — (1 người) |  |
| 10 | Salad Rau Củ | Món phụ | 30.000đ | Món phụ | Có | Không | — (1 người) |  |
| 55 | Bò xào rau củ | Món xào | 90.000đ | Món phụ | Không | Không | — (1 người) | Món xào ăn chung với cơm: để Món phụ để không bị gợi ý làm suất chính một mình. |
| 54 | Mực xào chua ngọt | Món xào | 95.000đ | Món phụ | Không | Không | — (1 người) | Như trên. |
| 12 | Bánh Flan | Tráng miệng | 30.000đ | Tráng miệng | Có | Không | — (1 người) | Có trứng + sữa: chay nếu khách ăn chay có trứng sữa. |
| 56 | Chè đậu xanh | Tráng miệng | 30.000đ | Tráng miệng | Có | Không | — (1 người) |  |
| 11 | Chè Khúc Bạch | Tráng miệng | 35.000đ | Tráng miệng | Có | Không | — (1 người) |  |
| 13 | Chè Thái | Tráng miệng | 40.000đ | Tráng miệng | Có | Không | — (1 người) |  |
| 16 | Cà Phê Sữa Đá | Đồ uống | 40.000đ | Đồ uống | Có | Không | — (1 người) |  |
| 14 | Nước Cam Vắt | Đồ uống | 40.000đ | Đồ uống | Có | Không | — (1 người) |  |
| 17 | Sinh Tố Bơ | Đồ uống | 45.000đ | Đồ uống | Có | Không | — (1 người) |  |
| 15 | Trà Đá | Đồ uống | 10.000đ | Đồ uống | Có | Không | — (1 người) | ⚠ Mô tả ghi "Trà đá miễn phí" nhưng giá 10.000đ: nên sửa một trong hai. |
| 23 | Trà đào | Đồ uống | 35.000đ | Đồ uống | Có | Không | — (1 người) |  |
| 24 | Trà tắc | Đồ uống | 25.000đ | Đồ uống | Có | Không | — (1 người) |  |
| 28 | Chả giò hải sản | Khai vị | 55.000đ | Khai vị | Không | Không | — (1 người) |  |
| 26 | Gỏi ngó sen tôm thịt | Khai vị | 65.000đ | Khai vị | Không | — (để trống) | — (1 người) | Cay để trống: gỏi thường có ớt, mô tả chỉ nói "ăn kèm món cay". |
| 27 | Khoai tây chiên | Khai vị | 35.000đ | Khai vị | Có | Không | — (1 người) |  |
| 25 | Súp cua | Khai vị | 45.000đ | Khai vị | Không | Không | — (1 người) | Nhóm "Khai vị" trên thực đơn nên để Khai vị (không để Canh/súp). |
| 48 | Lẩu gà lá é | Món lẩu | 220.000đ | Món chính | Không | Không | — (1 người) | Khẩu phần để trống: 1 nồi cho mấy người thì thực đơn không ghi (gợi ý 3–4, chủ dự án xác nhận). Để trống thì hệ thống coi 1 nồi = 1 người nên gần như không gợi ý lẩu. |
| 49 | Lẩu thái hải sản | Món lẩu | 250.000đ | Món chính | Không | Có | — (1 người) | Mô tả ghi "Món cay". Khẩu phần để trống: lý do như lẩu gà. |
| 53 | Cải thìa xào dầu hào | Món rau | 45.000đ | Món phụ | Không | Không | — (1 người) | ⚠ DB đang đánh dấu CHAY nhưng dầu hào làm từ hàu → đề xuất bỏ dấu chay. |
| 52 | Rau muống xào tỏi | Món rau | 40.000đ | Món phụ | Có | Không | — (1 người) | Chay nếu bếp không nêm nước mắm. |
| 38 | Thêm bánh phở | Món gọi thêm | 12.000đ | — (để trống) | Có | — | — (1 người) | Món gọi thêm (ăn kèm), không phải một món trong bữa → để trống để không bao giờ được gợi ý. |
| 39 | Thêm bún | Món gọi thêm | 10.000đ | — (để trống) | Có | — | — (1 người) | Món gọi thêm (ăn kèm), không phải một món trong bữa → để trống để không bao giờ được gợi ý. |
| 40 | Thêm cơm | Món gọi thêm | 10.000đ | — (để trống) | Có | — | — (1 người) | Món gọi thêm (ăn kèm), không phải một món trong bữa → để trống để không bao giờ được gợi ý. |
| 41 | Thêm đồ chua | Món gọi thêm | 5.000đ | — (để trống) | Có | — | — (1 người) | Món gọi thêm (ăn kèm), không phải một món trong bữa → để trống để không bao giờ được gợi ý. |
| 42 | Thêm dưa leo | Món gọi thêm | 5.000đ | — (để trống) | Có | — | — (1 người) | Món gọi thêm (ăn kèm), không phải một món trong bữa → để trống để không bao giờ được gợi ý. |
| 43 | Thêm mì | Món gọi thêm | 12.000đ | — (để trống) | Có | — | — (1 người) | Món gọi thêm (ăn kèm), không phải một món trong bữa → để trống để không bao giờ được gợi ý. |
| 44 | Thêm nước chấm | Món gọi thêm | 5.000đ | — (để trống) | Có | — | — (1 người) | Món gọi thêm (ăn kèm), không phải một món trong bữa → để trống để không bao giờ được gợi ý. |
| 45 | Thêm nước dùng | Món gọi thêm | 8.000đ | — (để trống) | Có | — | — (1 người) | Món gọi thêm (ăn kèm), không phải một món trong bữa → để trống để không bao giờ được gợi ý. |
| 46 | Thêm rau | Món gọi thêm | 10.000đ | — (để trống) | Có | — | — (1 người) | Món gọi thêm (ăn kèm), không phải một món trong bữa → để trống để không bao giờ được gợi ý. |
| 47 | Thêm trứng | Món gọi thêm | 8.000đ | — (để trống) | Không | — | — (1 người) | Món gọi thêm (ăn kèm), không phải một món trong bữa → để trống để không bao giờ được gợi ý. |

Tổng: Món chính 18, Canh / súp 2, Món phụ 6, Khai vị 6, Tráng miệng 4, Đồ uống 6, — (để trống) 10 (= 52 món).

Cần chủ dự án quyết: khẩu phần 2 nồi lẩu (ID 48, 49) và Gà nướng muối ớt (ID 37); cay của ID 32, 34, 26; có sửa cờ chay
ID 9 (Gỏi cuốn) và 53 (Cải thìa dầu hào) không; Trà đá (ID 15) miễn phí hay 10.000đ.

## 2. Khu vực + ghép bàn — chi nhánh Quận 1

Bàn hiện có (20 bàn, đọc từ màn chọn bàn trên app): Bàn 1–5 (2 chỗ), Bàn 6–15 (4 chỗ), Bàn 16–18 (6 chỗ), Bàn 19–20 (8 chỗ).
**Sơ đồ hiện tại chỉ xếp bàn theo số chỗ, không có vị trí thật trong nhà hàng**, nên "cạnh nhau" dưới đây là giả định
"số liền nhau = kê sát nhau". Chủ dự án sửa lại theo mặt bằng thật trước khi duyệt.

| Khu đề xuất | Bàn | Cặp ghép được (kê sát) | Nhóm khách ghép được |
|---|---|---|---|
| Khu A – Cửa sổ | 1, 2, 3, 4, 5 (2 chỗ) | 1+2, 2+3, 3+4, 4+5 | 2 bàn = 4 khách; 3 bàn liền = 6 khách |
| Khu B – Sảnh giữa | 6, 7, 8, 9, 10 (4 chỗ) | 6+7, 7+8, 8+9, 9+10 | 2 bàn = 8; 3 bàn = 12; 4 bàn = 16 khách |
| Khu C – Sảnh trong | 11, 12, 13, 14, 15 (4 chỗ) | 11+12, 12+13, 13+14, 14+15 | như Khu B |
| Khu D – Bàn nhóm | 16, 17, 18 (6 chỗ), 19, 20 (8 chỗ) | 16+17, 17+18, 19+20 | 12, 18 khách (bàn 6); 16 khách (19+20) |

Không ghép bàn khác khu (hệ thống cũng không cho). Mỗi nhóm tối đa 4 bàn. Hệ thống chọn ít bàn nhất, rồi ít ghế thừa nhất.
Chi nhánh Bình Thạnh, Gò Vấp: chưa đề xuất (đề bài chỉ yêu cầu Quận 1); chưa cấu hình thì không ghép, đặt bàn như cũ.

## 3. Toạ độ chi nhánh — chủ dự án tự lấy (Claude không đoán)

| Chi nhánh | ID | Địa chỉ đang lưu | Vĩ độ | Kinh độ |
|---|---|---|---|---|
| Chi nhánh Quận 1 | 1 | 123 Nguyễn Huệ, Quận 1, TP.HCM | (chưa có) | (chưa có) |
| Chi nhánh Gò Vấp | 2 | 456 Quang Trung, Gò Vấp, TP.HCM | (chưa có) | (chưa có) |
| Chi nhánh Bình Thạnh | 3 | 789 Điện Biên Phủ, Bình Thạnh, TP.HCM | (chưa có) | (chưa có) |

Cách lấy: mở Google Maps, nhấn giữ đúng vị trí nhà hàng → dòng số hiện ra, số đầu là **vĩ độ** (khoảng 10.7…), số sau là
**kinh độ** (khoảng 106.6…–106.7…). Gửi Claude 2 số mỗi chi nhánh, hoặc nhập ở Admin → "Vị trí chi nhánh".
Nếu địa chỉ đang lưu không phải địa chỉ thật, sửa luôn ở trang đó.
