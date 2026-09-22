# Orders DB Split Plan

## Write Model Owner

`Orders.Api` owns these write tables and is the only service allowed to mutate them directly:

- `Orders`
- `OrderItems`
- `OrderStatus`
- `InboxEvents`
- `OutboxEvents`

## Local Read Models

`Orders.Api` may persist local snapshots for external catalog reads, but these are not source-of-truth tables:

- `CatalogBranchSnapshots`
- `CatalogDishSnapshots`
- `CatalogTableSnapshots`

Refresh strategy:

- use fresh local snapshot first
- refresh remotely from `Catalog.Api` when local data is stale or missing
- fall back to stale local snapshot when the remote call fails

Implementation rule:

- `OrdersController` and background consumers must depend on `ICatalogReadModel`
- no runtime flow in `Orders.Api` may query `CatalogBranchSnapshots`, `CatalogDishSnapshots`, or `CatalogTableSnapshots` directly outside the catalog read-model client

Remaining sync dependency policy:

- customer loyalty lookup by phone goes through `ICustomerLoyaltyReadModel` with short-lived cache
- scan-loyalty flow must not perform a duplicate customer lookup after the initial resolve
- billing integration remains asynchronous through outbox polling, not request-time command coupling

## Runtime Boundary

`Orders.Api` is the source of truth for:

- active order state
- kitchen transitions
- order item mutations
- order completion after `payment.completed.v1`

`Orders.Api` is not the source of truth for:

- branch/table/menu master data
- customer profile or loyalty master data
- billing records

## Cutover Requirements

Before deep DB cutover, `Orders.Api` must start only when all owned write tables and all local snapshot tables exist physically in the Orders database.

## After Orders Cutover

`Billing.Api` can be cut deeper with less risk, because live order state will already be anchored cleanly in `Orders.Api`.
