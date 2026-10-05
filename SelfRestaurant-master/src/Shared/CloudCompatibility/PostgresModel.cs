using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SelfRestaurant.CloudCompatibility;

internal static class PostgresModel
{
    private static readonly ValueConverter<DateTime, DateTime> Utc = new(
        value => NormalizeInstant(value), value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static DateTime NormalizeInstant(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => throw new InvalidOperationException("PostgreSQL instant requires an explicit UTC/local DateTime kind.")
    };

    public static void Apply(ModelBuilder builder, string schema)
    {
        builder.HasDefaultSchema(schema);
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            if (entity.GetTableName() is not null) entity.SetSchema(schema);
            if (entity.GetViewName() is not null) entity.SetViewSchema(schema);
            foreach (var property in entity.GetProperties())
            {
                var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (clrType == typeof(DateTime))
                {
                    property.SetColumnType("timestamp with time zone");
                    property.SetValueConverter(Utc);
                }
                else if (property.GetColumnType() == "nvarchar(max)") property.SetColumnType("text");
                var defaultSql = property.GetDefaultValueSql();
                if (defaultSql is "(getdate())" or "SYSUTCDATETIME()") property.SetDefaultValueSql("CURRENT_TIMESTAMP");
                else if (defaultSql is not null)
                    throw new InvalidOperationException($"Unreviewed PostgreSQL default on {entity.Name}.{property.Name}.");
            }
            // Exact known filters, not a dialect-wide string replacement.
            foreach (var index in entity.GetIndexes())
            {
                var filter = index.GetFilter();
                if (filter is null) continue;
                index.SetFilter(filter switch
                {
                    "[SubmitIdempotencyKey] IS NOT NULL" => "\"SubmitIdempotencyKey\" IS NOT NULL",
                    "[ReservationId] IS NOT NULL" => "\"ReservationId\" IS NOT NULL",
                    "[EndedAtUtc] IS NULL" => "\"EndedAtUtc\" IS NULL",
                    "[CheckoutIdempotencyKey] IS NOT NULL" => "\"CheckoutIdempotencyKey\" IS NOT NULL",
                    "[IsActive] = 1" => "\"IsActive\" = TRUE",
                    "[ProviderReference] IS NOT NULL" => "\"ProviderReference\" IS NOT NULL",
                    "[IdempotencyKey] IS NOT NULL" => "\"IdempotencyKey\" IS NOT NULL",
                    _ => throw new InvalidOperationException($"Unreviewed PostgreSQL index filter on {entity.Name}.")
                });
            }
        }
    }
}
