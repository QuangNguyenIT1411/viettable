using System.Text.RegularExpressions;

namespace SelfRestaurant.Backend.ContractTests;

public sealed class TraceabilityTests
{
    private static readonly Regex ScreenRow = new(@"^\| S(?<id>\d{2}) \|", RegexOptions.Multiline);
    private static readonly Regex RuleRow = new(@"^\| R(?<id>\d{2}) \|", RegexOptions.Multiline);

    [Fact]
    public void Mobile_traceability_contains_all_72_continuous_screens()
    {
        var document = ReadRepoFile("docs", "MOBILE_B0_TRACEABILITY.md");
        var ids = ScreenRow.Matches(document).Select(match => int.Parse(match.Groups["id"].Value)).ToArray();

        Assert.Equal(Enumerable.Range(1, 72), ids);
    }

    [Fact]
    public void Mobile_traceability_contains_all_27_continuous_rules()
    {
        var document = ReadRepoFile("docs", "MOBILE_B0_TRACEABILITY.md");
        var ids = RuleRow.Matches(document).Select(match => int.Parse(match.Groups["id"].Value)).ToArray();

        Assert.Equal(Enumerable.Range(1, 27), ids);
    }

    [Fact]
    public void Mobile_contract_reserves_versioned_gateway_and_error_conventions()
    {
        var document = ReadRepoFile("docs", "MOBILE_API_V1_CONTRACT.md");

        Assert.Contains("/api/mobile/v1", document, StringComparison.Ordinal);
        Assert.Contains("Idempotency-Key", document, StringComparison.Ordinal);
        Assert.Contains("X-Correlation-Id", document, StringComparison.Ordinal);
        Assert.Contains("MobileApiError", document, StringComparison.Ordinal);
        Assert.Contains("PAYMENT_REQUESTED", document, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SelfRestaurant.Microservices.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(new[] { directory!.FullName }.Concat(relativeSegments).ToArray()));
    }
}
