# Final Regression Checklist

Date: 2026-03-31
Base URL: `http://localhost:5100`

## Final Result

- Full rebuild: `PASS`
- Service health:
  - `5100` => `200`
  - `5101` => `200`
  - `5102` => `200`
  - `5103` => `200`
  - `5104` => `200`
  - `5105` => `200`

## Regression Logs

- Customer:
  - [.runlogs/customer_detailed_new_summary_20260331_081641.json](/mnt/c/Users/Quang/Downloads/SINH_VIEN/SINH_VIEN/SelfRestaurant-main/.runlogs/customer_detailed_new_summary_20260331_081641.json)
  - [.runlogs/customer_detailed_new_20260331_081641.log](/mnt/c/Users/Quang/Downloads/SINH_VIEN/SINH_VIEN/SelfRestaurant-main/.runlogs/customer_detailed_new_20260331_081641.log)
- Chef:
  - [.runlogs/chef_customer_flow_summary_20260331_081734.json](/mnt/c/Users/Quang/Downloads/SINH_VIEN/SINH_VIEN/SelfRestaurant-main/.runlogs/chef_customer_flow_summary_20260331_081734.json)
- Cashier:
  - [.runlogs/cashier_customer_flow_summary_20260331_081710.json](/mnt/c/Users/Quang/Downloads/SINH_VIEN/SINH_VIEN/SelfRestaurant-main/.runlogs/cashier_customer_flow_summary_20260331_081710.json)
- Admin:
  - [.runlogs/admin_flow_summary_20260331_081710.json](/mnt/c/Users/Quang/Downloads/SINH_VIEN/SINH_VIEN/SelfRestaurant-main/.runlogs/admin_flow_summary_20260331_081710.json)
  - [.runlogs/admin_flow_test_20260331_081710.log](/mnt/c/Users/Quang/Downloads/SINH_VIEN/SINH_VIEN/SelfRestaurant-main/.runlogs/admin_flow_test_20260331_081710.log)

## Checklist

### Customer

- [x] Home
- [x] Menu
- [x] Order
- [x] Login
- [x] Register
- [x] Forgot Password
- [x] Reset Password
- [x] Dashboard
- [x] Full customer regression pass: `18/18`

### Admin Outside Staff

- [x] Dashboard
- [x] Categories
- [x] Ingredients
- [x] Dishes
- [x] Customers
- [x] Employees
- [x] Employees History
- [x] Revenue Report
- [x] Top Dishes Report
- [x] Settings
- [x] Admin regression pass: `6/6`

### Staff Chef

- [x] Orders board
- [x] Dish list/detail view
- [x] Dish ingredients view-only flow
- [x] Stop selling / continue selling flow
- [x] History
- [x] Chef/customer flow pass

### Staff Cashier

- [x] Cashier index
- [x] Table selection
- [x] Payment flow
- [x] Loyalty points flow
- [x] History
- [x] Report
- [x] Cashier/customer flow pass

## Final Notes

- One parallel test run produced a `chef` failure because `chef` and `cashier` touched overlapping test data at the same time. This was a test collision, not a runtime regression.
- After resetting orders/bills and rerunning `chef` sequentially, the chef flow passed cleanly.
- Current state is suitable for demo/submission on the microservice stack.

## Still Open

- RabbitMQ is still `ready-to-enable`, not the default runtime transport.
- Some views are intentionally not source-code `1:1` with the old MVC project because microservice runtime hooks remain in place under the same UI/use-case surface.
