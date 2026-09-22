# RabbitMQ Local

Tai lieu nay ghi cach bat RabbitMQ local cho SelfRestaurant ma khong lam anh huong den flow hien tai.

## Muc tieu

- `Orders.Api` va `Billing.Api` van ghi `OutboxEvents` + JSONL nhu hien tai.
- Khi bat `RabbitMq:Enabled = true`, publisher se phat them event len exchange `selfrestaurant.events`.
- Neu broker chua chay, he thong van khong gay flow sync.

## Cach bat RabbitMQ bang Docker Compose

```bash
docker compose up -d rabbitmq
```

Hoac tren Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-rabbitmq-windows.ps1
```

Thong tin mac dinh:

- Management UI: `http://localhost:15672`
- AMQP: `localhost:5672`
- Username: `guest`
- Password: `guest`

## Cach bat publisher RabbitMQ local

Cap nhat `appsettings.json` cua:

- `src/Services/SelfRestaurant.Orders.Api/appsettings.json`
- `src/Services/SelfRestaurant.Billing.Api/appsettings.json`

Dat:

```json
"RabbitMq": {
  "Enabled": true,
  "Host": "localhost",
  "Port": 5672,
  "Username": "guest",
  "Password": "guest",
  "VirtualHost": "/",
  "Exchange": "selfrestaurant.events",
  "RoutingKeyPrefix": "selfrestaurant"
}
```

Neu muon bat nhanh cho local exe stack ma khong sua file config, co the dung:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-phase1.ps1 -EnableRabbitMq
```

## Trong Docker Compose

`docker-compose.yml` da duoc cap nhat de:

- them service `rabbitmq:3-management`
- truyen `RabbitMq__*` env vars vao `orders` va `billing`

Neu chay bang docker compose, publisher RabbitMQ se duoc bat san.

## Kiem tra nhanh

1. Tao order va gui bep
2. Cho chef chuyen `READY`
3. Cho cashier thanh toan
4. Mo RabbitMQ management UI va xem exchange `selfrestaurant.events`

## Ghi chu

- RabbitMQ hien tai la broker phat event bo sung.
- Consumer dang chay o local stack van dua tren `outbox + poll` de giu migration an toan.
- Buoc tiep theo hop ly la dual-read hoac cutover consumer sang broker that su.
