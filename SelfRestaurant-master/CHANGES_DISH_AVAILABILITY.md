# CẬP NHẬT HIỂN THỊ MÓN TẠM NGƯNG

## Ngày: 10/05/2026

## Vấn đề

**Trước đây:**
- Khi chef/admin tạm ngưng món → Món biến mất khỏi menu
- Chef không thể bấm "Tiếp tục" vì món đã bị ẩn
- Customer không thấy món tạm ngưng trong menu

**Yêu cầu:**
- Chef tạm ngưng món → Món vẫn hiển thị để có thể bấm "Tiếp tục"
- Customer vẫn thấy món tạm ngưng nhưng không thể thêm vào giỏ, chỉ xem thông tin

## Giải pháp

### 1. Backend - Catalog API

**File:** `src/Services/SelfRestaurant.Catalog.Api/Controllers/CatalogController.cs`

**Thay đổi trong `GetMenu` endpoint:**

**Trước:**
```csharp
.Where(x => x.CategoryID == category.CategoryID && (x.Available ?? true))
// Chỉ lấy món Available = true

available = true,
// Luôn trả về available = true
```

**Sau:**
```csharp
.Where(x => x.CategoryID == category.CategoryID && (x.IsActive ?? true))
// Lấy TẤT CẢ món active (cả available và unavailable)

available = x.Available ?? true,
// Trả về giá trị available thực tế từ database
```

**Kết quả:**
- API trả về TẤT CẢ món (cả đang bán và tạm ngưng)
- Mỗi món có field `available: true/false` chính xác
- Frontend tự quyết định hiển thị như thế nào

### 2. Chef Frontend

**File:** `src/Frontend/selfrestaurant-chef-web/src/pages/DashboardPage.tsx`

**Tính năng có sẵn:**
- Tab "Thực đơn" hiển thị tất cả món
- Filter theo trạng thái: "Tất cả" / "Đang bán" / "Tạm ngưng"
- Mỗi món có badge hiển thị trạng thái
- Nút "Tạm ngưng" / "Tiếp tục" luôn hiển thị

**Cách hoạt động:**
```typescript
const filteredMenuDishes = useMemo(() => {
  return data.menu.dishes.filter((dish) => {
    const matchesStatus =
      dishStatusFilter === "ALL" ||
      (dishStatusFilter === "AVAILABLE" ? dish.available : !dish.available);
    return matchesSearch && matchesStatus && matchesSpecial;
  });
}, [data, dishSearch, dishSpecialFilter, dishStatusFilter]);
```

**Khi filter = "ALL":** Hiển thị TẤT CẢ món (cả available và unavailable)

### 3. Customer Frontend

**File:** `src/Frontend/selfrestaurant-customer-web/src/pages/MenuPage.tsx`

**Tính năng có sẵn (không cần sửa):**
- Hiển thị TẤT CẢ món trong menu
- Món tạm ngưng có:
  - Badge "Tạm hết" (màu xám)
  - Class `dish-card-unavailable` (làm mờ card)
  - Text "Món đang tạm hết hoặc ngừng bán"
  - Nút "Thêm" bị disable
  - Không thể tăng số lượng trong giỏ

**Code xử lý:**
```typescript
// Hiển thị badge
{!dish.available ? <span className="badge-unavailable">{t.unavailable}</span> : null}

// Hiển thị text cảnh báo
{!dish.available ? <div className="dish-unavailable-text">{t.unavailableHint}</div> : null}

// Disable nút thêm
<button type="button" className="btn-add" disabled={!dish.available} onClick={() => addDishToCart(dish.dishId)}>
  {dish.available ? t.add : t.unavailable}
</button>

// Kiểm tra khi thêm vào giỏ
if (!dish.available) {
  setToast({ type: "info", message: `Món "${dish.name}" hiện đang tạm hết.` });
  return;
}
```

## Luồng hoạt động mới

### Chef tạm ngưng món:

1. Chef vào tab **"Thực đơn"**
2. Tìm món đang bán
3. Click nút **"Tạm ngưng"** (màu vàng)
4. Món chuyển trạng thái → Badge "Tạm ngưng" (màu xám)
5. **Món vẫn hiển thị** trong danh sách
6. Nút đổi thành **"Tiếp tục"** (màu xanh)

### Chef tiếp tục bán món:

1. Chef vào tab **"Thực đơn"**
2. Tìm món đã tạm ngưng (hoặc filter "Tạm ngưng")
3. Click nút **"Tiếp tục"** (màu xanh)
4. Món chuyển trạng thái → Badge "Đang bán" (màu xanh)
5. Nút đổi thành **"Tạm ngưng"** (màu vàng)

### Customer xem món tạm ngưng:

1. Customer vào menu
2. Thấy món tạm ngưng với:
   - ✅ Hình ảnh món (bị làm mờ)
   - ✅ Tên món, giá, mô tả
   - ✅ Badge "Tạm hết"
   - ✅ Text "Món đang tạm hết hoặc ngừng bán"
   - ❌ Nút "Thêm" bị disable
3. Không thể thêm vào giỏ hàng
4. Nếu món đã có trong giỏ:
   - Hiển thị badge "Tạm hết" trong giỏ
   - Không thể tăng số lượng
   - Hiển thị cảnh báo khi submit
   - Phải xóa hoặc giảm món đó trước khi gửi bếp

## Lợi ích

### Cho Chef:
1. ✅ **Dễ quản lý**: Món tạm ngưng vẫn hiển thị, không bị "mất"
2. ✅ **Tiện lợi**: Có thể bật lại món bất cứ lúc nào
3. ✅ **Rõ ràng**: Badge màu sắc phân biệt trạng thái
4. ✅ **Filter linh hoạt**: Xem tất cả, chỉ đang bán, hoặc chỉ tạm ngưng

### Cho Customer:
1. ✅ **Minh bạch**: Vẫn thấy món tồn tại nhưng tạm hết
2. ✅ **Thông tin đầy đủ**: Xem được giá, mô tả, hình ảnh
3. ✅ **Tránh nhầm lẫn**: Rõ ràng món đang tạm hết, không phải bị xóa
4. ✅ **UX tốt**: Không thể thêm món tạm hết vào giỏ

### Cho Admin:
1. ✅ **Nhất quán**: Logic giống Chef
2. ✅ **Kiểm soát**: Tạm ngưng trước khi xóa
3. ✅ **An toàn**: Không xóa nhầm món đang bán

## Files đã sửa

### Backend:
- ✅ `src/Services/SelfRestaurant.Catalog.Api/Controllers/CatalogController.cs`
  - Sửa `GetMenu` endpoint
  - Lấy tất cả món (cả available và unavailable)
  - Trả về giá trị `available` chính xác

### Frontend:
- ✅ `src/Frontend/selfrestaurant-chef-web/src/pages/DashboardPage.tsx`
  - Đã có sẵn logic hiển thị tất cả món
  - Đã có filter theo trạng thái
  - Đã có nút Tạm ngưng/Tiếp tục

- ℹ️ `src/Frontend/selfrestaurant-customer-web/src/pages/MenuPage.tsx`
  - **Không cần sửa** - đã xử lý món unavailable hoàn hảo

## Cách test

### Test Chef:
1. Restart backend (Shift+F5 → F5)
2. Vào Chef app → Tab "Thực đơn"
3. Tạm ngưng 1 món → Món vẫn hiển thị với badge "Tạm ngưng"
4. Click "Tiếp tục" → Món chuyển về "Đang bán"
5. Test filter "Tạm ngưng" → Chỉ hiển thị món tạm ngưng
6. Test filter "Tất cả" → Hiển thị tất cả món

### Test Customer:
1. Hard refresh customer app (Ctrl+Shift+R)
2. Vào menu → Thấy món tạm ngưng với badge "Tạm hết"
3. Thử click "Thêm" → Nút bị disable
4. Xem thông tin món (giá, mô tả) → Hiển thị bình thường
5. Nếu món đã có trong giỏ → Hiển thị cảnh báo

### Test Admin:
1. Vào Admin → Món ăn
2. Tạm ngưng 1 món → Món vẫn hiển thị
3. Click "Tiếp tục" → Món bán lại
4. Thử xóa món đang bán → Báo lỗi "Vui lòng tạm ngưng trước"
5. Tạm ngưng → Xóa → Thành công

## Tương thích

- ✅ Tương thích ngược với database hiện tại
- ✅ Không ảnh hưởng đến đơn hàng đang xử lý
- ✅ Không cần migration database
- ✅ Chỉ cần restart backend để áp dụng

---

**Hoàn thành bởi Kiro AI - 10/05/2026**
