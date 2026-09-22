## Architecture Context
MVC is the business-flow source of truth.
The current microservice repo is a service-split implementation of those same flows.
Preserve MVC UI/UX and behavior, but adapt implementation to the current service ownership model.

Service ownership:
- Catalog: menu, tables, dishes, ingredients
- Orders: order lifecycle
- Identity: auth, customer, employee identity
- Billing: checkout, bill, cashier aggregates
- Customers: ready notification and some read models

The gateway preserves UI/session flow. Do not reintroduce direct cross-domain DB access.
## Migration Interpretation Rules
- MVC is the source of truth for user-visible behavior, screen flow, validation, and UI.
- Microservice is the source of truth for service boundaries and data ownership.
- Do not recreate MVC direct database access patterns inside microservice.
- Reproduce MVC outcomes through gateway orchestration and existing service ownership.
- When MVC and microservice architecture conflict, preserve MVC user-facing behavior while keeping microservice boundaries intact.