# Cloud session state — phase 1

Ngày 05/10/2026; chưa deploy Railway/Vercel, chưa provision Redis.

## Source thực tế

Gateway `Program.cs` dùng `AddDistributedMemoryCache` + `AddSession`, idle timeout 8 giờ, cookie HttpOnly/Essential/SameSite=Lax. Tên “distributed” không làm cache này bền vững: dữ liệu vẫn ở RAM một process.

`Infrastructure/SessionKeys.cs` lưu CustomerID, tên/username/phone/email/loyalty, CurrentTableID/BranchID/Number và SavedCustomerTableContexts; staff lưu EmployeeID, RoleCode/RoleID và BranchID. Controller web kiểm tra các khóa này để authorize. Đây **không chỉ là UI cache**. Restart Gateway làm mất login/context; nhiều replica có thể nhìn thấy session khác nhau. Không thay bằng ID/role do browser gửi lên.

| State | Owner / persistence hiện tại | Cloud requirement |
|---|---|---|
| Mobile access JWT | Identity ký; Gateway validate; Android lưu token an toàn | Cùng `MobileJwt:SigningKey`, issuer/audience giữa các service; key bền vững, rotation có kế hoạch |
| Refresh token / revocation / family | Identity `MobileRefreshTokens`, hash và UTC | DB bền vững; migration/cutover phải quyết định relogin; không đưa raw token vào dump/docs |
| Web session role/branch/customer | Gateway process RAM | Shared distributed session store trước scale/restart acceptance |
| Session cookie protection | ASP.NET DataProtection | Shared persistent key ring, application name ổn định, access/encryption phù hợp |
| Dining session / orders / payments | Owned database, REST canonical | Không nằm trong ASP.NET Session; đọc lại từ owning API |
| SignalR connections/groups | Gateway RAM, theo connection | Reconnect + REST resync; backplane/service trước multi-replica |
| Catalog/loyalty caches, rate-limit counters | Process memory | Có thể rebuild; rate-limit distributed strategy cần riêng nếu scale |
| Temp UI/table context | Web session | Rehydrate từ API/valid session, không tự tái tạo phiên nghiệp vụ |

## Quyết định next phase

Giữ local unchanged trong compatibility phase. Trước cloud web acceptance, dùng Redis-compatible `IDistributedCache` cho Session và shared persistent DataProtection keys (object/database/Redis provider đã kiểm chứng). Không chỉ mount key ring rồi kết luận Session đã bền vững. Sticky sessions đơn lẻ không giải quyết restart.

Chưa thêm Redis dependency/provisioning, chưa đổi cookie auth flow. Vercel webs cần cùng-origin Gateway proxy hoặc CORS/cookie credential/CSRF/SameSite/Secure design được nghiệm thu, không tự đổi cookie trong phase DB.

## Acceptance trước cutover

- [ ] Staff login → restart Gateway → session/role/branch vẫn đúng hoặc documented relogin policy được chấp thuận.
- [ ] Hai Gateway replicas đọc cùng session; non-staff không có quyền do session missing/spoof.
- [ ] Cookie còn giải mã được sau restart/key rotation.
- [ ] Mobile JWT refresh hoạt động sau API restart; không thay signing key mỗi restart.
- [ ] SignalR reconnect/rest resync qua replica đúng, không dùng event làm canonical state.

RAM session và key ring ephemeral vẫn là **cloud cutover blocker**, không phải lỗi schema PostgreSQL.
