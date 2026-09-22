`Orders.Api` only keeps write-model entities and local read-model snapshots here.

Write-model:
- `Orders`
- `OrderItems`
- `OrderStatus`
- `InboxEvents`
- `OutboxEvents`

Read-model snapshots:
- `CatalogBranchSnapshots`
- `CatalogDishSnapshots`
- `CatalogTableSnapshots`

Legacy shared-schema entities were removed to keep ownership boundaries explicit.

Catalog data in Orders is read-only snapshot data. Controllers and background workers must go through `ICatalogReadModel`/`CatalogApiClient` and must not query `CatalogBranchSnapshots`, `CatalogTableSnapshots`, or `CatalogDishSnapshots` directly.
