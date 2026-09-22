# Phase 4 - Progress

## Muc tieu Phase 4

Bat dau dua he thong sang huong event-driven theo cach an toan: publish integration event that su o nhung diem nghiep vu quan trong, giu nguyen flow dong bo hien tai de tranh regression, va tao nen tang de sau nay noi sang broker/outbox day du.

## Dot 1 - Integration event backbone cho Orders va Billing

Date: 2026-03-25

Changes:
- `Orders.Api` bo sung háº¡ táº§ng event publisher nhe:
  - `IIntegrationEventPublisher`
  - `FileIntegrationEventPublisher`
- `Billing.Api` bo sung háº¡ táº§ng event publisher nhe:
  - `IIntegrationEventPublisher`
  - `FileIntegrationEventPublisher`
- Event sink tam thoi ghi dong thoi vao:
  - file JSONL trong `App_Data/integration-events`
  - bang `dbo.OutboxEvents` trong database cua tung service
- `Orders.Api` publish cac su kien nghiep vu quan trong:
  - `order.submitted.v1`
  - `order.status-preparing.v1`
  - `order.status-ready.v1`
  - `order.received-confirmed.v1`
  - `order.checkout-completed.v1`
  - `order.cancelled.v1`
- `Billing.Api` publish su kien:
  - `payment.completed.v1`

Why this matters:
- He thong da co event backbone that su, khong con chi dung request-response va tai lieu ly thuyet.
- Event duoc gan voi correlation id cua request hien tai, phu hop cho tracing va mo rong sau nay.
- Cach lam nay giu an toan migration:
  - nghiep vu dong bo van giu nguyen
  - event publish la best-effort, khong lam vo flow chinh neu event sink gap su co
- Tao nen tang de chuyen tiep sang:
  - outbox pattern day du hon
  - message broker (RabbitMQ)
  - consumer retry / dead-letter

Runtime evidence:
- `Orders.Api` da ghi event vao:
  - `src/Services/SelfRestaurant.Orders.Api/bin/Release/net8.0/App_Data/integration-events/orders-events-20260325.jsonl`
- `Billing.Api` da ghi event vao:
  - `src/Services/SelfRestaurant.Billing.Api/bin/Release/net8.0/App_Data/integration-events/billing-events-20260325.jsonl`
- Mau event thu duoc sau smoke test:
  - `order.status-ready.v1`
  - `order.received-confirmed.v1`
  - `order.checkout-completed.v1`
  - `payment.completed.v1`

Validation:
- Rebuild Release pass cho toan bo solution thanh phan.
- Full smoke sau reset pass:
  - customer 18/18
  - chef <-> customer PASS
  - cashier <-> customer PASS
  - admin 6/6 PASS


Dot 2 - Outbox table cho Orders va Billing

Date: 2026-03-25

Changes:
- `Orders.Api` bo sung entity `OutboxEvents`, map trong `OrdersDbContext` va bootstrap tao bang `dbo.OutboxEvents` neu chua ton tai.
- `Billing.Api` bo sung entity `OutboxEvents`, map trong `BillingDbContext` va bootstrap tao bang `dbo.OutboxEvents` neu chua ton tai.
- `FileIntegrationEventPublisher` cua ca `Orders` va `Billing` bay gio ghi ca 2 noi:
  - luu 1 dong vao bang `OutboxEvents` voi status `PENDING`
  - append 1 dong vao file JSONL de de quan sat khi demo
- `Program.cs` cua `Orders.Api` va `Billing.Api` doi event publisher sang `Scoped` de dung chung voi `DbContext`.

Why this matters:
- He thong da buoc qua muc â€œchi log ra fileâ€, va bat dau co event persistence theo huong outbox.
- Event duoc luu lai ben trong database service, phu hop hon cho retry, consumer, va eventual consistency o buoc sau.
- Giai phap hien tai van an toan khi migration: business flow sync khong doi, nhung event da co cho backend xu ly tiep.

Runtime evidence sau smoke test moi nhat:
- `RESTAURANT_ORDERS.dbo.OutboxEvents` co `7` dong.
- `RESTAURANT_BILLING.dbo.OutboxEvents` co `1` dong.
- File JSONL van ghi binh thuong, vi du: `order.status-ready.v1`, `order.received-confirmed.v1`, `order.checkout-completed.v1`, `payment.completed.v1`.

Validation:
- Rebuild stack `start-phase1.ps1 -Rebuild` pass.
- Health check `5100-5105` pass.
- Full smoke sau reset pass:
  - customer 18/18
  - chef <-> customer PASS
  - cashier <-> customer PASS
  - admin 6/6 PASS


Dot 3 - Consumer dau tien cho payment.completed.v1

Date: 2026-03-25

Changes:
- `Billing.Api` bo sung internal outbox endpoints:
  - `GET /api/internal/outbox/pending`
  - `POST /api/internal/outbox/{id}/ack`
- `Orders.Api` bo sung `BillingEventsClient` de poll `Billing.Api` lay event `payment.completed.v1`.
- `Orders.Api` bo sung `PaymentCompletedConsumerService` chay nen theo chu ky.
- `Orders.Api` bo sung bang `dbo.InboxEvents` de luu event da consume theo huong idempotent inbox pattern.
- Consumer xu ly `payment.completed.v1` bang cach:
  - luu inbox record trong `Orders`
  - reconcile lai `Order.Status = COMPLETED`
  - release table ve `AVAILABLE` neu can
  - ack lai outbox event ben `Billing`

Why this matters:
- Day la consumer that su dau tien giua 2 service, khong con chi publish event mot chieu.
- `Orders` co kha nang tu dong hoa giai quyet eventual consistency neu luong sync checkout gap truc trac.
- Inbox/outbox dua he thong tien gan hon den integration pattern dung chuan microservice.

Runtime evidence:
- Sau smoke test, `RESTAURANT_ORDERS.dbo.InboxEvents` co `2` dong `payment.completed.v1` voi status `PROCESSED`.
- `RESTAURANT_BILLING.dbo.OutboxEvents` co `0` dong `PENDING`, `2` dong `PROCESSED`.
- Gia tri `Error` tren outbox duoc dung de ghi nhan ACK tu consumer: `ACK:Orders.Api/payment-completed-consumer`.

Validation:
- Rebuild stack `start-phase1.ps1 -Rebuild` pass.
- Full smoke pass sau khi bo sung consumer:
  - customer 18/18
  - chef <-> customer PASS
  - cashier <-> customer PASS
  - admin 6/6 PASS

## Buoc tiep theo de xuat

1. Bo sung retry/dead-letter strategy cho consumer dau tien.
2. Mo rong them consumer cho `order.status-ready.v1` hoac `order.received-confirmed.v1` neu can projection/doc lap.
3. Chuan hoa event contracts va versioning.
4. Sau khi on dinh moi noi sang broker that su (RabbitMQ).

Dot 4 - Retry/dead-letter cho consumer thanh toan va consumer thu hai cho order.status-ready.v1

Date: 2026-03-25

Changes:
- `Orders.Api` mo rong `InboxEvents` de ho tro retry:
  - `RetryCount`
  - `NextRetryAtUtc`
- `PaymentCompletedConsumerService` bo sung retry/dead-letter logic:
  - event loi se duoc retry toi da 3 lan
  - qua nguong se chuyen `Status = DEAD`
  - event thanh cong se chuyen `Status = PROCESSED`
- `Orders.Api` bo sung internal outbox endpoint rieng cho service nay:
  - `GET /api/internal/outbox/pending`
  - `POST /api/internal/outbox/{id}/ack`
- `Customers.Api` bo sung consumer thu hai `OrderReadyConsumerService` de poll `Orders.Api` cho event `order.status-ready.v1`.
- `Customers.Api` bo sung bang:
  - `dbo.InboxEvents`
  - `dbo.ReadyDishNotifications`
- Consumer `order.status-ready.v1` se:
  - luu inbox idempotent trong `Customers`
  - tao projection thong bao mon san sang trong `ReadyDishNotifications`
  - ack lai outbox event ben `Orders`

Why this matters:
- He thong da co retry/dead-letter o consumer dau tien, khong con la background poller don gian.
- Da co consumer thu hai o mot service khac (`Customers`), chung minh event backbone dang tro thanh integration layer that su.
- `ReadyDishNotifications` la mot projection doc lap, cho thay gia tri cua event-driven architecture trong viec xay dung read model phuc vu UI/notification.

Runtime evidence sau rebuild va smoke test:
- `RESTAURANT_ORDERS.dbo.InboxEvents` co `3` dong `payment.completed.v1`, tat ca `PROCESSED`, `RETRY = 0`, `DEAD = 0`.
- `RESTAURANT_BILLING.dbo.OutboxEvents` co 3 dong `payment.completed.v1`, tat ca `PROCESSED` va co dau ACK tu `Orders.Api/payment-completed-consumer` trong cot `Error`.
- `RESTAURANT_CUSTOMERS.dbo.InboxEvents` co `3` dong `order.status-ready.v1`, tat ca `PROCESSED`.
- `RESTAURANT_CUSTOMERS.dbo.ReadyDishNotifications` co `2` dong thong bao san sang voi `Status = OPEN`.

Validation:
- Rebuild stack `start-phase1.ps1 -Rebuild` pass.
- Full smoke sau reset pass:
  - customer 18/18
  - chef <-> customer PASS
  - cashier <-> customer PASS
  - admin 6/6 PASS

Dot 5 - Expose read-model thong bao mon san sang ra API va Gateway

Date: 2026-03-27

Changes:
- `Customers.Api` bo sung public endpoints cho read-model `ReadyDishNotifications`:
  - `GET /api/customers/{customerId}/ready-notifications?tableId=...`
  - `POST /api/customers/{customerId}/ready-notifications/{notificationId}/resolve`
- `Gateway.Api` bo sung contract va client de doc/xu ly ready notifications tu `Customers.Api`.
- `MenuController` bo sung 2 endpoint BFF:
  - `GET /Menu/GetReadyNotifications`
  - `POST /Menu/ResolveReadyNotification`
- `Views/Menu/Index.cshtml` bo sung poll cycle rieng cho ready notifications:
  - doc danh sach thong bao tu Gateway
  - render vao `notificationArea`
  - mo modal â€œmon san sangâ€ tu projection event-driven
  - resolve notification khi khach xac nhan da nhan mon

Why this matters:
- Event-driven projection gio khong con chi nam trong database, ma da duoc dua ra bieu dien cho UI doc va xu ly.
- `Customers.Api` tro thanh owner cua read-model thong bao cho khach hang, dung vai tro projection service.
- Gateway chi doc projection qua API, khong can biet chi tiet event hay schema ben duoi.

Runtime evidence:
- `GET http://localhost:5103/api/customers/1/ready-notifications?tableId=1` tra ve danh sach thong bao `order.status-ready.v1` that su.
- `RESTAURANT_CUSTOMERS.dbo.ReadyDishNotifications` van chua cac dong `OPEN` sau chef flow.
- Full smoke sau rebuild va reset van pass:
  - customer 18/18
  - chef <-> customer PASS
  - cashier <-> customer PASS
  - admin 6/6 PASS

Dot 6 - RabbitMQ publisher duoc bat theo kieu optional-safe

Date: 2026-03-27

Changes:
- `Orders.Api` va `Billing.Api` bo sung package `RabbitMQ.Client`.
- Publisher event giu nguyen `OutboxEvents + JSONL`, dong thoi co the publish them len RabbitMQ neu `RabbitMq:Enabled = true`.
- `docker-compose.yml` bo sung service `rabbitmq:3-management`.
- `orders` va `billing` trong docker compose duoc cap env vars `RabbitMq__*` de bat publisher khi chay stack docker.
- Bo sung tai lieu van hanh:
  - `docs/RABBITMQ_LOCAL.md`

Why this matters:
- He thong da co broker that su de buoc tiep event backbone ra khoi file/HTTP polling.
- Cac luong hien tai van an toan vi `Outbox + JSONL + consumers` cu van duoc giu nguyen.
- Day la buoc "bat duoc broker that su" ma khong can cutover manh tay.

Validation:
- Rebuild stack local pass khi `RabbitMq:Enabled = false`.
- Full smoke tren stack local pass sau reset:
  - customer 18/18
  - chef <-> customer PASS
  - cashier <-> customer PASS
  - admin 6/6 PASS

