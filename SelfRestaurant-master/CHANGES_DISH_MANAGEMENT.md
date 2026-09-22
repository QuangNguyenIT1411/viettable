# CẬP NHẬT QUẢN LÝ MÓN ĂN TRONG ADMIN

## Ngày: 10/05/2026

## Thay đổi

### 1. Đổi logic "Vô hiệu" → "Tạm ngưng/Tiếp tục"

**Trước đây:**
- Có 2 trạng thái: `isActive` (vô hiệu hóa) và `available` (đang bán/tạm ngưng)
- Nút "Vô hiệu" để đánh dấu món không còn dùng
- Phải vô hiệu hóa trước khi xóa

**Bây giờ:**
- Chỉ dùng trạng thái `available` (đang bán/tạm ngưng)
- Bỏ nút "Vô hiệu"
- Nút **"Tạm ngưng"** (màu vàng) cho món đang bán → đặt `available = false`
- Nút **"Tiếp tục"** (màu xám) cho món đã tạm ngưng → đặt `available = true`

### 2. Quy tắc xóa món ăn mới

**Trước đây:**
- Phải vô hiệu hóa (`isActive = false`) trước khi xóa
- Thông báo: "Vui lòng vô hiệu hóa trước khi xóa."

**Bây giờ:**
- Phải **tạm ngưng** (`available = false`) trước khi xóa
- Thông báo: "Vui lòng tạm ngưng món ăn trước khi xóa."
- Hiển thị dialog xác nhận khi xóa

### 3. Giao diện danh sách món ăn

**Cột trạng thái:**
- ✅ **Đang bán** (màu xanh) - `available = true`
- ⚠️ **Tạm ngưng** (màu vàng) - `available = false`

**Các nút hành động:**
1. **Sửa** - Mở form chỉnh sửa món
2. **Tạm ngưng** (hiện khi đang bán) - Tạm ngưng bán món
3. **Tiếp tục** (hiện khi đã tạm ngưng) - Tiếp tục bán món
4. **Nguyên liệu** - Quản lý nguyên liệu món ăn
5. **Xóa** - Xóa món (chỉ được phép khi đã tạm ngưng)

## Luồng sử dụng

### Tạm ngưng món ăn:
1. Tìm món đang bán (trạng thái "Đang bán")
2. Click nút **"Tạm ngưng"** (màu vàng)
3. Món chuyển sang trạng thái "Tạm ngưng"
4. Khách hàng không còn thấy món này trong menu

### Tiếp tục bán món:
1. Tìm món đã tạm ngưng (trạng thái "Tạm ngưng")
2. Click nút **"Tiếp tục"**
3. Món chuyển sang trạng thái "Đang bán"
4. Khách hàng lại thấy món này trong menu

### Xóa món ăn:
1. **Bước 1**: Tạm ngưng món (nếu đang bán)
2. **Bước 2**: Click nút **"Xóa"**
3. **Bước 3**: Xác nhận trong dialog
4. Món bị xóa vĩnh viễn khỏi hệ thống

**Lưu ý:** Không thể xóa món đang bán. Phải tạm ngưng trước.

## Files đã sửa

### Frontend:
- `src/Frontend/selfrestaurant-admin-web/src/pages/AdminConsolePage.tsx`
  - Thêm constant `DELETE_DISH_REQUIRES_SUSPENDED_MESSAGE`
  - Sửa function `removeDish()` - kiểm tra `available` thay vì `isActive`
  - Sửa UI danh sách món - thay nút "Vô hiệu" bằng "Tạm ngưng"/"Tiếp tục"
  - Thêm dialog xác nhận khi xóa

### Build:
- Đã rebuild admin frontend: `dist/` folder updated

## Cách test

1. **Restart backend services** (Visual Studio: Shift+F5 → F5)
2. **Hard refresh admin** (Ctrl+Shift+R)
3. Vào Admin → Món ăn
4. Test các trường hợp:
   - ✅ Tạm ngưng món đang bán
   - ✅ Tiếp tục món đã tạm ngưng
   - ✅ Thử xóa món đang bán → Thấy lỗi "Vui lòng tạm ngưng món ăn trước khi xóa"
   - ✅ Tạm ngưng món → Xóa thành công
   - ✅ Kiểm tra customer menu không hiển thị món đã tạm ngưng

## Lợi ích

1. **Đơn giản hóa**: Chỉ 1 trạng thái thay vì 2
2. **Rõ ràng hơn**: "Tạm ngưng/Tiếp tục" dễ hiểu hơn "Vô hiệu"
3. **An toàn hơn**: Phải tạm ngưng trước khi xóa → tránh xóa nhầm món đang bán
4. **UX tốt hơn**: Nút có màu sắc phân biệt (vàng = cảnh báo, xám = bình thường)

## Tương thích ngược

- ⚠️ Các món có `isActive = false` trong database vẫn hoạt động bình thường
- ⚠️ Logic backend không thay đổi, chỉ thay đổi UI frontend
- ⚠️ Nếu cần, có thể chạy script SQL để sync `isActive` với `available`

---

**Hoàn thành bởi Kiro AI - 10/05/2026**
