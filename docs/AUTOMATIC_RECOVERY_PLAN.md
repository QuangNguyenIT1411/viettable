# Automatic durable recovery — plan only (10/10/2026)

**UNVERIFIED / NOT ACTIVATED.** Manual recovery và pending work qua redeploy đã có evidence
trong `CLOUD_REALTIME_RECOVERY_ACCEPTANCE.md`. Task này không bật extension, tạo job/Vault secret,
đổi deployment protection hoặc gọi recovery để nghiệm thu automatic.

## Đề xuất không cần laptop

Supabase **pg_cron → pg_net HTTPS POST → CloudHost `/api/internal/recovery`**.
`pg_cron` gọi một function quản trị nhỏ; `pg_net` thực hiện request async sau transaction commit.
Vault lưu credential mã hóa; SQL definition/schedule chỉ nhắc **tên secret**, không literal credential.
Đây là inference áp dụng capability chính thức, không phải job đã nghiệm thu trên project.
[Supabase Cron](https://supabase.com/docs/guides/cron),
[Scheduling + Vault guidance](https://supabase.com/docs/guides/functions/schedule-functions),
[pg_net](https://supabase.com/docs/guides/database/extensions/pg_net).

### Auth và isolation

- Header `Authorization: Bearer <Vault recovery credential>` phải khớp **CRON_SECRET riêng** của Preview.
  Không reuse customer/staff JWT, signing key hoặc Storage service key.
- Endpoint vẫn 401 cho anonymous/customer, 503 nếu chưa cấu hình; không mở route public bypass auth.
- SECURITY DEFINER function có fixed search_path, fully qualified objects, revoke PUBLIC/anon/authenticated
  execute; role scheduler giới hạn tối thiểu. Runtime năm domain owners không đọc Vault/cron/net queues.
- Vault credential được nhập bằng Dashboard/user hoặc parameterized protected provisioner,
  không paste vào SQL Editor literal, source, job command, CLI argv, report hoặc log.
- Chỉ đích HTTPS staging allowlisted, không follow redirect để forward Authorization sang host lạ.
- Headers phải tồn tại tạm trong pg_net queue để gửi request: không đồng nghĩa secret chưa bao giờ plaintext
  trong memory/database worker. Restrict access queue/decrypted view; không dump queue/history request headers.
- Audit job results chỉ request ID/time/status code/count, không response credential/cookie/full request.

### Ràng buộc Preview Protection

Supabase worker không có cookie Vercel browser hoặc development CLI OIDC. Preview hiện được bảo vệ:
`Authorization: Bearer CRON_SECRET` **không tự bypass Vercel Deployment Protection**.
Không disable protection và không lấy development token ngắn hạn làm unattended secret.

Nếu duyệt automatic Preview pilot, option là **dedicated Automation Bypass** lưu trong Vault,
gửi thêm header `x-vercel-protection-bypass`, tuyệt đối không query-string URL.
Capability hiện có trên mọi plan, nhưng credential bypass áp dụng mọi deployment trong project,
không riêng recovery path/Preview; cần user duyệt security scope, lifecycle/rotation và rollback trước.
Không tạo credential trong task này. Application CRON_SECRET vẫn bắt buộc sau platform bypass.
[Vercel Automation Bypass](https://vercel.com/docs/deployment-protection/methods-to-bypass-deployment-protection/protection-bypass-automation).

Alias staging ổn định cần trỏ tới **Preview được chọn**, không Production placeholder hiện tại.
Không auto-follow “latest” nếu deployment chưa nghiệm thu, không hardcode generated URL vào source.
Sau rotation bypass secret cần revalidate/redeploy theo behavior provider; OIDC/browser access của agent
không chứng minh scheduler access.

## Tần suất, bounded work và chi phí

Đề xuất pilot **10 phút/lần** trong thời gian demo đã duyệt: 144 requests/ngày, khoảng 4.320/tháng
nếu chạy cả tháng; đây là count, **không dự toán miễn phí tuyệt đối**. Có thể chỉ enable trong khung
giờ diễn tập để giảm cold-start/DB usage. Không activate pilot lúc này.

Cloud endpoint hiện có deadline 55 giây, durable coordinator leases/bounded recovery; success 200
`processed`, overlap 202 `already_running`. Không retry immediate loop, không insert business success giả.
Timeout/500 chỉ retry ở tick sau; inspect canonical state/pending work vì partial commit vẫn có thể xảy ra.
`pg_net` network timeout nên đủ cold-start + work (khoảng 90 giây), không dùng default timeout ngắn;
ghi nhận HTTP response async thay vì coi cron function dispatch thành công là recovery thành công.

Không cần paid extension/add-on theo design, nhưng phải verify **extension availability/config và quotas
trên project thật**; chưa có evidence cron/pg_net/Vault đã được enable và authorize. Supabase Free có
compute/network/disk và pause constraints; DB paused thì lịch không chạy. Vercel cold-start/requests,
memory/CPU và private-backplane WSS vẫn tiêu resource. Không hứa zero cost/24x7 SLA.

## Acceptance trước khi gọi automatic PASS

1. Read-only extension/role/queue permissions + account quota check; user approve bounded pilot và bypass scope.
2. User nhập Vault credentials ngoài source/log; inspect job SQL chỉ chứa function call/names.
3. Call controlled tick qua worker, prove Vercel protection + app auth cùng pass; bad token 401, wrong target blocked.
4. Reuse **isolated staging recovery harness**, pending committed work qua Preview redeploy; laptop verifier tắt.
5. Scheduled execution phải tự tạo canonical final state (invoice/session/table/outbox/notification), no duplicates;
   chỉ dùng timestamp/status/count audit, không token.
6. Test overlap 202, timeout/network outage tick sau retry đúng, rotated token fail closed, rollback unschedule.
7. Disable schedule sau rehearsal, báo quota/usage thực tế; separate broker-outage WSS acceptance vẫn UNVERIFIED.

Không có SQL schedule executable hoặc CRON_SECRET literal được thêm vào repository này.
