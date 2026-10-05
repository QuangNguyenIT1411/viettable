using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

internal static class ReferenceBootstrap
{
    // Catalog reference graph only. Never export identity/financial/QA transaction rows.
    private static readonly string[] Tables = ["Restaurants", "TableStatus", "Branches", "Categories", "Units", "Ingredients",
        "Dishes", "DiningTables", "Menus", "MenuCategory", "CategoryDish", "DishIngredients"];

    public static async Task RunAsync(NpgsqlConnection target, Input input, Func<string, DbContext> context)
    {
        var sourceConfig = new SqlConnectionStringBuilder(input.SourceConnection);
        if (sourceConfig.InitialCatalog != "RESTAURANT_CATALOG" || sourceConfig.DataSource != "host.docker.internal,1433")
            throw new InvalidOperationException("Reference source must be explicit local Catalog only.");
        await using var source = new SqlConnection(input.SourceConnection);
        await source.OpenAsync();
        await VerifySourceUtcAsync(source);
        var graph = new Dictionary<string, DataTable>();
        foreach (var name in Tables)
        {
            await using var command = new SqlCommand($"SELECT * FROM dbo.[{name}]", source);
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new DataTable(); rows.Load(reader); graph[name] = rows;
            if (rows.Columns.Contains("Name") && rows.Rows.Cast<DataRow>().Any(row =>
                (row["Name"] as string ?? "").StartsWith("QA TEMP", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Temporary Catalog reference row requires reviewed exclusion before import.");
        }
        // Current canonical demo inventory, checked BEFORE writing any row.
        if (graph["Branches"].Rows.Count != 3 || graph["Dishes"].Rows.Count != 52)
            throw new InvalidOperationException("Canonical branch/dish inventory changed; audit allowlist before bootstrap.");
        var statusRows = graph["TableStatus"].Rows.Cast<DataRow>();
        var available = statusRows.Single(row => (string)row["StatusCode"] == "AVAILABLE")["StatusID"];
        // Fresh staging has no sessions/orders: import table identity/QR/seats, not local occupancy.
        foreach (DataRow row in graph["DiningTables"].Rows)
        {
            row["CurrentOrderID"] = DBNull.Value;
            if (Convert.ToString(row["IsActive"]) == "True") row["StatusID"] = available;
        }
        var legacyZone = TimeZoneInfo.Utc; // Verified Docker/SQL source UTC and current API contract; not device timezone.
        await using var db = context("catalog");
        await using var tx = await target.BeginTransactionAsync();
        var inserted = 0;
        foreach (var name in Tables)
        {
            var model = db.Model.GetRelationalModel().Tables.Single(t => t.Name == name);
            var columns = model.Columns.ToArray();
            var key = model.PrimaryKey!.Columns.Select(c => c.Name).ToArray();
            foreach (DataRow row in graph[name].Rows)
            {
                var values = columns.Select(column =>
                {
                    if (!row.Table.Columns.Contains(column.Name))
                        throw new InvalidOperationException($"Source reference column missing: {name}.{column.Name}.");
                    var value = row[column.Name];
                    if (value is DateTime time && column.StoreType == "timestamp with time zone")
                        return (object)TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(time, DateTimeKind.Unspecified), legacyZone);
                    if (value is DateTime day && column.StoreType == "date") return DateOnly.FromDateTime(day);
                    return value;
                }).ToArray();
                var quoted = string.Join(",", columns.Select(c => '"' + c.Name + '"'));
                var parameters = string.Join(",", columns.Select((_, i) => "@p" + i));
                var conflict = string.Join(",", key.Select(k => '"' + k + '"'));
                await using var insert = new NpgsqlCommand($"INSERT INTO catalog.\"{name}\" ({quoted}) OVERRIDING SYSTEM VALUE VALUES ({parameters}) ON CONFLICT ({conflict}) DO NOTHING", target, tx);
                for (var i = 0; i < columns.Length; i++)
                {
                    var parameter = insert.Parameters.AddWithValue("p" + i, values[i]);
                    if (values[i] is DBNull)
                        parameter.NpgsqlDbType = columns[i].StoreType switch
                        {
                            "integer" => NpgsqlDbType.Integer, "boolean" => NpgsqlDbType.Boolean,
                            "timestamp with time zone" => NpgsqlDbType.TimestampTz, "date" => NpgsqlDbType.Date,
                            _ when columns[i].StoreType.StartsWith("numeric", StringComparison.Ordinal) => NpgsqlDbType.Numeric,
                            _ => NpgsqlDbType.Text
                        };
                }
                inserted += await insert.ExecuteNonQueryAsync();
            }
            foreach (var column in columns.Where(c => c.Name.EndsWith("ID", StringComparison.Ordinal)
                         && key.Contains(c.Name) && c.StoreType == "integer"))
            {
                await using var sequence = new NpgsqlCommand($"SELECT setval(pg_get_serial_sequence('catalog.\"{name}\"',@c), "
                    + $"GREATEST(COALESCE((SELECT max(\"{column.Name}\") FROM catalog.\"{name}\"),1),1),true)", target, tx);
                sequence.Parameters.AddWithValue("c", column.Name);
                await sequence.ExecuteNonQueryAsync();
            }
            await using var count = new NpgsqlCommand($"SELECT count(*) FROM catalog.\"{name}\"", target, tx);
            if ((long)(await count.ExecuteScalarAsync())! != graph[name].Rows.Count)
                throw new InvalidOperationException("Reference count mismatch; no overwrite permitted.");
        }
        // Fresh metadata only, use source-convention statuses and roles; no credentials/accounts.
        foreach (var (owner, name, id, code, label, data) in new[] {
            ("orders", "OrderStatus", "StatusID", "StatusCode", "StatusName", new[] { ("PENDING","Chờ xác nhận"),("CONFIRMED","Đã xác nhận"),("PREPARING","Đang chuẩn bị"),("READY","Sẵn sàng"),("SERVING","Đang phục vụ"),("COMPLETED","Hoàn tất"),("CANCELLED","Đã huỷ") }),
            ("identity", "EmployeeRoles", "RoleID", "RoleCode", "RoleName", new[] { ("ADMIN","Quản trị viên"),("MANAGER","Quản lý"),("CASHIER","Thu ngân"),("WAITER","Phục vụ"),("CHEF","Đầu bếp"),("KITCHEN_STAFF","Nhân viên bếp") }) })
        {
            foreach (var (status, text) in data)
            {
                await using var insert = new NpgsqlCommand($"INSERT INTO {owner}.\"{name}\" (\"{code}\",\"{label}\") SELECT @code,@name "
                    + $"WHERE NOT EXISTS(SELECT 1 FROM {owner}.\"{name}\" WHERE \"{code}\"=@code)", target, tx);
                insert.Parameters.AddWithValue("code", status); insert.Parameters.AddWithValue("name", text);
                await insert.ExecuteNonQueryAsync();
            }
        }
        await tx.CommitAsync();
        Console.WriteLine($"PASS: canonical Catalog reference graph bootstrap; inserted={inserted}; FK validation enforced; no account/transaction import.");
        Console.WriteLine($"Counts: Branches={graph["Branches"].Rows.Count}; Tables={graph["DiningTables"].Rows.Count}; Categories={graph["Categories"].Rows.Count}; MenuItems={graph["Dishes"].Rows.Count}.");
    }

    private static async Task VerifySourceUtcAsync(SqlConnection source)
    {
        await using var clock = new SqlCommand("SELECT DATEPART(TZOFFSET,SYSDATETIMEOFFSET())", source);
        if (Convert.ToInt32(await clock.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException("Source SQL UTC clock policy changed; audit required.");
    }

    public static async Task CorrectTimesAsync(NpgsqlConnection target, Input input, Func<string, DbContext> context)
    {
        var config=new SqlConnectionStringBuilder(input.SourceConnection);
        if(config.InitialCatalog!="RESTAURANT_CATALOG"||config.DataSource!="host.docker.internal,1433")
            throw new InvalidOperationException("Only explicit local reference source permitted.");
        await using var source=new SqlConnection(input.SourceConnection); await source.OpenAsync(); await VerifySourceUtcAsync(source);
        await using var db=context("catalog"); await using var tx=await target.BeginTransactionAsync();
        var previousZone=TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); var changed=0;
        foreach(var name in Tables)
        {
            var table=db.Model.GetRelationalModel().Tables.Single(x=>x.Name==name);
            var timeColumns=table.Columns.Where(c=>c.StoreType=="timestamp with time zone").ToArray();
            if(timeColumns.Length==0)continue;
            await using var query=new SqlCommand($"SELECT * FROM dbo.[{name}]",source);
            await using var reader=await query.ExecuteReaderAsync(); var rows=new DataTable();rows.Load(reader);
            var keys=table.PrimaryKey!.Columns.Select(c=>c.Name).ToArray();
            foreach(var column in timeColumns)
            {
                var values=new List<string>(); await using var update=new NpgsqlCommand{Connection=target,Transaction=tx};
                var index=0;
                foreach(DataRow row in rows.Rows)
                {
                    if(row[column.Name] is not DateTime raw)continue;
                    var parameters=new List<string>();
                    for(var k=0;k<keys.Length;k++)
                    {var p="k"+index+"_"+k;update.Parameters.AddWithValue(p,row[keys[k]]);parameters.Add("@"+p);}
                    var wall=DateTime.SpecifyKind(raw,DateTimeKind.Unspecified);
                    update.Parameters.AddWithValue("old"+index,NpgsqlDbType.TimestampTz,TimeZoneInfo.ConvertTimeToUtc(wall,previousZone));
                    update.Parameters.AddWithValue("new"+index,NpgsqlDbType.TimestampTz,TimeZoneInfo.ConvertTimeToUtc(wall,TimeZoneInfo.Utc));
                    parameters.Add("@old"+index);parameters.Add("@new"+index); values.Add("("+string.Join(",",parameters)+")");index++;
                }
                if(values.Count==0)continue;
                var aliases=keys.Select((_,k)=>"k"+k).Concat(new[]{"previous","corrected"});
                var matches=string.Join(" AND ",keys.Select((key,k)=>$"t.\"{key}\"=v.k{k}"));
                // CAS only the old imported value: preserve later legitimate API edits.
                update.CommandText=$"UPDATE catalog.\"{name}\" t SET \"{column.Name}\"=v.corrected FROM (VALUES {string.Join(",",values)}) v({string.Join(",",aliases)}) "
                    +$"WHERE {matches} AND t.\"{column.Name}\"=v.previous";
                changed+=await update.ExecuteNonQueryAsync();
            }
        }
        await tx.CommitAsync();
        Console.WriteLine($"PASS: reference timestamp correction to verified source UTC; fields corrected={changed}; later API edits preserved.");
    }
}
