# Deploy chat hỗ trợ + sửa bàn phím — 07/11/2026

## SQL

Chủ dự án chạy `SelfRestaurant-master/docs/SUPPORT_CHAT_MIGRATION_20261106.sql` trên Supabase (ảnh chụp chủ dự án gửi 07/11):
9 cột kiểm tra (`conversations_insert`, `conversations_update`, `messages_insert`, `quick_replies_delete`, `hours_update`,
`conversations_seq`, `messages_seq`, `quick_replies_seq`, `migrated`) đều `true`.

## Deploy (chủ dự án duyệt trước: "Sau khi tôi chạy SQL thì deploy + cài APK")

- Thư mục `scripts\prepare-staff-preview.ps1` (image `4b7a4a3e5d65`, 06/11 23:48), link `viet-table/viettable`
  (`projectName = viettable`, orgId `team_nCVol8ciJW2LgutrUDqgQUQU`), đã xoá `.env.local`, deploy 1 lần.
- **`dpl_CWXyn4NE1u9hA47pGwn9zEvz6ibK`** (`https://viettable-4h088vte6-viet-table.vercel.app`), `READY`, target production,
  alias `https://viettable-beta.vercel.app`. Code backend `c1a4c8a`.
- Gọi 1 lần: `/healthz` 200 1,0 s, `/readyz` 200 1,0 s, `/` 200, `/app/cashier/` 200, `/app/chef/` 200, `/app/admin/` 200,
  `/api/mobile/v1/branches` 200, `/api/mobile/v1/support/chat/topics` 401 (ẩn danh, đúng).
- Bản quay lui: `dpl_8rkgZQBvzq275Hn12M2C8H4NqxtN` (`https://viettable-be8r9hyr4-viet-table.vercel.app`); SQL chỉ thêm bảng.
- Ghi chú: tin nhắn chủ dự án "trước khi deploy tìm và sửa lại [bàn phím]" đến khi lệnh deploy đã chạy xong. Lỗi bàn phím
  chỉ ở app Android nên không cần deploy lại.

## Sửa bàn phím che ô nhập (`3428208`)

- Nguyên nhân: app nhắm Android mới (targetSdk 37) nên bị buộc vẽ tràn viền; `adjustResize` bị bỏ qua. Chỉ các màn chat
  (`liftBottomAboveKeyboard`) tự đẩy ô nhập lên; mọi màn khác bị bàn phím che.
- Sửa: `BaseFragment` cho mọi màn tự chừa phần bị bàn phím che (trừ phần dưới màn, ví dụ thanh menu) và cuộn ô đang nhập vào tầm nhìn.
- Máy ảo `bs_check` (bản debug, không server):
  - Đăng ký: ô "Xác nhận mật khẩu" trước nằm ở 1637–1790, mép bàn phím khoảng 1550 nên bị che. Sau khi sửa, bấm vào thì ô cuộn lên 1359–1512, nằm trên bàn phím (`screenshots\keyboard_fix\register_confirm_after.png`).
  - Đăng nhập: ô mật khẩu nằm trên bàn phím (`login_password_after.png`).
- Unit test 323/323 (chạy lại toàn bộ), 3 bản build đạt. APK `cloudProd` SHA-256 bắt đầu `E510919D6DCDAA49`
  (gồm chat hỗ trợ + sửa bàn phím + các sửa của bản `8C959DFA…`).

## Chưa làm

- **Chưa cài APK lên Samsung**: lúc cài, adb chỉ thấy máy ảo (Samsung không cắm). Cài khi chủ dự án cắm máy.
- Chưa test tay chat hỗ trợ với server thật (danh sách ở `RUN_2026-11-06_SUPPORT_CHAT.md`).
- Chưa push.
