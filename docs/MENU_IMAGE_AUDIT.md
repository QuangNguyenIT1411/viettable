# VietTable menu image audit — 2026-09-24

## Phạm vi và kết luận

- Nguồn dữ liệu: `GET /api/mobile/v1/branches/{id}/menu` qua Gateway, 3 chi nhánh, cùng 52 món active/available.
- Trước sửa: **37 BROKEN_IMAGE**, **13 LOW_QUALITY**, **2 OK**. Không thấy URL rỗng, ảnh trùng hash, hay ảnh sai ngữ nghĩa đang được tải thành công. Có hai asset ứng viên cũ không khớp tốt với tên món (`com-ca-kho-to.jpg` thiếu tộ; `com-bo-luc-lac.jpg` chủ yếu là cơm chiên), nhưng URL của chúng khi đó đã 404; không tính thành lỗi WRONG_IMAGE kép.
- Sau sửa: **52/52 URL ảnh trả HTTP 200, Content-Type image/** ở cả ba chi nhánh; **52 hash khác nhau**; không còn ảnh dưới 500 px chiều rộng hoặc 350 px chiều cao. Ảnh trên Samsung đã được kiểm tra ở danh sách Món chính, gồm Cơm bò lúc lắc và Cơm cá kho tộ.
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
| 24 | Trà tắc | Đồ uống | `/images/tra-tac.jpg` | BROKEN_IMAGE | `/images/dishes/tra-tac.png` | Asset sẵn có |
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
| 43 | Thêm mì | Món gọi thêm | `/images/them-mi.jpg` | BROKEN_IMAGE | `/images/dishes/them-mi.jpg` | Asset sẵn có |
| 44 | Thêm nước chấm | Món gọi thêm | `/images/them-nuoc-cham.jpg` | BROKEN_IMAGE | `/images/dishes/them-nuoc-cham-ai.webp` | AI |
| 45 | Thêm nước dùng | Món gọi thêm | `/images/them-nuoc-dung.jpg` | BROKEN_IMAGE | `/images/dishes/them-nuoc-dung-ai.webp` | AI |
| 46 | Thêm rau | Món gọi thêm | `/images/them-rau.jpg` | BROKEN_IMAGE | `/images/dishes/them-rau.jpg` | Asset sẵn có |
| 47 | Thêm trứng | Món gọi thêm | `/images/them-trung.jpg` | BROKEN_IMAGE | `/images/dishes/them-trung-ai.webp` | AI |

## Asset mới và nguồn

- 25 file `/images/dishes/*-ai.webp`: tạo bằng built-in image generation, sau đó mã hóa WebP chất lượng 85 để giảm kích thước; ảnh giữ nội dung món, chụp ngang trên bàn ăn đơn giản, không người/chữ/logo/watermark. Prompt nền chung: “photorealistic Vietnamese restaurant menu food photo, one correctly identified dish centered, horizontal card composition, natural daylight, no text, watermark, logos, people or extra dishes”; từng prompt nêu đặc trưng món: bún bò Huế có bún và nước dùng đỏ, bún chả có thịt nướng/bún/nước chấm, cơm bò lúc lắc có bò viên và cơm trắng, cá kho trong tộ đất, mì xào bò có mì rõ, cơm sườn bì chả đủ ba topping, gỏi cuốn không chiên, đồ uống đúng loại, và các món khác theo tên trong bảng.
- `pho-bo-dac-biet.jpg` sao chép từ `app/src/main/res/drawable/food_pho_bo.jpg` sẵn có.
- Các ảnh còn lại dùng file hiện có trong Gateway `wwwroot/images` hoặc `wwwroot/images/dishes`. Không tải ảnh có bản quyền không rõ từ Internet.
- Ảnh legacy không dùng và file upload debug không bị xóa vì có thể còn tham chiếu ngoài menu active.
- Không còn món active cần review thủ công vì lỗi được nhận diện trong audit này. Việc đánh giá chính xác ngữ nghĩa ảnh vẫn có yếu tố chủ quan; với món mới hoặc ảnh do admin thay về sau, nên kiểm tra bằng mắt trước khi xuất bản.

## Kiểm chứng

- `docker compose build catalog gateway` — pass; `docker compose up -d --no-deps catalog gateway` — pass, không khởi tạo lại DB.
- `powershell -ExecutionPolicy Bypass -File scripts/test-menu-images.ps1` — pass: 3 chi nhánh × 52 món, 52 URL riêng trả ảnh HTTP 200.
- `:app:assembleDebug :app:testDebugUnitTest` — pass; không đổi Android source.
- Samsung SM-A528B, `adb reverse tcp:5100 tcp:5100`: mở app, danh sách Món chính tải ảnh mới; đã xem màn có Bún Bò Huế, Bún Chả, Cơm bò lúc lắc, Cơm cá kho tộ, Cơm gà xối mỡ và Cơm sườn bì chả. Mở chi tiết Cơm bò lúc lắc: ảnh hero sắc nét, đúng món. Chưa kiểm tra thủ công từng ảnh trên mọi loại thiết bị/kích thước màn.
