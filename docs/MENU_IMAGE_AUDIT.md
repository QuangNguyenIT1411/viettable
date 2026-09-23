# VietTable menu image audit — 2026-09-24

## Phạm vi và kết luận

- Nguồn dữ liệu: `GET /api/mobile/v1/branches/{id}/menu` qua Gateway, 3 chi nhánh, cùng 52 món active/available.
- Trước sửa kỹ thuật: **37 BROKEN_IMAGE**, **13 LOW_QUALITY**, **2 OK**. Không thấy URL rỗng hoặc ảnh trùng hash; đợt này chưa xác nhận đầy đủ ngữ nghĩa ảnh. Có hai asset ứng viên cũ không khớp tốt với tên món (`com-ca-kho-to.jpg` thiếu tộ; `com-bo-luc-lac.jpg` chủ yếu là cơm chiên), nhưng URL của chúng khi đó đã 404; không tính thành lỗi WRONG_IMAGE kép.
- Sau sửa: **52/52 URL ảnh trả HTTP 200, Content-Type image/** ở cả ba chi nhánh; **52 hash khác nhau**; không còn ảnh dưới 500 px chiều rộng hoặc 350 px chiều cao. Ảnh trên Samsung đã được kiểm tra ở danh sách Món chính, gồm Cơm bò lúc lắc và Cơm cá kho tộ. Final semantic QA ở cuối tài liệu đã sửa thêm hai ảnh gây hiểu nhầm.
- Tổng kết phân loại ban đầu: **MISSING_IMAGE 0, BROKEN_IMAGE 37, WRONG_IMAGE 0, DUPLICATE_SUSPICIOUS 0, LOW_QUALITY 13, OK 2**. Đã sửa **50 món**: 25 dùng ảnh sẵn có/đổi đường dẫn, 25 dùng ảnh mới tạo. Hai món OK giữ nguyên. Menu hiện không có món tên “Cơm tấm”, “Bò kho” hoặc combo/set riêng; không gán ảnh hay dựng món giả để kiểm tra các ví dụ đó.

## Nguyên nhân và cách sửa

1. Catalog seed/DB lưu đường dẫn `/images/<slug>.jpg`, trong khi nhiều file thật nằm tại `wwwroot/images/dishes/`; một số file là PNG/WebP, hoặc sai chính tả tên file (Bánh Flan). Gateway trả 404; Android `ImageLoader` và web chỉ dùng URL Catalog đưa ra, không tự gán nhầm món.
2. Mười ba ảnh active cũ chỉ 220 px chiều cao (và đa số 220–404 px chiều rộng); ở màn chi tiết dễ mờ. Thay bằng asset WebP độ phân giải cao hơn đã kiểm tra bằng mắt.
3. Ba món gọi thêm chưa có file phù hợp; tạo ảnh thật riêng. Ảnh Phở Bò được tái dùng từ asset Android hiện có, không hotlink bên ngoài.
4. `CatalogDbBootstrapper.EnsureDishImagePathsAsync` chỉ sửa các path demo cũ được liệt kê cụ thể, kể cả path trung gian của lần deploy đầu. Không chạm đường ảnh upload/custom. Không reset hoặc xóa database/volume.
5. Android nhận `image` từ `MobileRepository.parseDish`, ghép URL Gateway qua `ApiClient.absoluteUrl`, tải bằng `ImageLoader`; web cũng dùng `image` của menu. Không cần thay code client.

## Từng món (trạng thái trước sửa → đường ảnh hiện tại)

| ID | Món | Danh mục | Ảnh trước | Trạng thái trước | Ảnh hiện tại | Nguồn sửa |
|---:|---|---|---|---|---|---|
| 29 | Bánh mì ốp la | Món chính | `/images/banh-mi-op-la.jpg` | BROKEN_IMAGE | `/images/dishes/banh-mi-op-la.jpg` | Asset sẵn có |
| 30 | Bò né | Món chính | `/images/bo-ne.jpg` | BROKEN_IMAGE | `/images/dishes/bo-ne.png` | Asset sẵn có |
| 4 | Bún Bò Huế | Món chính | `/images/bun-bo-hue.jpg` | LOW_QUALITY | `/images/dishes/bun-bo-hue-ai.webp` | AI |
| 2 | Bún Chả Hà Nội | Món chính | `/images/bun-cha-ha-noi.jpg` | LOW_QUALITY | `/images/dishes/bun-cha-ha-noi-ai.webp` | AI |
| 31 | Canh cải thịt bằm | Món chính | `/images/canh-cai-thit-bam.jpg` | BROKEN_IMAGE | `/images/dishes/canh-cai-thit-bam.jpg` | Asset sẵn có |
| 32 | Canh chua cá | Món chính | `/images/canh-chua-ca.jpg` | BROKEN_IMAGE | `/images/dishes/canh-chua-ca.jpg` | Asset sẵn có |
| 33 | Cơm bò lúc lắc | Món chính | `/images/com-bo-luc-lac.jpg` | BROKEN_IMAGE | `/images/dishes/com-bo-luc-lac-ai.webp` | AI |
| 34 | Cơm cá kho tộ | Món chính | `/images/com-ca-kho-to.jpg` | BROKEN_IMAGE | `/images/dishes/com-ca-kho-to-ai.webp` | AI |
| 35 | Cơm chiên hải sản | Món chính | `/images/com-chien-hai-san.jpg` | BROKEN_IMAGE | `/images/dishes/com-chien-hai-san.jpg` | Asset sẵn có |
| 7 | Cơm Gà Xối Mỡ | Món chính | `/images/com-ga-xoi-mo.jpg` | LOW_QUALITY | `/images/dishes/com-ga-xoi-mo-ai.webp` | AI |
| 3 | Cơm Sườn Bì Chả | Món chính | `/images/com-suon-bi-cha.jpg` | LOW_QUALITY | `/images/dishes/com-suon-bi-cha-ai.webp` | AI |
| 36 | Cơm sườn nướng | Món chính | `/images/com-suon-nuong.jpg` | BROKEN_IMAGE | `/images/dishes/com-suon-nuong-ai.webp` | AI |
| 37 | Gà nướng muối ớt | Món chính | `/images/ga-nuong-muoi-ot.jpg` | BROKEN_IMAGE | `/images/dishes/ga-nuong-muoi-ot-ai.webp` | AI |
| 5 | Hủ Tiếu Nam Vang | Món chính | `/images/hu-tieu-nam-vang.jpg` | LOW_QUALITY | `/images/dishes/hu-tieu-nam-vang-ai.webp` | AI |
| 6 | Mì Xào Bò | Món chính | `/images/mi-xao-bo.jpg` | LOW_QUALITY | `/images/dishes/mi-xao-bo-ai.webp` | AI |
| 1 | Phở Bò Đặc Biệt | Món chính | `/images/pho-bo-dac-biet.jpg` | BROKEN_IMAGE | `/images/dishes/pho-bo-dac-biet.jpg` | Asset sẵn có |
| 50 | Cháo gà | Món nước | `/images/chao-ga.jpg` | BROKEN_IMAGE | `/images/dishes/chao-ga.jpg` | Asset sẵn có |
| 51 | Phở gà | Món nước | `/images/pho-ga.jpg` | BROKEN_IMAGE | `/images/dishes/pho-ga-ai.webp` | AI |
| 22 | Chả Cá | Món phụ | `/images/cha-ca.jpg` | BROKEN_IMAGE | `/images/dishes/cha-ca.jpg` | Asset sẵn có |
| 9 | Gỏi Cuốn | Món phụ | `/images/goi-cuon.jpg` | LOW_QUALITY | `/images/dishes/goi-cuon-ai.webp` | AI |
| 8 | Nem Rán | Món phụ | `/images/nem-ran.jpg` | LOW_QUALITY | `/images/dishes/nem-ran-ai.webp` | AI |
| 10 | Salad Rau Củ | Món phụ | `/images/salad-rau-cu.jpg` | LOW_QUALITY | `/images/dishes/salad-rau-cu-ai.webp` | AI |
| 55 | Bò xào rau củ | Món xào | `/images/bo-xao-rau-cu.jpg` | BROKEN_IMAGE | `/images/dishes/bo-xao-rau-cu.jpg` | Asset sẵn có |
| 54 | Mực xào chua ngọt | Món xào | `/images/muc-xao-chua-ngot.jpg` | BROKEN_IMAGE | `/images/dishes/muc-xao-chua-ngot-ai.webp` | AI |
| 12 | Bánh Flan | Tráng miệng | `/images/banh-flan.jpg` | BROKEN_IMAGE | `/images/ban-flan.jpg` | Asset sẵn có |
| 56 | Chè đậu xanh | Tráng miệng | `/images/che-dau-xanh.jpg` | BROKEN_IMAGE | `/images/dishes/che-dau-xanh.jpg` | Asset sẵn có |
| 11 | Chè Khúc Bạch | Tráng miệng | `/images/che-khuc-bach.jpg` | OK | `/images/che-khuc-bach.jpg` | Giữ nguyên |
| 13 | Chè Thái | Tráng miệng | `/images/che-thai.jpg` | LOW_QUALITY | `/images/dishes/che-thai-ai.webp` | AI |
| 16 | Cà Phê Sữa Đá | Đồ uống | `/images/ca-phe-sua-da.jpg` | LOW_QUALITY | `/images/dishes/ca-phe-sua-da-ai.webp` | AI |
| 14 | Nước Cam Vắt | Đồ uống | `/images/nuoc-cam-vat.jpg` | LOW_QUALITY | `/images/dishes/nuoc-cam-vat-ai.webp` | AI |
| 17 | Sinh Tố Bơ | Đồ uống | `/images/sinh-to-bo.jpg` | OK | `/images/sinh-to-bo.jpg` | Giữ nguyên |
| 15 | Trà Đá | Đồ uống | `/images/tra-da.jpg` | LOW_QUALITY | `/images/dishes/tra-da-ai.webp` | AI |
| 23 | Trà đào | Đồ uống | `/images/tra-dao.jpg` | BROKEN_IMAGE | `/images/dishes/tra-dao.png` | Asset sẵn có |
| 24 | Trà tắc | Đồ uống | `/images/tra-tac.jpg` | BROKEN_IMAGE | `/images/dishes/tra-tac-ai.webp` | AI, thay ở final semantic QA |
| 28 | Chả giò hải sản | Khai vị | `/images/cha-gio-hai-san.jpg` | BROKEN_IMAGE | `/images/dishes/cha-gio-hai-san.jpg` | Asset sẵn có |
| 26 | Gỏi ngó sen tôm thịt | Khai vị | `/images/goi-ngo-sen-tom-thit.jpg` | BROKEN_IMAGE | `/images/dishes/goi-ngo-sen-tom-thit.jpg` | Asset sẵn có |
| 27 | Khoai tây chiên | Khai vị | `/images/khoai-tay-chien.jpg` | BROKEN_IMAGE | `/images/dishes/khoai-tay-chien-ai.webp` | AI |
| 25 | Súp cua | Khai vị | `/images/sup-cua.jpg` | BROKEN_IMAGE | `/images/dishes/sup-cua.png` | Asset sẵn có |
| 48 | Lẩu gà lá é | Món lẩu | `/images/lau-ga-la-e.jpg` | BROKEN_IMAGE | `/images/dishes/lau-ga-la-e.jpg` | Asset sẵn có |
| 49 | Lẩu thái hải sản | Món lẩu | `/images/lau-thai-hai-san.jpg` | BROKEN_IMAGE | `/images/dishes/lau-thai-hai-san-ai.webp` | AI |
| 53 | Cải thìa xào dầu hào | Món rau | `/images/cai-thia-xao-dau-hao.jpg` | BROKEN_IMAGE | `/images/dishes/cai-thia-xao-dau-hao.png` | Asset sẵn có |
| 52 | Rau muống xào tỏi | Món rau | `/images/rau-muong-xao-toi.jpg` | BROKEN_IMAGE | `/images/dishes/rau-muong-xao-toi.png` | Asset sẵn có |
| 38 | Thêm bánh phở | Món gọi thêm | `/images/them-banh-pho.jpg` | BROKEN_IMAGE | `/images/dishes/them-banh-pho.jpg` | Asset sẵn có |
| 39 | Thêm bún | Món gọi thêm | `/images/them-bun.jpg` | BROKEN_IMAGE | `/images/dishes/them-bun.png` | Asset sẵn có |
| 40 | Thêm cơm | Món gọi thêm | `/images/them-com.jpg` | BROKEN_IMAGE | `/images/dishes/them-com-ai.webp` | AI |
| 41 | Thêm đồ chua | Món gọi thêm | `/images/them-do-chua.jpg` | BROKEN_IMAGE | `/images/dishes/them-do-chua.jpg` | Asset sẵn có |
| 42 | Thêm dưa leo | Món gọi thêm | `/images/them-dua-leo.jpg` | BROKEN_IMAGE | `/images/dishes/them-dua-leo.webp` | Asset sẵn có |
| 43 | Thêm mì | Món gọi thêm | `/images/them-mi.jpg` | BROKEN_IMAGE | `/images/dishes/them-mi-ai.webp` | AI, thay ở final semantic QA |
| 44 | Thêm nước chấm | Món gọi thêm | `/images/them-nuoc-cham.jpg` | BROKEN_IMAGE | `/images/dishes/them-nuoc-cham-ai.webp` | AI |
| 45 | Thêm nước dùng | Món gọi thêm | `/images/them-nuoc-dung.jpg` | BROKEN_IMAGE | `/images/dishes/them-nuoc-dung-ai.webp` | AI |
| 46 | Thêm rau | Món gọi thêm | `/images/them-rau.jpg` | BROKEN_IMAGE | `/images/dishes/them-rau.jpg` | Asset sẵn có |
| 47 | Thêm trứng | Món gọi thêm | `/images/them-trung.jpg` | BROKEN_IMAGE | `/images/dishes/them-trung-ai.webp` | AI |

## Asset mới và nguồn

- 27 file `/images/dishes/*-ai.webp` hiện active: 25 ảnh từ đợt trước, 2 ảnh final semantic QA. Tạo bằng built-in image generation, sau đó mã hóa WebP để giảm kích thước; ảnh giữ nội dung món, chụp ngang trên bàn ăn đơn giản, không người/chữ/logo/watermark. Hai prompt bổ sung ở phần final QA bên dưới.
- `pho-bo-dac-biet.jpg` sao chép từ `app/src/main/res/drawable/food_pho_bo.jpg` sẵn có.
- Các ảnh còn lại dùng file hiện có trong Gateway `wwwroot/images` hoặc `wwwroot/images/dishes`. Không tải ảnh có bản quyền không rõ từ Internet.
- Ảnh legacy không dùng và file upload debug không bị xóa vì có thể còn tham chiếu ngoài menu active.
- Phần audit kỹ thuật này không thay thế được kiểm tra ngữ nghĩa bằng mắt; final semantic QA đủ 52 ảnh được ghi ở phần bên dưới. Với món mới hoặc ảnh do admin thay về sau, cần kiểm tra lại trước khi xuất bản.

## Kiểm chứng

- `docker compose build catalog gateway` — pass; `docker compose up -d --no-deps catalog gateway` — pass, không khởi tạo lại DB.
- `powershell -ExecutionPolicy Bypass -File scripts/test-menu-images.ps1` — pass: 3 chi nhánh × 52 món, 52 URL riêng trả ảnh HTTP 200.
- `:app:assembleDebug :app:testDebugUnitTest` — pass; không đổi Android source.
- Samsung SM-A528B, `adb reverse tcp:5100 tcp:5100`: mở app, danh sách Món chính tải ảnh mới; đã xem màn có Bún Bò Huế, Bún Chả, Cơm bò lúc lắc, Cơm cá kho tộ, Cơm gà xối mỡ và Cơm sườn bì chả. Mở chi tiết Cơm bò lúc lắc: ảnh hero sắc nét, đúng món. Chưa kiểm tra thủ công từng ảnh trên mọi loại thiết bị/kích thước màn.

## Final visual/semantic QA — 2026-09-24

Danh sách canonical lấy lại từ `GET /api/mobile/v1/branches/1/menu` qua Gateway sau deploy. Cả chi nhánh Bình Thạnh, Gò Vấp và Quận 1 trả cùng 52 `dishId`/ảnh; bảng dưới áp dụng cho cả ba. Đã **xem bằng mắt đủ 52 ảnh** theo bốn contact sheet 13 ảnh/trang, mở riêng năm ảnh dễ nhầm (Canh chua cá, Cháo gà, Trà tắc, Gỏi ngó sen tôm thịt, Thêm mì), và xem lại hai ảnh thay thế ở kích thước gốc. `PASS` ở đây là phù hợp tên món và chất lượng ảnh trên menu, không phải kết luận về công thức chế biến chi tiết. 27 ảnh AI active được kiểm tra thêm về lỗi vật thể, chữ, watermark và logo; không thấy lỗi rõ ràng.

| ItemId | Tên món | Image | Source | Semantic Result | Note quan sát bằng mắt |
|---:|---|---|---|---|---|
| 29 | Bánh mì ốp la | `/images/dishes/banh-mi-op-la.jpg` | Reuse | PASS | Bánh mì kẹp trứng ốp la rõ lòng đỏ. |
| 30 | Bò né | `/images/dishes/bo-ne.png` | Reuse | PASS | Bò và trứng trên chảo gang nóng. |
| 4 | Bún Bò Huế | `/images/dishes/bun-bo-hue-ai.webp` | AI | PASS | Bún sợi tròn, thịt bò và nước dùng đỏ. |
| 2 | Bún Chả Hà Nội | `/images/dishes/bun-cha-ha-noi-ai.webp` | AI | PASS | Thịt nướng trong bát nước chấm, bún và rau riêng. |
| 31 | Canh cải thịt bằm | `/images/dishes/canh-cai-thit-bam.jpg` | Reuse | PASS | Lá cải xanh và thịt bằm trong canh. |
| 32 | Canh chua cá | `/images/dishes/canh-chua-ca.jpg` | Reuse | PASS | Khúc cá, thơm, cà chua, giá và dọc mùng trong nước canh. |
| 33 | Cơm bò lúc lắc | `/images/dishes/com-bo-luc-lac-ai.webp` | AI | PASS | Bò cắt khối xào rau củ cạnh cơm trắng. |
| 34 | Cơm cá kho tộ | `/images/dishes/com-ca-kho-to-ai.webp` | AI | PASS | Cá kho trong tộ đất, cơm trắng bên cạnh. |
| 35 | Cơm chiên hải sản | `/images/dishes/com-chien-hai-san.jpg` | Reuse | PASS | Cơm chiên với tôm và rau củ. |
| 7 | Cơm Gà Xối Mỡ | `/images/dishes/com-ga-xoi-mo-ai.webp` | AI | PASS | Đùi gà da vàng giòn cạnh cơm. |
| 3 | Cơm Sườn Bì Chả | `/images/dishes/com-suon-bi-cha-ai.webp` | AI | PASS | Cơm, sườn nướng, bì và chả trứng hiện rõ. |
| 36 | Cơm sườn nướng | `/images/dishes/com-suon-nuong-ai.webp` | AI | PASS | Miếng sườn nướng và cơm, không lẫn bì/chả. |
| 37 | Gà nướng muối ớt | `/images/dishes/ga-nuong-muoi-ot-ai.webp` | AI | PASS | Gà nướng nguyên phần, màu ớt trên bề mặt. |
| 5 | Hủ Tiếu Nam Vang | `/images/dishes/hu-tieu-nam-vang-ai.webp` | AI | PASS | Hủ tiếu với tôm, thịt, trứng và nước dùng. |
| 6 | Mì Xào Bò | `/images/dishes/mi-xao-bo-ai.webp` | AI | PASS | Mì xào cùng miếng bò và rau. |
| 1 | Phở Bò Đặc Biệt | `/images/dishes/pho-bo-dac-biet.jpg` | Reuse | PASS | Bánh phở dẹt, thịt bò trong tô nước dùng. |
| 50 | Cháo gà | `/images/dishes/chao-ga.jpg` | Reuse | PASS | Nền cháo trắng đặc với miếng gà, hành và rau. |
| 51 | Phở gà | `/images/dishes/pho-ga-ai.webp` | AI | PASS | Bánh phở và thịt gà xé trong tô nước dùng. |
| 22 | Chả Cá | `/images/dishes/cha-ca.jpg` | Reuse | PASS | Các miếng chả cá chiên tròn và chén nước chấm. |
| 9 | Gỏi Cuốn | `/images/dishes/goi-cuon-ai.webp` | AI | PASS | Cuốn tươi bánh tráng trong, thấy tôm và rau. |
| 8 | Nem Rán | `/images/dishes/nem-ran-ai.webp` | AI | PASS | Nem rán vàng, phần cắt cho thấy nhân. |
| 10 | Salad Rau Củ | `/images/dishes/salad-rau-cu-ai.webp` | AI | PASS | Rau lá, cà chua và dưa leo tươi. |
| 55 | Bò xào rau củ | `/images/dishes/bo-xao-rau-cu.jpg` | Reuse | PASS | Miếng bò xào với bông cải và rau. |
| 54 | Mực xào chua ngọt | `/images/dishes/muc-xao-chua-ngot-ai.webp` | AI | PASS | Khoanh mực xào với thơm và rau củ. |
| 12 | Bánh Flan | `/images/ban-flan.jpg` | Reuse | PASS | Bánh custard phủ caramel. |
| 56 | Chè đậu xanh | `/images/dishes/che-dau-xanh.jpg` | Reuse | PASS | Chè đậu xanh vàng trong bát. |
| 11 | Chè Khúc Bạch | `/images/che-khuc-bach.jpg` | Giữ nguyên | PASS | Khối khúc bạch, nhãn/vải và hạnh nhân. |
| 13 | Chè Thái | `/images/dishes/che-thai-ai.webp` | AI | PASS | Trái cây, thạch màu và nước cốt dừa trong bát. |
| 16 | Cà Phê Sữa Đá | `/images/dishes/ca-phe-sua-da-ai.webp` | AI | PASS | Cà phê đá pha sữa trong ly, hai lớp màu. |
| 14 | Nước Cam Vắt | `/images/dishes/nuoc-cam-vat-ai.webp` | AI | PASS | Ly nước cam và quả cam cắt đôi. |
| 17 | Sinh Tố Bơ | `/images/sinh-to-bo.jpg` | Giữ nguyên | PASS | Sinh tố màu bơ xanh đặc, miếng bơ trang trí. |
| 15 | Trà Đá | `/images/dishes/tra-da-ai.webp` | AI | PASS | Ly trà hổ phách với đá, không gán trái cây khác. |
| 23 | Trà đào | `/images/dishes/tra-dao.png` | Reuse | PASS | Ly trà có lát đào màu vàng/cam. |
| 24 | Trà tắc | `/images/dishes/tra-tac-ai.webp` | AI (final fix) | PASS | Trà đá với tắc cắt lát và quả tắc nhỏ; không còn nửa quả cam gây nhầm. |
| 28 | Chả giò hải sản | `/images/dishes/cha-gio-hai-san.jpg` | Reuse | PASS | Chả giò chiên, phần cắt có nhân hải sản. |
| 26 | Gỏi ngó sen tôm thịt | `/images/dishes/goi-ngo-sen-tom-thit.jpg` | Reuse | PASS | Tôm, lát thịt và ngó sen thái sợi trong gỏi. |
| 27 | Khoai tây chiên | `/images/dishes/khoai-tay-chien-ai.webp` | AI | PASS | Khoai tây cắt que chiên vàng. |
| 25 | Súp cua | `/images/dishes/sup-cua.png` | Reuse | PASS | Súp sánh với thịt cua, trứng và rau thơm. |
| 48 | Lẩu gà lá é | `/images/dishes/lau-ga-la-e.jpg` | Reuse | PASS | Nồi lẩu gà với nhiều lá é/rau thơm. |
| 49 | Lẩu thái hải sản | `/images/dishes/lau-thai-hai-san-ai.webp` | AI | PASS | Nồi lẩu đỏ có tôm, mực và nghêu. |
| 53 | Cải thìa xào dầu hào | `/images/dishes/cai-thia-xao-dau-hao.png` | Reuse | PASS | Cải thìa xào bóng nước sốt. |
| 52 | Rau muống xào tỏi | `/images/dishes/rau-muong-xao-toi.png` | Reuse | PASS | Rau muống xào, có lát tỏi/ớt. |
| 38 | Thêm bánh phở | `/images/dishes/them-banh-pho.jpg` | Reuse | PASS | Bánh phở sợi dẹt trắng, không phải bún. |
| 39 | Thêm bún | `/images/dishes/them-bun.png` | Reuse | PASS | Bún sợi tròn trắng trong rổ. |
| 40 | Thêm cơm | `/images/dishes/them-com-ai.webp` | AI | PASS | Một bát cơm trắng riêng. |
| 41 | Thêm đồ chua | `/images/dishes/them-do-chua.jpg` | Reuse | PASS | Cà rốt, củ cải ngâm chua thái sợi. |
| 42 | Thêm dưa leo | `/images/dishes/them-dua-leo.webp` | Reuse | PASS | Dưa leo thái lát tươi. |
| 43 | Thêm mì | `/images/dishes/them-mi-ai.webp` | AI (final fix) | PASS | Mì trứng đã nấu, sợi mềm rời trong bát; không còn mì xoăn kiểu mì gói. |
| 44 | Thêm nước chấm | `/images/dishes/them-nuoc-cham-ai.webp` | AI | PASS | Bát nước chấm với tỏi và ớt. |
| 45 | Thêm nước dùng | `/images/dishes/them-nuoc-dung-ai.webp` | AI | PASS | Bát nước dùng trong, không có món chính. |
| 46 | Thêm rau | `/images/dishes/them-rau.jpg` | Reuse | PASS | Phần rau xanh ăn kèm. |
| 47 | Thêm trứng | `/images/dishes/them-trung-ai.webp` | AI | PASS | Một trứng ốp la riêng. |

**Tổng kết:** Total 52; Semantic PASS 52; WRONG_DISH 0; AMBIGUOUS 0; LOW_VISUAL_QUALITY 0 còn mở. Trong lần review này phát hiện và sửa 2 trường hợp dễ gây sai ngữ nghĩa: ảnh cũ Trà tắc có nửa quả cam chiếm khung, ảnh cũ Thêm mì là mì xoăn kiểu mì gói. Hiện active: 27 AI, 23 reuse remap, 2 ảnh giữ nguyên. Hai ảnh mới dùng prompt cụ thể: (1) bát mì trứng đã nấu, sợi mềm rời, không gạch mì gói/nhân khác; (2) ly trà đá hổ phách với tắc nhỏ, không cam/chanh lớn. Không thay 50 ảnh khác, không xóa asset legacy.

**Runtime final:** `scripts/test-menu-images.ps1` PASS 3 chi nhánh × 52 món, tất cả URL HTTP 200. Samsung SM-A528B: đã thấy list với Cơm bò lúc lắc, Cơm cá kho tộ, Cơm chiên hải sản, Cơm Gà Xối Mỡ, Bún Bò Huế, Bún Chả, Phở gà, Chè Khúc Bạch, Chè Thái, Cà phê sữa đá, Nước cam vắt và ảnh mới Thêm mì; mở chi tiết Bún Bò Huế → back → cuộn tiếp, không thấy tráo ảnh. Web customer menu: ảnh Bánh mì ốp la/Chả Cá/Lẩu thái hải sản tải thật; lọc Trà tắc và mở chi tiết cho thấy asset mới `/images/dishes/tra-tac-ai.webp` (`naturalWidth=1448`). Screenshot/contact sheet nằm tại `D:\Quang\VietTable-QA-2026-09-24\` local test-only, không đưa vào APK/Git. Cashier không có màn duyệt Catalog bằng ảnh; trang dashboard chỉ hiện thumbnail 60 px của món trong active order nếu có. Không có active order phù hợp để spot-check Cashier mà không tạo dữ liệu nghiệp vụ mới.

**Icon launcher:** Trước QA là Android Studio default robot/nền lưới, phân loại `DEFAULT_ANDROID_ICON` và `WRONG_BRANDING`. Sau QA dùng biểu tượng sen–bát có sẵn của VietTable với nền kem, foreground trong adaptive safe zone (22–86 dp trên 108 dp), monochrome riêng, round icon và bitmap WebP 48/72/96/144/192 px. Không thay splash/logo trong app. Đã cài APK bằng `adb install -r` (giữ dữ liệu), xem icon trong App info **và app drawer Samsung**: không crop/mờ; mở lại app đến Home/menu thành công. `:app:assembleDebug :app:testDebugUnitTest` PASS. Một crash khi gọi app từ màn đa nhiệm sau khi restart Gateway được log ở SignalR/Splash; dừng tiến trình rồi mở foreground không tái hiện. Đó là issue lifecycle ngoài phạm vi ảnh/icon, không kết luận đã sửa.

**Kết luận:** `MENU IMAGES: PASS — 52/52 semantic verified`; `APP ICON: PASS` cho launcher/app info Samsung. Chưa kiểm tra launcher trên mọi OEM/density khác.
