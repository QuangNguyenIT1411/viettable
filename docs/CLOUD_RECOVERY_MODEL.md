# CloudHost durable recovery (Phase C)

Scope: `SelfRestaurant-master/src/Backend/VietTable.CloudHost` on PostgreSQL/Supabase. The six-process local deployment retains its hosted workers. This document does not authorize Vercel deployment.

## Source inventory and criticality

| Owner / class | Legacy trigger and input | Writes / side effects | Previous retry and failure | Classification |
|---|---|---|---|---|
| Catalog / `TableAutoReleaseService` | 60-second scan; `DiningTables` OCCUPIED, `CurrentOrderID=null`, `UpdatedAt` older than 15 minutes | AVAILABLE, clears order link | Catches errors and retries next loop. Could incorrectly release a checked-in session before its first order. | C — idle cleanup; must never undermine active-session correctness |
| Customers / `OrderReadyConsumerService` | Orders `order.status-ready.v1` outbox, 2-second poll, batch20 | `ReadyDishNotifications`, Customers inbox, source ACK | Formerly DEAD after three attempts; crash after inbox commit before ACK causes replay. READY itself already canonical in Orders. | B — eventually consistent customer notification |
| Orders / `PaymentCompletedConsumerService` | Billing legacy `payment.completed.v1` outbox; configured 2-second poll (code fallback 5), batch20 | Orders completion/inbox, Catalog table release, source ACK | Formerly DEAD after three attempts; Catalog release exception was swallowed, allowing ACK despite an occupied table. | A — legacy payment completion/release |
| Billing / `PaymentReconciliationWorker` | 5-second scan, first 50 invoices with null `SettlementCompletedAtUtc` | Orders `payment-succeeded` command, Customers loyalty settlement, Billing settlement receipt | Individual `ReconcileSafelyAsync` catches failure; invoice stays unresolved. Payment remains SUCCEEDED. | A — mobile settlement, session close and loyalty |

Existing source: `Catalog.Api/Infrastructure/Tables/TableAutoReleaseService.cs`, `Customers.Api/Infrastructure/OrderReadyConsumerService.cs`, `Orders.Api/Infrastructure/Eventing/PaymentCompletedConsumerService.cs`, and `Billing.Api/Infrastructure/Payments/PaymentFinalizationService.cs`. The paths are relative to `SelfRestaurant-master/src/Services`.

## Replacement and ownership

CloudHost config disables `Runtime:LegacyPersistentWorkers`, RabbitMQ and diagnostic JSONL. None of the four hosted loops runs in CloudHost. Each owner exposes a **bounded one-shot method** on the same handler used by local workers. CloudHost coordinates calls but does not query another owner's tables: Billing invoice/quote, Orders outbox/session, Customers inbox/notification, and Catalog table state remain in their respective owner schemas.

`POST` or `GET /api/internal/recovery` on the public CloudHost is accepted only with `Authorization: Bearer <CRON_SECRET>`. Missing configuration returns 503; missing/wrong bearer returns 401. The value is provisioned by deployment environment, never in source. Private module `/api/internal/*` routes remain undiscoverable through Gateway. A pass processes at most 50 Billing invoices, 20 Billing events, 20 Orders READY events and 50 idle tables. Run repeatedly until backlog clears. The endpoint is not a general customer/staff API.

One CloudHost recovery pass holds a transaction-scoped PostgreSQL advisory lock in Billing for its duration. Another instance uses `pg_try_advisory_xact_lock` and returns HTTP 202 `already_running` instead of waiting/duplicating the work. The lock is released on commit/rollback or process death, so no RAM ownership and no stranded lease. SQL Server's transaction app-lock is retained for local compatibility. The lock is for coordination only; each owner uses its own DbContext and commits independently, **not** a distributed transaction. Normal business requests remain concurrent and rely on their own idempotency constraints.

### Fast paths

- Chef READY: Orders state and `order.status-ready.v1` outbox now commit in one Orders transaction in CloudHost. On successful Gateway READY POST, a bounded recovery pass projects to Customers immediately. If it fails or the process dies, the outbox remains PENDING. Customers inbox `(source, sourceEventId)` and order/item/event check prevent duplicate projection; ACK follows inbox commit.
- Mobile payment: Billing marks payment/parts/quote and inserts one invoice in its transaction. `ConfirmAsync` immediately calls Orders session completion/table release and Customers loyalty settlement. `SettlementCompletedAtUtc` is written only after both succeed. If the process dies or either call fails, the invoice is the durable pending work; recovery retries it. Each attempt records `RecoveryAttemptCount` and `RecoveryLastAttemptAtUtc`; the bounded scan selects never-attempted and least-recently-attempted invoices first so a permanently failing oldest invoice cannot starve newer work. Orders `payment-succeeded` is idempotent on `(session, quote, payment)` and retries a missed Catalog release, but refuses to release a table reused by a newer active session.
- Legacy cashier checkout: Billing payment outbox is committed with its checkout transaction. Successful cashier Gateway POST triggers a bounded pass. Orders inbox prevents duplicate completion. Catalog release errors now leave the event unacknowledged, not silently marked delivered.
- Idle table cleanup: a scheduled pass checks expired Catalog rows but excludes table IDs that Orders says belong to an active DiningSession. If Orders is unavailable, cleanup fails closed. This is cleanup, not the canonical payment-close path.

`RETRY` inbox records use bounded backoff (15–300 seconds), not a terminal third-attempt DEAD state. Outbox is acknowledged only after the consumer has persisted the corresponding inbox/effect. If source ACK alone fails after that commit, the inbox stays `PROCESSED`; a later pass only retries ACK. Orders commits legacy payment effect and inbox in one owner save. Duplicate recovery is expected and harmless; failure metrics/alerts for persistent RETRY and old invoices/outboxes are still an operational requirement before public production.

## Failure matrix and acceptance

| Boundary | Durable marker | Recovery result |
|---|---|---|
| READY committed; projection throws or process stops | Orders READY + PENDING outbox | Customers projection once, inbox PROCESSED, source ACK |
| Payment SUCCEEDED committed; process stops before Orders | Billing invoice without settlement timestamp | Orders CLOSED, Catalog released, loyalty settled, timestamp written |
| Orders CLOSED; Catalog release throws | Billing invoice still unresolved; Orders closed session | Retried `payment-succeeded` releases only if no newer active session |
| Legacy event consumer crashes before ACK | Source outbox remains PENDING | Inbox processed check sends ACK without repeating effect |
| Two recovery calls race | Same persistent outbox/invoice plus advisory lock | One pass runs, other returns 202; idempotent owner handlers ensure one effect |

The test-only `Recovery:TestFailReadyProjection`, `Recovery:TestFailAfterPaymentCommit` and `Recovery:TestFailTableRelease` toggles are honored **only in Development**. `scripts/test-staging-database.ps1 -Action CloudRecovery` uses these on QA data with Supabase staging, kills/restarts the actual process, calls the authenticated endpoint concurrently and again, then checks canonical mobile responses. It never resets the database. Do not enable these toggles in user-facing environments.

Staging acceptance on 2026-10-06: the additive Billing invoice-retry migration applied (Billing history: 2 migrations; other four owners unchanged), and five-owner physical model parity passed. `CloudRecovery` passed on fresh QA sessions 10, 11 and 12 (the last after the inbox/ACK fix): READY survived an injected projection failure and restart with one notification after concurrent/duplicate recovery; payment `SUCCEEDED` and session `CLOSED` survived separate injected failures and restarts; subsequent recovery completed one invoice and settlement receipt and released the table. Sessions 11 and 12 also verified persisted invoice retry count/time. Cross-role API, branch denial, Admin QA-only CRUD, SignalR reconnect, and durable authentication after restart passed. CloudHost tests passed 5/5; backend contract tests passed 141 with one optional Redis skip. This is staging evidence, not a production deployment or recovery-latency SLA.

## Platform decision

- **Vercel Queues for .NET: NOT USED.** The [official Queue REST API](https://vercel.com/docs/queues/api) supports publish/receive/ack with a Vercel OIDC token; so raw .NET HTTP access is technically possible. The current [Queue documentation](https://vercel.com/docs/queues) lists JS and Python SDKs and push Function handlers, but no native .NET consumer trigger for this CloudHost. Adding a polling consumer or a Node adapter would add runtime/operational complexity without improving PostgreSQL correctness. No Queue message is used as authority.
- **Workflow SDK for .NET: NOT USED.** The [official Workflow documentation](https://vercel.com/docs/workflows) targets JS/TS; this project keeps orchestration in .NET and PostgreSQL rather than introducing a second runtime.
- **Vercel Cron: safety net only, not 2-second delivery.** [Cron Jobs](https://vercel.com/docs/cron-jobs) invoke an HTTP GET path on the production deployment and can supply `CRON_SECRET` as bearer auth. The [current limits](https://vercel.com/docs/cron-jobs/usage-and-pricing) allow Hobby no more frequently than once per day and Pro once per minute. A possible Hobby schedule is UTC `0 2 * * *` for `/api/internal/recovery`; it is **not configured or deployed yet**. Prompt failure recovery on Hobby requires an explicit on-demand invocation; normal success uses request fast paths. No near-real-time failure SLA is claimed from daily Cron.
- **RabbitMQ:** CloudHost requires no broker at startup, readiness, normal E2E, or recovery. Local six-service compatibility retains optional RabbitMQ diagnostics/publishing; do not delete it until separate migration acceptance.

`readyz` checks databases and essential owner modules, not worker loops/RabbitMQ. CloudHost is still a local/staging acceptance artifact; production Vercel scheduling, budget, domain/TLS and multi-instance SignalR remain separate gates.
