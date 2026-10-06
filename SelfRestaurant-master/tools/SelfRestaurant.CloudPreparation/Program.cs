using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SelfRestaurant.Catalog.Api.Persistence;
using SelfRestaurant.Orders.Api.Persistence;
using SelfRestaurant.Customers.Api.Persistence;
using SelfRestaurant.Identity.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence;

// Credentials arrive over stdin from the current-user DPAPI helper, not argv/files/logs.
// Explicit operator opt-in, never run from a production API startup or default test suite.
try
{
    var input = JsonSerializer.Deserialize<Input>(await Console.In.ReadToEndAsync())
        ?? throw new InvalidOperationException("Missing staging input.");
    if (input.Action is not ("Probe" or "Migrate" or "Verify" or "Roles" or "Bootstrap" or "CorrectReferenceTime" or "Workflow" or "CloudHost" or "CloudHostAuth" or "StorageUpload" or "StorageVerify")) throw new InvalidOperationException("Unsupported staging action.");
    if (string.IsNullOrWhiteSpace(input.ProjectRef) || string.IsNullOrWhiteSpace(input.Password))
        throw new InvalidOperationException("Staging credentials required.");
    if (!((input.Host == "db." + input.ProjectRef + ".supabase.co" && input.Username == "postgres")
        || (input.Host.EndsWith(".pooler.supabase.com") && input.Username == "postgres." + input.ProjectRef)))
        throw new InvalidOperationException("Connection does not identify the explicit staging project.");
    var connection = new NpgsqlConnectionStringBuilder
    {
        Host = input.Host, Port = input.Port, Database = "postgres", Username = input.Username,
        Password = input.Password, SslMode = SslMode.VerifyFull, RootCertificate = "/staging-ca/supabase-ca.crt", Timeout = 15,
        MaxPoolSize = 3, IncludeErrorDetail = false, ApplicationName = "VietTable-Staging-Preparation"
    }.ConnectionString;
    if (input.Action.StartsWith("Storage", StringComparison.Ordinal))
    {
        await StorageAcceptance.RunAsync(input);
        return 0;
    }
    await using var admin = new NpgsqlConnection(connection);
    await admin.OpenAsync();
    Console.WriteLine("PASS: staging PostgreSQL connection with verified TLS.");
    if (input.Action is "CloudHost" or "CloudHostAuth")
    {
        await CloudHostAcceptance.RunAsync(input);
        return 0;
    }
    if (input.Action == "CorrectReferenceTime")
    {
        await ReferenceBootstrap.CorrectTimesAsync(admin, input, owner => Context(owner, connection));
        return 0;
    }
    if (input.Action == "Workflow")
    {
        await CloudWorkflow.RunAsync(input, owner => Context(owner, CloudWorkflow.RuntimeConnection(input, owner)));
        return 0;
    }
    if (input.Action == "Bootstrap")
    {
        await ReferenceBootstrap.RunAsync(admin, input, owner => Context(owner, connection));
        return 0;
    }
    if (input.Action == "Verify")
    {
        await DatabaseAcceptance.VerifyAsync(admin, owner => Context(owner, connection));
        return 0;
    }
    if (input.Action == "Roles")
    {
        await DatabaseAcceptance.RolesAsync(admin, input, owner => Context(owner, connection));
        return 0;
    }
    if (input.Action == "Probe")
    {
        foreach (var owner in new[] { "catalog", "orders", "customers", "identity", "billing" })
        {
            await using var inventory = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name=@schema), "
                + "(SELECT count(*) FROM information_schema.tables WHERE table_schema=@schema AND table_type='BASE TABLE')", admin);
            inventory.Parameters.AddWithValue("schema", owner);
            await using var rows = await inventory.ExecuteReaderAsync();
            await rows.ReadAsync();
            Console.WriteLine($"Inventory: {owner}; exists={rows.GetBoolean(0)}; tables={rows.GetInt64(1)}.");
        }
        return 0;
    }
    // Refuse to silently migrate an unrelated pre-existing application schema.
    foreach (var owner in new[] { "catalog", "orders", "customers", "identity", "billing" })
    {
        await using var check = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema=@schema) "
            + "AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema=@schema AND table_name='__EFMigrationsHistory')", admin);
        check.Parameters.AddWithValue("schema", owner);
        if ((bool)(await check.ExecuteScalarAsync())!) throw new InvalidOperationException("Existing untracked owner schema; manual audit required.");
    }
    // citext may already be installed under Supabase's extensions schema. Include it
    // in this migration principal's session search_path without touching runtime grants.
    await using (var search = new NpgsqlCommand("SET search_path TO public, extensions", admin)) await search.ExecuteNonQueryAsync();
    var migrationConnection = new NpgsqlConnectionStringBuilder(connection) { SearchPath = "public,extensions" }.ConnectionString;
    foreach (var owner in new[] { "catalog", "orders", "customers", "identity", "billing" })
    {
        await using var db = Context(owner, migrationConnection);
        await db.Database.MigrateAsync();
        if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new InvalidOperationException("Pending migrations remain.");
        var applied = (await db.Database.GetAppliedMigrationsAsync()).Count();
        if (applied != db.Database.GetMigrations().Count()) throw new InvalidOperationException("Migration history mismatch.");
        Console.WriteLine($"PASS: {owner} migrations/history ({applied}).");
    }
    return 0;
}
catch (Exception error)
{
    if (error is CloudAcceptanceException safe) Console.Error.WriteLine("Request failure: " + safe.Message);
    Console.Error.WriteLine("Acceptance stage: " + CloudWorkflow.Stage);
    // Method names only: diagnostic location without exception messages, arguments or SQL.
    foreach (var frame in new System.Diagnostics.StackTrace(error.GetBaseException()).GetFrames()?.Take(6)
        ?? Array.Empty<System.Diagnostics.StackFrame>())
        Console.Error.WriteLine("Location: " + frame.GetMethod()?.DeclaringType?.FullName + "." + frame.GetMethod()?.Name);
    // Never print exception message/stack/connection/SQL parameters from a cloud job.
    var postgres = error.GetBaseException() as PostgresException;
    Console.Error.WriteLine("FAIL: staging preparation. Type=" + error.GetBaseException().GetType().Name
        + (postgres is null ? "" : "; SQLSTATE=" + postgres.SqlState));
    return 1;
}

static DbContext Context(string owner, string connection) => owner switch
{
    "catalog" => new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection,
        x => x.MigrationsHistoryTable("__EFMigrationsHistory", owner)).Options),
    "orders" => new OrdersDbContext(new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(connection,
        x => x.MigrationsHistoryTable("__EFMigrationsHistory", owner)).Options),
    "customers" => new CustomersDbContext(new DbContextOptionsBuilder<CustomersDbContext>().UseNpgsql(connection,
        x => x.MigrationsHistoryTable("__EFMigrationsHistory", owner)).Options),
    "identity" => new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection,
        x => x.MigrationsHistoryTable("__EFMigrationsHistory", owner)).Options),
    "billing" => new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>().UseNpgsql(connection,
        x => x.MigrationsHistoryTable("__EFMigrationsHistory", owner)).Options),
    _ => throw new InvalidOperationException("Unknown owner.")
};

internal sealed class Input
{
    public string Action { get; set; } = "Probe";
    public string ProjectRef { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 5432;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public Dictionary<string, string> RolePasswords { get; set; } = new();
    public string StorageServiceKey { get; set; } = "";
    public string StorageReceipt { get; set; } = "";
    public string SourceConnection { get; set; } = "";
    public bool RuntimePooling { get; set; } = true;
    public bool RuntimeNoReset { get; set; }
    public string CloudHostSigningKey { get; set; } = "";
    public string CloudHostDataProtectionCertificate { get; set; } = "";
    public string CloudHostAcceptancePassword { get; set; } = "";
    public override string ToString() => "Staging input (redacted)";
}
