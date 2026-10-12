# VIETTABLE — BIÊN BẢN NGHIỆM THU HIỆN TRẠNG & BÀN GIAO CÔNG VIỆC

> **Ngày lập:** 12/10/2026 (giờ Việt Nam)  
> **Kết luận nghiệm thu:** **CONDITIONAL / PARTIAL ACCEPTANCE** — **CHƯA ĐỦ ĐIỀU KIỆN BÀN GIAO TOÀN BỘ / RELEASE**  
> **Trạng thái công việc theo yêu cầu chủ dự án:** **DỪNG PHÁT TRIỂN VÀ DỪNG THAO TÁC MỚI; BÀN GIAO HIỆN TRẠNG**  
> **Mục đích:** Người tiếp nhận có thể tiếp tục từ trạng thái hiện có mà không phải suy đoán kết quả kiểm thử, dữ liệu hoặc thay đổi chưa triển khai.

## 0. Phạm vi và độ tin cậy của biên bản

Biên bản được **tổng hợp từ báo cáo nghiệm thu và các báo cáo kỹ thuật do chủ dự án cung cấp trong cuộc trao đổi**, không phải kết quả truy cập trực tiếp repository, Windows, Samsung, Vercel hay cơ sở dữ liệu trong phiên lập tài liệu này. **Không chạy lại test, không sửa code, không deploy, không commit/push, không tác động dữ liệu và không dừng tiến trình trên máy người dùng.**

Các đường dẫn `D:\...` là đường dẫn **trên máy Windows của chủ dự án**, chưa được xác minh độc lập trong phiên tạo tài liệu. Người nhận bàn giao cần mở những báo cáo gốc, đối chiếu mã nguồn và dấu thời gian trước khi sử dụng như bằng chứng chính thức.

**Quy ước kết quả**:

- **PASS (mới):** đã có kết quả chạy trong lượt nghiệm thu gần nhất, theo báo cáo.
- **PASS HISTORICAL:** có bằng chứng lượt trước, **không đồng nghĩa đã kiểm tra lại trong lượt nghiệm thu cuối**.
- **BLOCKED:** bị chặn bởi quyền truy cập, thiết bị, môi trường hoặc rủi ro thao tác.
- **NOT TESTED:** chưa được kiểm tra.
- **FAIL:** test đã chạy và xác nhận sai; không ghi nhận FAIL mới trong báo cáo cuối, **không có nghĩa là không tồn tại lỗi**.

## 1. Quyết định nghiệm thu

**Quyết định: CHẤP NHẬN MỘT PHẦN / BÀN GIAO CÔNG VIỆC DỞ DANG.** Không ký nghiệm thu toàn phần; không chuyển candidate thành stable; chưa chứng nhận an toàn bảo mật toàn hệ thống; chưa chứng nhận hiệu năng hoặc E2E trên Samsung đã đạt SLA.

| Trạng thái | Số ca | Ý nghĩa |
|---|---:|---|
| PASS mới | 23 | Có kết quả trong đợt nghiệm thu cuối |
| PASS HISTORICAL | 30 | Có bằng chứng lịch sử, chưa chạy lại |
| BLOCKED | 4 | Chưa thể xác minh vì rào cản |
| NOT TESTED | 7 | Chưa thực hiện |
| FAIL xác nhận trong lượt cuối | 0 | Không suy ra 100% chức năng đúng |
| **Tổng** | **64** | Ma trận nguồn: `FULL_ACCEPTANCE_MATRIX.csv` |

**Điều kiện cần để xem xét nghiệm thu toàn phần trong tương lai:** (1) thống nhất tiêu chí và môi trường nghiệm thu; (2) xử lý/đánh giá rào cản truy cập Android Cloud; (3) xác minh cô lập payload realtime giữa hai customer; (4) kiểm chứng recovery an toàn theo fixture; (5) nghiệm thu Order/Invoice History có dữ liệu trên Samsung và các luồng ghi B0–B7 trong QA cô lập; (6) xác nhận các lỗi P0/P1 đã được giải quyết/được chấp thuận rủi ro; (7) có bằng chứng E2E, hiệu năng và release tương ứng. Không tự động đổi trạng thái khi chưa đáp ứng.

## 2. Thông tin hệ thống và phạm vi bàn giao

- **Repository/project máy chủ sở hữu:** `D:\Quang\VietTable`.
- **Các thành phần được nhắc trong báo cáo:** Android Customer; Chef Web; Cashier Web; Admin Web; API Gateway; Identity, Catalog, Orders, Customers, Billing/CloudHost; dữ liệu SQL; realtime SignalR/WebSocket; Vercel candidate/preview/stable; các phép thử và tài liệu B0–B7.
- **Candidate Vercel:** `dpl_7MxW7oBh8H1DCYmPSefcYX7Hf8fE` — **Ready / Preview** theo báo cáo; **không phải xác nhận đủ điều kiện phát hành**.
- **Stable alias:** giữ nguyên, **không đổi** trong lượt nghiệm thu.
- **Commit có liên quan từ giai đoạn tối ưu trước:** `103bebb` — `fix: unblock protected Q1 emulator QA and reduce Home rerenders`. **Không mặc định đây là HEAD hiện tại**; người tiếp nhận phải chạy `git status` / `git log` để đối chiếu.
- **Working tree:** báo cáo nêu có các thay đổi sẵn từ trước (hai file `.idea` và routing/Gateway), **không thuộc commit nêu trên**; không discard/reset/commit thay người đang làm.
- **Bảo vệ dữ liệu:** không thay session/đơn/hóa đơn thật, không chạy cleanup toàn cục, không gửi thanh toán thật.

## 3. Kết quả theo phân hệ

| Phân hệ | Bằng chứng đã báo cáo | Điểm chưa chốt / giới hạn | Trạng thái bàn giao |
|---|---|---|---|
| Android Customer | Samsung SM-A528B vào Home, tải dữ liệu Bình Thạnh và khôi phục thẻ **Bàn 2**; build Android Debug/CloudPreview PASS | Chưa nghiệm thu Order/Invoice History **có dữ liệu** trên Samsung; chưa chạy lại toàn bộ B0–B7; UX/hiệu năng session còn cần phân tích | **PARTIAL** |
| Chef Web | Có bằng chứng UI Quận 1 từ lượt trước | Không đăng nhập/chạy lại trong nghiệm thu cuối | **PASS HISTORICAL** |
| Cashier Web | Browser thật hiển thị **20 bàn trống** Quận 1; payment fixture QA từng thu tiền; History mở được | Danh sách hóa đơn `legacy = 0` **không phải** Customer Invoice History; cần đối chiếu theo fixture/role | **PARTIAL / HISTORICAL** |
| Admin Web | Có chứng cứ giao diện/luồng Quận 1 lịch sử | Không chạy lại các luồng quản trị trong lượt cuối | **PASS HISTORICAL** |
| Backend / Gateway / Cloud | Health/readiness HTTP 200; candidate Ready/Preview; 12/12 request lịch sử đơn/hóa đơn trên candidate và stable đúng theo báo cáo | HTTP 200 không chứng minh toàn bộ nghiệp vụ; APK CloudPreview thông thường bị Vercel 401 trước Identity | **PARTIAL** |
| Realtime | 17/17 quyền subscribe/reconnect PASS; event khác instance có bằng chứng lịch sử | Chưa có bài test âm tính **event payload cross-customer** với hai tài khoản đồng thời | **PARTIAL / SECURITY UNVERIFIED** |
| Recovery | Đã nhận diện rủi ro endpoint có cleanup toàn cục | Không thử recovery theo một fixture do chưa an toàn | **BLOCKED** |
| Release | Candidate tồn tại, stable alias không đổi | Chưa đạt điều kiện chuyển candidate → stable | **NOT READY** |

**Cần lưu ý:** Thẻ **Bàn 2** trên Samsung tại Bình Thạnh và **Bàn 37 / Session #20** trong snapshot owner-backed không được tự coi là cùng một phiên/đối tượng. Phải xác minh qua ID và môi trường gốc trước khi đối chiếu.

## 4. Bằng chứng build và test

Theo báo cáo cuối:

| Nhóm kiểm thử | Kết quả đã báo cáo | Giới hạn |
|---|---|---|
| Android Debug & CloudPreview | Build PASS | Không chứng minh CloudPreview APK đi qua Vercel Protection |
| Android unit tests | **54/54 PASS** | Unit test không thay thế E2E |
| CloudHost tests | **75/75 PASS** | Sau khi khôi phục dependency thiếu trong SDK container |
| Backend contract tests | **143 PASS, 1 skipped** | Có 1 skipped, cần xem nguyên nhân |
| Web frontends | **4/4 build PASS** | Build thành công không thay thế browser E2E |
| Instrumentation | Không chạy trong nghiệm thu cuối | Không tự ghi PASS |
| B0–B7 flow mới | Chưa chạy toàn bộ | Không tạo giao dịch mới trên dữ liệu thật |

Các lệnh thực tế, exit code, bằng chứng và thời điểm phải lấy từ `docs/acceptance/evidence/RUN_2026-10-12.md` và các log gốc, không suy diễn từ bảng trên.

## 5. Hiệu năng: snapshot, KHÔNG phải chứng chỉ SLA

**Samsung SM-A528B / Android 14**, ba lần cold launch trong nghiệm thu cuối:

| Chỉ số | Giá trị | Ghi chú |
|---|---:|---|
| Home có dữ liệu p50 | **5,81 giây** | Thời gian UIAutomator quan sát thấy |
| Home có dữ liệu p95 | **8,41 giây** | Chỉ 3 mẫu, p95 thiếu độ tin cậy thống kê |
| Thẻ phiên p50 | **8,41 giây** | Quan sát UI, có độ trễ lấy mẫu |
| Thẻ phiên p95 | **29,75 giây** | Cần điều tra nguyên nhân, không suy ra chính xác thời gian render |
| Candidate Order History khi process mới | **11,16 giây** | Một lần đọc, không đại diện phân phối |

Dữ liệu tham khảo các lượt trước: Home Samsung ~**5,094 giây** (baseline khác điều kiện); `setupViews()` Emulator ~**355 → 40 ms** (đo logic dựng view, **không phải** tốc độ mở Home Samsung); Cashier browser bàn sử dụng được p50 **2,10 s**, p95 **5,38 s** và payment UI p50 **2,95 s**, p95 **6,28 s** (3 lượt đo). **Không so sánh các chỉ số khác bản chất và môi trường để tuyên bố đã tối ưu thành công.**

## 6. Dữ liệu / trạng thái cần giữ nguyên

**Snapshot owner-backed gần nhất được báo cáo:**

| Đối tượng | Trạng thái gần nhất |
|---|---|
| Dining Session **#20** | `PAYMENT_REQUESTED` |
| Order/item **#38** | `READY` |
| **Bàn 37** | Đang sử dụng |
| Payment/Invoice liên quan snapshot | **Chưa có** theo bằng chứng đã báo cáo |

Một baseline cũ từng ghi `ACTIVE`/`CONFIRMED`, **đã lỗi thời** và không được sử dụng thay snapshot gần nhất. Người nhận phải xác minh trạng thái theo ID, branch và timestamp trước khi thực hiện bất kỳ hành động liên quan.

**Các thao tác cấm nếu chưa có phê duyệt rõ ràng của chủ dự án:** logout Samsung đang giữ phiên; clear data/uninstall làm mất local session; thanh toán/đóng/hủy Session #20; chuyển trạng thái Order #38; giải phóng Bàn 37; cleanup database/recovery toàn cục; tạo payment thật; xóa/đổi dữ liệu các tài khoản khác.

## 7. Danh sách tồn đọng để người kế nhiệm xử lý

> Mức độ ưu tiên dưới đây là **đề xuất quản trị rủi ro**; không được xem là kết luận đã phát hiện lỗ hổng hoặc FAIL nếu chưa có phép thử xác nhận.

| ID | Ưu tiên đề xuất | Tồn đọng / hiện trạng | Điều kiện đóng | Giới hạn an toàn |
|---|---|---|---|---|
| VT-HO-01 | P0 — cản release | **APK Android CloudPreview thông thường gặp Vercel Authentication 401** trước VietTable Identity. Bridge loopback chỉ phục vụ Emulator QA và đã tắt sau lượt trước | Giải pháp kết nối Android vào đúng môi trường có xác thực, kiểm thử trên thiết bị; không lộ secret; quyết định release được phê duyệt | Không tự tắt Vercel Protection, không nhúng OIDC/bypass token vào APK |
| VT-HO-02 | P0 — yêu cầu bằng chứng an ninh | **Chưa chứng minh event payload realtime được cô lập giữa hai customer** dù 17/17 check subscribe/reconnect PASS | Negative E2E test hai QA customers/fixtures đồng thời, chứng minh B không nhận dữ liệu riêng của A; có log đã ẩn dữ liệu | Chỉ dùng QA cô lập, không kiểm tra qua dữ liệu khách hàng thật |
| VT-HO-03 | P1 | Recovery endpoint hiện tại có **cleanup toàn cục**, nên không thể test an toàn theo fixture | Có đường recovery theo phạm vi hẹp hoặc môi trường cô lập; test có rollback/cleanup kiểm soát | **Không chạy endpoint cleanup toàn cục** |
| VT-HO-04 | P1 | Samsung Order/Invoice History **có dữ liệu** chưa được xác minh; B0–B7 ghi dữ liệu chưa nghiệm thu lại | E2E evidence mới theo yêu cầu, dùng các QA fixtures độc lập; kiểm tra dữ liệu trước/sau | Không dùng Session #20 / Bàn 37 / payment thật |
| VT-HO-05 | P1/P2 tùy SLA | Thẻ phiên Samsung có thời gian quan sát cao; chỉ số p95 3 mẫu chưa ổn định | Instrumentation/tracing và mẫu đo đủ lớn, điều kiện trước/sau thống nhất, ngưỡng được chủ dự án chấp thuận | Không kết luận root cause khi chưa có waterfall/tracing |
| VT-HO-06 | P1/P2 tùy phạm vi | Chef/Admin/Cashier và một số luồng Cloud chủ yếu có bằng chứng lịch sử | Kiểm thử lại các kịch bản bắt buộc trên candidate/stable đúng phạm vi và phiên bản | Tránh tái thanh toán fixture đã hoàn tất |
| VT-HO-07 | Phụ thuộc | Xác nhận `git status`, phiên bản deploy, thay đổi `.idea` và routing/Gateway chưa commit | Ghi inventory read-only, chốt baseline với chủ dự án | Không reset/stash/discard thay đổi của người khác |

**Điều không được hiểu sai:** “0 FAIL mới” không có nghĩa mọi bài test đều PASS; “17/17 subscription PASS” không đồng nghĩa event payload không rò; “payment QA đã thu” không đồng nghĩa được phép tạo thêm payment hay rằng Customer Invoice History trên Samsung đã PASS.

## 8. Lệnh DỪNG và nguyên tắc giữ nguyên hiện trạng

Từ thời điểm bàn giao, theo yêu cầu chủ dự án:

1. **DỪNG** phát triển tính năng mới, sửa code, tối ưu hiệu năng và các vòng kiểm thử mới do nhóm hiện tại chủ động khởi chạy.
2. **KHÔNG** deploy Candidate/Production, không đổi stable alias, không push, merge, reset, rebase hoặc commit ngoài phê duyệt.
3. **KHÔNG** chạy script ghi/xóa database, payment, recovery cleanup toàn cục hoặc thay đổi phiên/bàn/đơn thật.
4. **KHÔNG** đăng xuất, xóa dữ liệu hay cài đè ứng dụng trên Samsung đang giữ session nếu chưa đánh giá tác động.
5. Giữ nguyên logs, screenshots, test evidence, ma trận, thư mục tài liệu và working tree; không xóa sạch build/cache nếu chúng là bằng chứng cần giữ.
6. Các bridge QA/tunnel/token tạm phải được **kiểm tra trạng thái trên máy thực tế** bởi người vận hành; báo cáo trước đó cho biết bridge đã tắt, **tài liệu này không tự dừng tiến trình**.
7. Mọi thao tác tiếp theo do **người nhận bàn giao** đảm nhận, sau khi chủ dự án phê duyệt phạm vi; phải mở một phiên làm việc/nhật ký công việc mới, không viết đè lịch sử nghiệm thu.

**Phạm vi lệnh dừng trong phiên này:** Đây là **chỉ thị đóng băng và biên bản bàn giao**, không phải hành động từ xa tắt Vercel, Docker, backend, điện thoại, Codex hay những tiến trình/automation khác mà người lập tài liệu không điều khiển được.

## 9. Bộ tài liệu phải chuyển nguyên vẹn cho người nhận

Thư mục gốc: `D:\Quang\VietTable\docs\acceptance\`

| Tài liệu | Vai trò |
|---|---|
| `FINAL_ACCEPTANCE_REPORT.md` | Báo cáo nghiệm thu chi tiết đã có |
| `FULL_ACCEPTANCE_MATRIX.csv` | Ma trận 64 test, trạng thái, bằng chứng, môi trường |
| `REMAINING_ISSUES.md` | Các tồn đọng và lý do chưa nghiệm thu |
| `RELEASE_READINESS.md` | Quyết định sẵn sàng phát hành / chặn phát hành |
| `evidence/RUN_2026-10-12.md` | Bằng chứng và nhật ký kiểm thử cuối |
| `../Q1_CLOUD_E2E_ACCEPTANCE.md` | E2E Cloud Quận 1 và kết quả lịch sử |
| `../FULL_SYSTEM_PERFORMANCE_AUDIT.md` | Baseline và phân tích hiệu năng |

Tài liệu bàn giao này nên được lưu tại:

`D:\Quang\VietTable\docs\acceptance\VIETTABLE_NGHIEM_THU_BAN_GIAO_2026-10-12.md`

**Lưu ý:** chưa sao chép tệp vào đường dẫn Windows trên bằng công cụ trong phiên này. Tệp Markdown được cung cấp riêng để chủ dự án chuyển cho người tiếp nhận.

## 10. Hướng dẫn bắt đầu cho người nhận (CHỈ KHI ĐƯỢC CHO PHÉP TIẾP TỤC)

**Bước 1 — Kiểm kê, không thay đổi:** đọc tất cả tài liệu Mục 9; xem `git status`, current branch/HEAD, candidate/stable IDs, phiên bản APK, test fixture và branch của mỗi ảnh chụp. Lập baseline mới; không tự động chạy lệnh sửa chữa.

**Bước 2 — Xác nhận quyền và phạm vi:** xác định tài khoản, môi trường QA cô lập, thiết bị Android, quyền Vercel, hai customer QA và dữ liệu test. Lấy chấp thuận bằng văn bản trước mọi thao tác đổi Cloud/routing/recovery/payment.

**Bước 3 — Kế hoạch nghiệm thu lại:** ưu tiên VT-HO-01 và VT-HO-02, sau đó VT-HO-03/04, cuối cùng benchmark và regression. Mỗi lần chuyển trạng thái test phải gắn bằng chứng runtime mới và điều kiện môi trường.

**Bước 4 — Quyết định release:** chỉ xem xét FULLY ACCEPTED và thay stable alias sau khi người có thẩm quyền duyệt tiêu chí, bằng chứng và kế hoạch rollback. Việc sửa xong code chưa tương đương chấp thuận release.

## 11. Câu hỏi còn mở / không được tự điền giả định

- SLA chính thức cho Home/session/Cashier là bao nhiêu? Chưa có tiêu chí được duyệt trong báo cáo được cung cấp.
- 4 BLOCKED và 7 NOT TESTED tương ứng chính xác những ID nào? Đọc trực tiếp `FULL_ACCEPTANCE_MATRIX.csv`, không suy từ bảng tổng hợp này.
- Hai QA customer/fixtures độc lập đã có chưa, có quyền thử âm tính realtime không?
- Con đường truy cập CloudPreview cho Samsung được chủ dự án cho phép là gì?
- Có môi trường QA recovery không có cleanup toàn cục hay chưa?
- Current branch, HEAD, dirty files, APK build trên Samsung và deployment hiện tại có còn đúng với snapshot báo cáo không?
- Người phụ trách mới, người duyệt kết quả và ngày bàn giao chính thức là ai?

## 12. Biên nhận bàn giao / ký xác nhận (CHƯA KÝ)

| Vai trò | Người xác nhận | Ngày giờ | Xác nhận |
|---|---|---|---|
| Chủ dự án / người giao | _Chưa điền_ | _Chưa điền_ | _Chưa ký_ |
| Người tiếp nhận kỹ thuật | _Chưa điền_ | _Chưa điền_ | _Chưa ký_ |
| Người duyệt nghiệm thu / release | _Chưa điền_ | _Chưa điền_ | _Chưa ký_ |

**Nội dung xác nhận khi bàn giao:** Người tiếp nhận đã nhận đúng repository/tài liệu/evidence và hiểu rằng đây là **bàn giao công việc ở trạng thái PARTIAL ACCEPTANCE**, không phải bàn giao sản phẩm đã được nghiệm thu toàn phần. Các quyền truy cập và thay đổi production cần phê duyệt riêng.

---

## KẾT LUẬN CUỐI

**VIETTABLE = CONDITIONAL / PARTIAL ACCEPTANCE.** Có bằng chứng build, backend và nhiều luồng thành công; còn rủi ro/điều kiện chưa chứng minh ở CloudPreview Android, cách ly payload realtime, recovery an toàn, E2E Samsung và hiệu năng. **Dừng công việc ở hiện trạng để chuyển giao; không tiếp tục sửa/đo/deploy từ biên bản này.**
