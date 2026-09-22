# Local Frontend Development

This repo has two valid local web flows:

1. `Gateway + built frontend`
2. `Gateway + APIs in Visual Studio` and `npm run dev` for one or more frontend apps

## Why only APIs/Swagger were showing

Before this update:

- every backend API project had `launchBrowser: true` with `launchUrl: swagger`
- the gateway had `launchBrowser: false`

So Visual Studio startup opened Swagger tabs from the APIs instead of opening the customer web UI served by the gateway.

## Preferred local entry points

If you start the backend from Visual Studio:

- customer UI via gateway: `http://localhost:5100/`
- chef UI via gateway: `http://localhost:5100/app/chef/Staff/Account/Login`
- cashier UI via gateway: `http://localhost:5100/app/cashier/Staff/Account/Login`
- admin UI via gateway: `http://localhost:5100/app/admin/Admin/Account/Login`

If you run frontend Vite dev servers separately:

- customer UI: `http://localhost:5173/`
- chef UI: `http://localhost:5174/app/chef/Staff/Account/Login`
- cashier UI: `http://localhost:5175/app/cashier/Staff/Account/Login`
- admin UI: `http://localhost:5176/app/admin/Admin/Account/Login`

All Vite apps proxy `/api` to the gateway at `http://localhost:5100` by default.

You can override that target with `VITE_GATEWAY_URL`.

Example:

```bash
VITE_GATEWAY_URL=http://localhost:5200 npm run dev
```

## Run steps

### Backend

Start these projects in Visual Studio:

- `SelfRestaurant.Gateway.Api`
- `SelfRestaurant.Catalog.Api`
- `SelfRestaurant.Orders.Api`
- `SelfRestaurant.Customers.Api`
- `SelfRestaurant.Identity.Api`
- `SelfRestaurant.Billing.Api`

After this change, the gateway is the only project that opens a browser automatically.

If Visual Studio reports `address already in use` on one of the standard local service ports (`5100-5105`), clear stale listeners first:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\stop-dev-ports.ps1
```

This is the expected recovery step when a previous debug session or local start script left a service process running.

### Customer web

```bash
cd src/Frontend/selfrestaurant-customer-web
npm install
npm run dev
```

Open:

- `http://localhost:5173/`

### Chef web

```bash
cd src/Frontend/selfrestaurant-chef-web
npm install
npm run dev
```

Open:

- `http://localhost:5174/app/chef/Staff/Account/Login`

### Cashier web

```bash
cd src/Frontend/selfrestaurant-cashier-web
npm install
npm run dev
```

Open:

- `http://localhost:5175/app/cashier/Staff/Account/Login`

### Admin web

```bash
cd src/Frontend/selfrestaurant-admin-web
npm install
npm run dev
```

Open:

- `http://localhost:5176/app/admin/Admin/Account/Login`

## Notes

- Customer web is the simplest local dev flow and is the best first UI to validate.
- Running through the gateway keeps all apps on one origin and matches production-like routing more closely.
- Running the Vite apps directly is best for frontend iteration and HMR.
