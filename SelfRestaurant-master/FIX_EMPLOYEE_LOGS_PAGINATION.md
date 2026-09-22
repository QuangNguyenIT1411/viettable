# Sửa lỗi: Nhật ký nhân viên và phân trang

## Vấn đề đã sửa

### 1. ✅ Nhật ký hoạt động chef bị dùng chung
**Vấn đề**: Khi bấm vào "Nhật ký" của Chef A, sau đó bấm vào "Nhật ký" của Chef B, vẫn thấy nhật ký của Chef A (state không được reset).

**Nguyên nhân**: State `history` không được reset khi chuyển sang employee khác.

**Giải pháp**:
- Thêm `setHistory(null)` trong `loadPage()` để reset state
- Thêm `useEffect` để reset pagination khi `employeeId` thay đổi
- Đảm bảo `employeeIdValue` có trong dependency array của `useEffect`

### 2. ✅ Thêm phân trang cho nhật ký
**Vấn đề**: 
- Nhật ký hoạt động của chef có phân trang nhưng không có nút bấm
- Nhật ký thu ngân có phân trang nhưng không có nút bấm

**Giải pháp**:
- Thêm state `activityPage` và `cookingPage`
- Thêm nút "← Trang trước" và "Trang sau →"
- Disable nút khi ở trang đầu/cuối
- Reset pagination về trang 1 khi chuyển employee

### 3. ✅ Thêm cột "Ghi chú" vào nhật ký nấu ăn
**Đã hoàn thành ở commit trước**:
- Hiển thị `[HỦY]` lý do hủy món
- Hiển thị `[BEP]` ghi chú của bếp

## Chi tiết thay đổi

### Frontend Changes

#### File: `src/Frontend/selfrestaurant-admin-web/src/pages/employees/EmployeesModulePage.tsx`

**1. Thêm state cho pagination:**
```typescript
const [activityPage, setActivityPage] = useState(1);
const [cookingPage, setCookingPage] = useState(1);
```

**2. Reset history khi load:**
```typescript
async function loadPage() {
  setLoading(true);
  setError(null);
  setHistory(null); // ← Reset để tránh hiển thị data cũ
  // ...
}
```

**3. Reset pagination khi chuyển employee:**
```typescript
useEffect(() => {
  setActivityPage(1);
  setCookingPage(1);
}, [employeeIdValue]);
```

**4. Gọi API với pagination:**
```typescript
setHistory(await adminApi.getEmployeeHistory(employeeIdValue, activityPage, 50, 90));
```

**5. Thêm pagination controls:**
```tsx
{history.chefActivityLogs.totalPages > 1 && (
  <div className="pagination-controls">
    <button 
      className="ghost" 
      disabled={activityPage <= 1}
      onClick={() => setActivityPage(p => Math.max(1, p - 1))}
    >
      ← Trang trước
    </button>
    <span>
      Trang {history.chefActivityLogs.page}/{history.chefActivityLogs.totalPages} 
      ({history.chefActivityLogs.totalItems} nhật ký)
    </span>
    <button 
      className="ghost"
      disabled={activityPage >= history.chefActivityLogs.totalPages}
      onClick={() => setActivityPage(p => p + 1)}
    >
      Trang sau →
    </button>
  </div>
)}
```

## Giao diện mới

### Nhật ký hoạt động (Chef)
```
┌────────────────────────────────────────────────────────┐
│ Nhật ký hoạt động (Tạm ngưng/Tiếp tục món)            │
├────────────────────────────────────────────────────────┤
│ Thời gian | Hành động | Món ăn | Trạng thái           │
│ 10:30     | Tạm ngưng | Phở bò | ⚫ Tạm ngưng         │
│ 11:00     | Tiếp tục  | Phở bò | 🟢 Đang bán         │
└────────────────────────────────────────────────────────┘
[← Trang trước]  Trang 1/3 (125 nhật ký)  [Trang sau →]
```

### Nhật ký thu ngân (Cashier)
```
┌────────────────────────────────────────────────────────┐
│ Nhật ký thu ngân                                       │
├────────────────────────────────────────────────────────┤
│ Mã HĐ | Thời gian | Bàn | Khách | Tổng tiền          │
│ BILL-01 | 9:00 | Bàn 5 | Nguyễn A | 200,000đ         │
└────────────────────────────────────────────────────────┘
[← Trang trước]  Trang 1/5 (230 hóa đơn)  [Trang sau →]
```

## Testing

### Test Case 1: Chuyển đổi giữa các chef
1. Vào trang Nhân viên
2. Click "Nhật ký" của Chef A
3. Xem nhật ký của Chef A
4. Quay lại danh sách
5. Click "Nhật ký" của Chef B
6. **Kết quả mong đợi**: Thấy nhật ký của Chef B (không phải Chef A)

### Test Case 2: Phân trang
1. Vào nhật ký của một chef có nhiều hơn 50 log
2. Thấy nút "Trang sau →"
3. Click "Trang sau →"
4. **Kết quả mong đợi**: 
   - Hiển thị trang 2
   - Nút "← Trang trước" được enable
   - Nếu là trang cuối, nút "Trang sau →" bị disable

### Test Case 3: Reset pagination khi chuyển employee
1. Vào nhật ký Chef A, chuyển sang trang 2
2. Quay lại danh sách
3. Vào nhật ký Chef B
4. **Kết quả mong đợi**: Hiển thị trang 1 của Chef B (không phải trang 2)

## Build Status
- ✅ Frontend: Build thành công
- ✅ Backend: Không thay đổi (đã có API phân trang từ trước)

## Lưu ý

### Pagination hiện tại:
- **Chef activity logs**: Có phân trang với nút điều hướng ✅
- **Chef cooking history**: Chưa có phân trang (hiển thị tối đa 50 đơn) ⚠️
- **Cashier history**: Có phân trang với nút điều hướng ✅

### Để thêm phân trang cho cooking history:
Cần thay đổi backend API `GetChefHistoryAsync()` để hỗ trợ phân trang thay vì chỉ `take`.
Hiện tại cooking history chỉ hiển thị 50 đơn gần nhất, đủ cho hầu hết trường hợp.
