# PHASE 5 PROGRESS

Dot 1 - Runtime hardening co loi ro cho van hanh local/demo

Date: 2026-03-27

Changes:
- Chuan hoa timeout cho giao tiep noi bo:
  - `Gateway.Api`
  - `Orders.Api`
  - `Billing.Api`
  - `Customers.Api`
- Timeout duoc cau hinh qua `Services:TimeoutSeconds`, mac dinh `10s`.
- Bo sung diagnostics endpoints cho eventing:
  - `Orders.Api`: `/internal/diagnostics/eventing`
  - `Billing.Api`: `/internal/diagnostics/eventing`
  - `Customers.Api`: `/internal/diagnostics/eventing`

Why this matters:
- Khi mot service cham hoac dung tam thoi, cac service khac se khong treo request vo thoi han.
- Team co them diem quan sat nhanh de kiem tra outbox/inbox/event projection ma khong can vao SQL ngay.
- Day la dang hardening nhe nhung co tac dung truc tiep cho demo, regression test va van hanh local.

Validation:
- Rebuild stack pass.
- Full smoke pass sau reset:
  - customer 18/18
  - chef <-> customer PASS
  - cashier <-> customer PASS
  - admin 6/6 PASS

