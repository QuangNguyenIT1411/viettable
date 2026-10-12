namespace VietTable.CloudHost.Composition;

public static class ModuleStartup
{
    public static bool CanInitializeInParallel(IConfiguration configuration) =>
        configuration["VERCEL_ENV"] is "preview" or "production"
        && string.Equals(configuration["Database:Provider"], "Postgres", StringComparison.OrdinalIgnoreCase)
        && !configuration.GetValue<bool>("Database:ApplyMigrations")
        && (configuration["Database:SeedProfile"] ?? "None") == "None";
    // Vercel runtimes only (no persistent workers there). Migration/seed jobs and
    // non-Vercel hosts retain their order.
    public static async Task<T[]> InitializeAsync<T>(IReadOnlyList<string> owners,
        Func<string, Task<T>> initialize, bool parallel)
    {
        if (parallel) return await Task.WhenAll(owners.Select(initialize));
        var result = new List<T>(owners.Count);
        foreach (var owner in owners) result.Add(await initialize(owner));
        return result.ToArray();
    }
}
