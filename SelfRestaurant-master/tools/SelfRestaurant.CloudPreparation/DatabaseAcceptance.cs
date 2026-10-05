using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

internal static class DatabaseAcceptance
{
    internal static readonly string[] Owners = ["catalog", "orders", "customers", "identity", "billing"];

    public static async Task VerifyAsync(NpgsqlConnection admin, Func<string, DbContext> context)
    {
        await using (var policies = new NpgsqlCommand("SELECT count(*) FROM pg_policies WHERE schemaname='storage' AND tablename='objects' "
            + "AND cmd IN ('INSERT','ALL') AND roles && ARRAY['anon','public']::name[] AND permissive='PERMISSIVE'", admin))
        {
            if ((long)(await policies.ExecuteScalarAsync())! != 0)
                throw new InvalidOperationException("Anonymous Storage write policy exists; explicit audit required.");
            Console.WriteLine("PASS: no permissive anon/PUBLIC Storage INSERT/ALL policies.");
        }
        await using (var tx = await admin.BeginTransactionAsync())
        {
            await Execute("SET LOCAL ROLE anon", admin, tx);
            try
            {
                await using var attempt = new NpgsqlCommand("INSERT INTO storage.objects(bucket_id,name) VALUES('viettable-staging-media',@n)", admin, tx);
                attempt.Parameters.AddWithValue("n", "qa-policy-denial-" + Guid.NewGuid().ToString("N"));
                await attempt.ExecuteNonQueryAsync();
                throw new InvalidOperationException("Anon Storage INSERT unexpectedly permitted; transaction rolled back.");
            }
            catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.InsufficientPrivilege)
            {
                Console.WriteLine("PASS: actual anon-role Storage INSERT denied SQLSTATE42501; policy transaction rolled back.");
            }
            finally { await tx.RollbackAsync(); }
        }
        foreach (var owner in Owners)
        {
            await using var db = context(owner);
            if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new InvalidOperationException("Pending migrations.");
            var tables = db.Model.GetRelationalModel().Tables;
            var columns = 0;
            foreach (var table in tables)
            {
                if (table.Schema != owner) throw new InvalidOperationException("Cross-owner model table.");
                foreach (var column in table.Columns)
                {
                    await using var query = new NpgsqlCommand("SELECT format_type(a.atttypid,a.atttypmod), a.attnotnull "
                        + "FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace "
                        + "WHERE n.nspname=@s AND c.relname=@t AND a.attname=@c AND NOT a.attisdropped", admin);
                    query.Parameters.AddWithValue("s", owner); query.Parameters.AddWithValue("t", table.Name);
                    query.Parameters.AddWithValue("c", column.Name);
                    await using var rows = await query.ExecuteReaderAsync();
                    if (!await rows.ReadAsync()) throw new InvalidOperationException("Physical column missing.");
                    var actual = rows.GetString(0);
                    var expected = column.StoreType.Replace("character varying", "varchar", StringComparison.Ordinal);
                    actual = actual.Replace("character varying", "varchar", StringComparison.Ordinal);
                    if (actual != expected || rows.GetBoolean(1) != !column.IsNullable)
                        throw new InvalidOperationException($"Column mismatch: {owner}.{table.Name}.{column.Name}.");
                    columns++;
                }
                foreach (var constraint in table.UniqueConstraints)
                {
                    await using var check = new NpgsqlCommand("SELECT count(*) FROM pg_constraint k "
                        + "JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace "
                        + "WHERE n.nspname=@s AND c.relname=@t AND k.conname=@name AND k.contype IN ('p','u') AND k.convalidated", admin);
                    check.Parameters.AddWithValue("s", owner); check.Parameters.AddWithValue("t", table.Name);
                    check.Parameters.AddWithValue("name", constraint.Name);
                    if ((long)(await check.ExecuteScalarAsync())! != 1) throw new InvalidOperationException("Missing key constraint.");
                }
                foreach (var fk in table.ForeignKeyConstraints)
                {
                    await using var check = new NpgsqlCommand("SELECT count(*) FROM pg_constraint k "
                        + "JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace "
                        + "WHERE n.nspname=@s AND c.relname=@t AND k.conname=@name AND k.contype='f' AND k.convalidated", admin);
                    check.Parameters.AddWithValue("s", owner); check.Parameters.AddWithValue("t", table.Name);
                    check.Parameters.AddWithValue("name", fk.Name);
                    if ((long)(await check.ExecuteScalarAsync())! != 1) throw new InvalidOperationException("Missing validated FK.");
                }
                foreach (var index in table.Indexes)
                {
                    await using var check = new NpgsqlCommand("SELECT count(*) FROM pg_index i "
                        + "JOIN pg_class idx ON idx.oid=i.indexrelid JOIN pg_namespace n ON n.oid=idx.relnamespace "
                        + "WHERE n.nspname=@s AND idx.relname=@name AND i.indisvalid", admin);
                    check.Parameters.AddWithValue("s", owner); check.Parameters.AddWithValue("name", index.Name);
                    if ((long)(await check.ExecuteScalarAsync())! != 1) throw new InvalidOperationException("Missing valid index.");
                }
            }
            Console.WriteLine($"PASS: {owner} physical model parity; tables={tables.Count()}; columns={columns}; types/nullability/PK/FK/unique/indexes.");
        }
    }

    public static async Task RolesAsync(NpgsqlConnection admin, Input input, Func<string, DbContext> context)
    {
        foreach (var owner in Owners)
        {
            if (!input.RolePasswords.TryGetValue(owner, out var password) || password.Length < 40
                || password.Any(c => !char.IsAsciiLetterOrDigit(c))) throw new InvalidOperationException("Generated role secret missing.");
            var role = "viettable_" + owner;
            await using var tx = await admin.BeginTransactionAsync();
            await using var exists = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname=@r)", admin, tx);
            exists.Parameters.AddWithValue("r", role);
            var found = (bool)(await exists.ExecuteScalarAsync())!;
            if (!found)
            {
                // Identifiers are from fixed allowlist; password is cryptographically generated hex only.
                await Execute($"CREATE ROLE {role} LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD '{password}'", admin, tx);
            }
            await using var flags = new NpgsqlCommand("SELECT rolcanlogin AND NOT (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls) "
                + "AND NOT EXISTS(SELECT 1 FROM pg_auth_members WHERE member=pg_roles.oid) FROM pg_roles WHERE rolname=@r", admin, tx);
            flags.Parameters.AddWithValue("r", role);
            if ((bool)(await flags.ExecuteScalarAsync())! != true) throw new InvalidOperationException("Unexpected existing role privileges; audit required.");
            await Execute($"GRANT USAGE ON SCHEMA {owner} TO {role}; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {owner} TO {role}; "
                + $"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA {owner} TO {role}; "
                + $"REVOKE ALL ON {owner}.\"__EFMigrationsHistory\" FROM {role}; GRANT SELECT ON {owner}.\"__EFMigrationsHistory\" TO {role}", admin, tx);
            await tx.CommitAsync();
            var runtime = new NpgsqlConnectionStringBuilder(admin.ConnectionString)
            {
                Username = role + "." + input.ProjectRef, Password = password, Port = 6543,
                MaxPoolSize = 2, MinPoolSize = 0, MaxAutoPrepare = 0, SearchPath = ""
            }.ConnectionString;
            await using var login = new NpgsqlConnection(runtime);
            await login.OpenAsync();
            await using var db = context(owner);
            var table = db.Model.GetRelationalModel().Tables.First().Name;
            await Execute($"SELECT 1 FROM {owner}.\"{table}\" LIMIT 1", login);
            var denies = 0;
            foreach (var foreign in Owners.Where(x => x != owner))
            {
                await using var other = context(foreign);
                var target = other.Model.GetRelationalModel().Tables.First().Name;
                try
                {
                    await Execute($"SELECT 1 FROM {foreign}.\"{target}\" LIMIT 1", login);
                    throw new InvalidOperationException("Cross-schema read unexpectedly allowed.");
                }
                catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.InsufficientPrivilege) { denies++; }
            }
            Console.WriteLine($"PASS: {role} actual transaction-pooler login/owned SELECT; foreign SELECT denied={denies}/4 SQLSTATE42501.");
        }
    }

    private static async Task Execute(string sql, NpgsqlConnection connection, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }
}
