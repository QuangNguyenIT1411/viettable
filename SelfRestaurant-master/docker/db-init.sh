#!/usr/bin/env bash
set -euo pipefail

MSSQL_HOST="${MSSQL_HOST:-db}"
MSSQL_PORT="${MSSQL_PORT:-1433}"
MSSQL_USER="${MSSQL_USER:-sa}"
MSSQL_PASSWORD="${MSSQL_PASSWORD:-${MSSQL_SA_PASSWORD:-}}"
DB_NAME="${DB_NAME:-RESTAURANT}"
SERVICE_DATABASES="${SERVICE_DATABASES:-RESTAURANT_CATALOG,RESTAURANT_ORDERS,RESTAURANT_CUSTOMERS,RESTAURANT_IDENTITY,RESTAURANT_BILLING}"
SCHEMA_SCRIPT="${SCHEMA_SCRIPT:-/scripts/localdb.sql}"

if [[ -z "${MSSQL_PASSWORD}" ]]; then
  echo "Missing MSSQL password. Set MSSQL_PASSWORD or MSSQL_SA_PASSWORD." >&2
  exit 1
fi

SQLCMD=""
for candidate in /opt/mssql-tools18/bin/sqlcmd /opt/mssql-tools/bin/sqlcmd sqlcmd; do
  if command -v "$candidate" >/dev/null 2>&1; then
    SQLCMD="$candidate"
    break
  fi
done

if [[ -z "$SQLCMD" ]]; then
  echo "sqlcmd not found in container." >&2
  exit 1
fi

echo "Waiting for SQL Server at ${MSSQL_HOST}:${MSSQL_PORT}..."
SQLCMD_CONN=("$SQLCMD" -S "${MSSQL_HOST},${MSSQL_PORT}" -U "$MSSQL_USER" -P "$MSSQL_PASSWORD")
# sqlcmd 18+ enforces encryption by default; trust local/dev cert to allow bootstrap.
if "$SQLCMD" -? 2>&1 | grep -q -- '-C'; then
  SQLCMD_CONN+=(-C)
fi

for i in {1..60}; do
  if "${SQLCMD_CONN[@]}" -Q "SELECT 1" >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

echo "Ensuring database ${DB_NAME} exists..."
"${SQLCMD_CONN[@]}" -Q "IF DB_ID('$(echo "$DB_NAME")') IS NULL CREATE DATABASE [$DB_NAME];"

echo "Waiting for database ${DB_NAME} to be online..."
DB_READY=0
for i in {1..60}; do
  if "${SQLCMD_CONN[@]}" -d "$DB_NAME" -Q "SELECT 1" >/dev/null 2>&1; then
    DB_READY=1
    break
  fi
  sleep 2
done
if [[ "$DB_READY" != "1" ]]; then
  echo "Database ${DB_NAME} did not become available in time." >&2
  exit 1
fi

IFS=',' read -r -a SERVICE_DATABASE_ARRAY <<< "$SERVICE_DATABASES"

for service_db in "${SERVICE_DATABASE_ARRAY[@]}"; do
  service_db="$(echo "$service_db" | xargs)"
  if [[ -z "$service_db" ]]; then
    continue
  fi
  echo "Ensuring service database ${service_db} exists..."
  "${SQLCMD_CONN[@]}" -Q "IF DB_ID('$(echo "$service_db")') IS NULL CREATE DATABASE [$service_db];"
done

echo "Checking if schema already exists..."
HAS_TABLES="$("${SQLCMD_CONN[@]}" -d "$DB_NAME" -h -1 -W -Q "SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='Orders') SELECT 1 ELSE SELECT 0;" | tr -d '\r' | tail -n 1)"

if [[ "$HAS_TABLES" == "1" ]]; then
  echo "Schema already present; skipping ${SCHEMA_SCRIPT}."
else
  if [[ ! -f "$SCHEMA_SCRIPT" ]]; then
    echo "Schema script not found: $SCHEMA_SCRIPT" >&2
    exit 1
  fi

  echo "Applying schema from ${SCHEMA_SCRIPT}..."
  "${SQLCMD_CONN[@]}" -i "$SCHEMA_SCRIPT" -b
fi

for service_db in "${SERVICE_DATABASE_ARRAY[@]}"; do
  service_db="$(echo "$service_db" | xargs)"
  if [[ -z "$service_db" ]]; then
    continue
  fi

  case "$service_db" in
    RESTAURANT_BILLING)
      # Explicit migration removes aliases only after application cutover verification.
      continue
      ;;
    RESTAURANT_CATALOG|RESTAURANT_IDENTITY)
      allow_inserts="-- this service uses physical owned tables only"
      ;;
    *)
      allow_inserts="INSERT INTO @allow (name) VALUES
        (N'ActiveOrders'),(N'Bills'),(N'Branches'),(N'BranchRevenue'),(N'Categories'),(N'CategoryDish'),
        (N'CustomerLoyalty'),(N'Customers'),(N'DiningTables'),(N'DishDetails'),(N'Dishes'),
        (N'DishIngredients'),(N'EmployeeRoles'),(N'Employees'),(N'Ingredients'),(N'LoyaltyCards'),
        (N'MenuCategory'),(N'Menus'),(N'OrderItemIngredients'),(N'OrderItems'),(N'Orders'),
        (N'OrderStatus'),(N'PasswordResetTokens'),(N'PaymentMethod'),(N'Payments'),
        (N'PaymentStatus'),(N'Reports'),(N'Restaurants'),(N'TableNumbers'),(N'TableStatus');"
      ;;
  esac

  SYNC_SQL_FILE="$(mktemp)"
  cat > "$SYNC_SQL_FILE" <<SQL
SET NOCOUNT ON;
DECLARE @master sysname = N'${DB_NAME}';
DECLARE @sql nvarchar(max) = N'';
DECLARE @allow TABLE (name sysname NOT NULL PRIMARY KEY);
${allow_inserts}

SELECT @sql = @sql + N'DROP SYNONYM dbo.' + QUOTENAME(name) + N';' + CHAR(10)
FROM sys.synonyms WHERE schema_id = SCHEMA_ID(N'dbo');
EXEC sp_executesql @sql;
SET @sql = N'';

SELECT @sql = @sql +
    N'IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE schema_id = SCHEMA_ID(N''dbo'') AND name = N''' + o.name + N''') ' +
    N'CREATE SYNONYM dbo.' + QUOTENAME(o.name) + N' FOR ' + QUOTENAME(@master) + N'.dbo.' + QUOTENAME(o.name) + N';' + CHAR(10)
FROM
(
    SELECT name FROM [${DB_NAME}].sys.tables WHERE schema_id = SCHEMA_ID(N'dbo')
    UNION
    SELECT name FROM [${DB_NAME}].sys.views WHERE schema_id = SCHEMA_ID(N'dbo')
) AS o
INNER JOIN @allow a ON a.name = o.name
ORDER BY o.name;
EXEC sp_executesql @sql;
SQL

  echo "Preparing service database shell: ${service_db}"
  "${SQLCMD_CONN[@]}" -d "$service_db" -i "$SYNC_SQL_FILE" -b
  rm -f "$SYNC_SQL_FILE"
done

for ownership_spec in \
  "RESTAURANT_CATALOG:/scripts/catalog-owned-tables.sql" \
  "RESTAURANT_IDENTITY:/scripts/identity-owned-tables.sql" \
  "RESTAURANT_ORDERS:/scripts/orders-owned-tables.sql" \
  "RESTAURANT_BILLING:/scripts/billing-owned-tables.sql"; do
  ownership_db="${ownership_spec%%:*}"
  ownership_script="${ownership_spec#*:}"
  if [[ ! -f "$ownership_script" ]]; then
    echo "Ownership script not found: $ownership_script" >&2
    exit 1
  fi
  echo "Materializing owned tables in ${ownership_db}"
  "${SQLCMD_CONN[@]}" -d "$ownership_db" -i "$ownership_script" -b
done

echo "DB init done."
