# VietTable — cloud cost / service-count plan

> Superseded cho deployment target từ 05/10/2026: user chọn Vercel + Supabase, không Customer Web, không Railway deployment. Xem [VERCEL_SUPABASE_ARCHITECTURE.md](VERCEL_SUPABASE_ARCHITECTURE.md). Inventory/chi phí Railway bên dưới giữ như phương án lịch sử, không phải yêu cầu provision.

Kiểm tra 05/10/2026. Không provision hoặc deploy Railway compute/Redis/worker trong preparation phase. Không upgrade, không nhập billing. Ngày báo cáo dự kiến tháng 11/2026; kiểm tra lại giá/limits trước khi quyết định.

## Inventory chính xác

| Thành phần | Railway service count | Lifetime khi nghiệm thu/demo |
|---|---:|---|
| Gateway | 1 | Persistent, Session/SignalR |
| Catalog | 1 | API + TableAutoReleaseService |
| Orders | 1 | API + PaymentCompletedConsumerService (HTTP outbox polling) |
| Customers | 1 | API + OrderReadyConsumerService |
| Identity | 1 | API |
| Billing | 1 | API + PaymentReconciliationWorker |
| Redis/Valkey | 1 nếu đặt trên Railway | Persistent; AOF + persistent volume + noeviction |
| Managed RabbitMQ ngoài Railway | 0 | Persistent broker AMQPS |
| Supabase database + Storage | 0 | Provider riêng; lưu schema/data/media |
| Bốn worker riêng | **0 thêm trong kiến trúc hiện tại** | Hosted services chạy trong bốn owner processes trên |

Tổng: **7 Railway services** nếu Redis ở Railway; **6** nếu cache ngoài Railway. Nếu tự host RabbitMQ ở Railway thì thêm 1 thành **8**, nhưng không thay thế yêu cầu managed RabbitMQ mà chưa có quyết định. Không cần thêm service cho static frontend assets vì Gateway Dockerfile đã build/embed bốn web apps; Vercel triển khai độc lập sau backend acceptance.

## Plan compatibility

| Plan | Services/project | Kết luận cho 6–7 service |
|---|---:|---|
| Trial | 5 | Không đủ inventory trong một project |
| Free sau Trial | 3 | Không đủ inventory trong một project |
| Hobby | 50 | Đủ service count, chưa chứng minh ngân sách/performance |

Trial có $5 credit, hết sau 30 ngày hoặc khi dùng hết; Free hiện có $1 credit/tháng. Hobby tối thiểu $5/tháng với $5 included usage; vượt included usage vẫn tính thêm. Không dự báo tổng giá khi chưa đo CPU/RAM/egress/storage. Nguồn chính thức: [Railway pricing](https://railway.com/pricing), [Trial](https://docs.railway.com/pricing/free-trial), [pricing FAQ](https://docs.railway.com/pricing/faqs).

Không gom DbContexts/controllers/services thành monolith chỉ để né giới hạn. Có thể về mặt kỹ thuật chạy nhiều owner process trong một container với loopback routing nhưng cần supervisor/SIGTERM/readiness/independent failure handling và mất isolation/scale; **không chuẩn bị hoặc nghiệm thu phương án này**. Chia nhiều project cũng không tự giải quyết private networking, credit và security; không dùng như workaround chưa audit.

## Timeline đề xuất — không tự thực hiện

**Bây giờ:** giữ Railway project rỗng; dùng Supabase Free staging và Docker disposable tại local. Không cho rằng giữ project rỗng sẽ kéo dài thời hạn Trial: đồng hồ 30 ngày theo account vẫn có thể chạy. Không tạo lại account để lấy Trial mới.

**Trước báo cáo:** user quyết định ngân sách/plan; kiểm tra lại limits. Nếu muốn giữ nguyên 6 APIs + Redis trong một private project, Hobby là candidate về service count. Rehearsal ngắn, đo usage rồi quyết định thời gian chạy; không cam kết $5 đủ toàn bộ architecture. Provision managed AMQPS qua account được user cho phép. Chạy migration/role/storage/network/session/SignalR/restart acceptance trước Android staging cutover.

**Trong báo cáo:** chạy toàn bộ sáu API + cache + broker suốt cửa sổ demo/rehearsal; hosted workers không được sleep/serverless vì payment reconciliation và outbox cần lifetime liên tục. Giữ local Samsung demo làm phương án dự phòng. Không bắt buộc cloud runtime chạy 24/7 từ tháng 10.

**Sau báo cáo/rehearsal:** hoàn tất/reconcile pending payments/outbox, backup cần thiết; gỡ active deployments bằng thao tác Remove deployment của từng service, tắt trigger auto-deploy để không tự chạy lại. Xác minh không còn active deployment và usage không tăng vì compute. Không xóa DB hoặc persistent volume để dừng compute. Volume/storage/managed broker/subscription có thể vẫn tính phí ngay cả khi compute dừng; kiểm tra riêng, chỉ xóa material data sau approval/backup. Cancel/downgrade plan là thao tác user quyết định, không tự động.

## Tránh accidental billing

- Chưa có quyền dùng Trial compute ở lượt này; không deploy.
- Không nhập thẻ/upgrade/add-on tự động.
- Khi user cho phép sau này: đặt usage alerts/hard limit phù hợp, không coi alert là spending cap. [Cost control](https://docs.railway.com/pricing/cost-control).
- Một public Gateway domain; downstream private. Dùng private transfer khi có thể; đo egress Supabase/broker riêng.
- Không bật Railway Serverless cho owner processes có hosted workers chỉ để giảm giá.
- Mỗi rehearsal có giờ bắt đầu/kết thúc và owner chịu trách nhiệm kiểm tra deployments/usage sau khi dừng.

Các tài nguyên nghiệp vụ không cần cloud compute 24/7 trong giai đoạn chuẩn bị đồ án, nhưng DB/media/key ring phải được bảo toàn giữa các buổi. Supabase Free cũng có limits/idle pause; kiểm tra sức khỏe và đánh thức project trước rehearsal, không hứa uptime production.
