# Evidence — đo khởi động lạnh Production mới (việc 3.1), 13/10/2026

Phạm vi: chỉ đọc. Gửi GET ẩn danh bằng `curl.exe` (không cookie/token), mỗi địa chỉ 1 lần theo thứ tự, rồi lặp lại lượt 2 ngay sau đó.
Không deploy, không ghi DB, không chạy vòng lặp. Chủ dự án duyệt việc 3.1 trong phiên này.

## Bối cảnh

- Domain: `https://viettable-beta.vercel.app` (tài khoản Vercel mới, team `viet-table`, project `viettable`).
- Deployment (`npx vercel@62.4.0 inspect viettable-beta.vercel.app --scope viet-table`, chỉ đọc):
  `dpl_3nKhUqkKf8zTVxavp3LpnqPJpSdb`, target `production`, Ready, tạo 12/10/2026 23:51:38 +07,
  build `λ services/api/container (102.17MB) [syd1]`. Inspect không ghi commit (deploy bằng CLI);
  commit `5a25ff5` là theo xác nhận của chủ dự án.
- Theo chủ dự án, không ai mở web/app từ khoảng 0h đêm 12→13/10, tức là để yên khoảng 15 giờ trước lần đo.
- Máy đo: Windows, mạng nhà tại Việt Nam, Wi-Fi/mạng không kiểm soát nhân tạo.

## Lệnh

```bash
curl.exe -s -o /dev/null --max-time 90 -w "HTTP %{http_code} | ket noi %{time_connect}s | TLS %{time_appconnect}s | byte dau %{time_starttransfer}s | tong %{time_total}s | %{size_download} B\n" "https://viettable-beta.vercel.app<path>"
```

## Kết quả (giờ UTC; giờ VN = UTC+7)

| Lượt | Giờ bắt đầu | Địa chỉ | HTTP | Kết nối (s) | TLS xong (s) | Byte đầu (s) | Tổng (s) | Byte |
|---|---|---|---|---|---|---|---|---|
| 1 | 08:22:00Z | `/` | 200 | 0,166 | 0,271 | 7,739 | **7,739** | 769 |
| 1 | 08:22:08Z | `/healthz` | 200 | 0,066 | 0,135 | 7,330 | **7,330** | 15 |
| 1 | 08:22:15Z | `/api/mobile/v1/branches/1/menu` | 200 | 0,042 | 0,137 | 1,384 | 1,385 | 24 314 |
| 2 | 08:22:17Z | `/` | 200 | 0,245 | 0,340 | 0,867 | 0,867 | 769 |
| 2 | 08:22:18Z | `/healthz` | 200 | 0,041 | 0,134 | 7,255 | **7,255** | 15 |
| 2 | 08:22:25Z | `/api/mobile/v1/branches/1/menu` | 200 | 0,044 | 0,139 | 0,422 | 0,423 | 24 314 |

## So sánh với số đo trước

| Lần đo | Môi trường | Yêu cầu lạnh | Thời gian |
|---|---|---|---|
| Trước (`docs/FULL_SYSTEM_PERFORMANCE_AUDIT.md`) | Stable cũ `viettable-staging-hienkhanhvu981.vercel.app` (Preview, tài khoản cũ) | JS asset Chef qua container | **25,848 s** (lượt sau 1,712 s) |
| Trước (`RUN_2026-10-12.md`) | Stable cũ, từ Samsung | `branches` lạnh | 25,622 s và 23,840 s |
| **Hôm nay** | Production mới `viettable-beta`, `[syd1]` | `/` lần đầu sau ~15 giờ nghỉ | **7,739 s** (lượt sau 0,867 s) |

Lần khởi động lạnh hôm nay nhanh hơn khoảng 3,3 lần (25,8 s → 7,7 s). Hai lần đo không cùng điều kiện: khác tài khoản/project,
khác region/cấu hình, khác đường dẫn và khác máy đo. Vì vậy đây là so sánh tham khảo, không phải benchmark có kiểm soát (n=1).

## Nhận xét

1. Khởi động lạnh là khoảng 7,7 s, đo ở yêu cầu `/` đầu tiên. Sau khi máy chủ đã chạy, `/` mất 0,87 s và menu 0,42 s.
2. **Bất thường:** `/healthz` mất khoảng 7,3 s ở **cả hai lượt**, kể cả khi `/` đứng ngay trước đó chỉ mất 0,87 s.
   Như vậy không phải do khởi động lạnh. Phân tích code ở việc 3.2: handler `/healthz` chỉ trả `{"status":"ok"}`, không đụng DB.
   Nguyên nhân nhiều khả năng nằm ngoài code ứng dụng (định tuyến/instance phía Vercel). **Chưa xác minh.**
3. `/` trả 200 với 769 byte, khớp với trang chủ 3 nút (commit `c7241c5`) đã được deploy.
4. Lần đo này không gọi `/readyz` và `/api/mobile/v1/branches` (chủ dự án chọn 3 địa chỉ trên).
