using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Infrastructure.Payments;

/// <summary>
/// SePay bank-transfer webhooks. Environment variables only (never in code or logs):
/// Payments__SePay__Enabled, Payments__SePay__WebhookSecret (HMAC-SHA256 secret key),
/// Payments__SePay__AccountNumbers (comma separated: VA and the Test-mode account).
/// </summary>
public sealed class SePayOptions
{
    public bool Enabled { get; set; }
    public string? WebhookSecret { get; set; }
    public string? AccountNumbers { get; set; }
    public int MaxClockSkewSeconds { get; set; } = 300;

    public IReadOnlySet<string> AllowedAccounts() =>
        (AccountNumbers ?? string.Empty).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

/// <summary>X-SePay-Signature: "sha256=" + hex(HMAC-SHA256(secret, "{X-SePay-Timestamp}.{raw body}")).</summary>
public static class SePaySignature
{
    public static string Compute(string secret, string timestamp, string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp + "." + rawBody))).ToLowerInvariant();
    }

    public static bool IsValid(string? secret, string? timestamp, string? signature, string rawBody, DateTimeOffset now, int maxSkewSeconds)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature)) return false;
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)) return false;
        if (Math.Abs(now.ToUnixTimeSeconds() - seconds) > maxSkewSeconds) return false; // replay protection
        var expected = Encoding.ASCII.GetBytes(Compute(secret, timestamp, rawBody));
        var given = Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }
}

/// <summary>Finds "VT B{branch} TT{payment}" (BankTransferQrService.PaymentContent) in a bank's transfer content.</summary>
public static partial class SePayContentParser
{
    public sealed record PaymentReference(int BranchId, int PaymentId);

    // Banks add prefixes/suffixes, drop spaces or punctuation; digits must not run on into other digits.
    [GeneratedRegex(@"(?<![A-Z0-9])VT ?B(\d{1,6}) ?TT(\d{1,9})(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex Reference();

    /// <summary>Null when there is no reference or more than one different reference (ambiguous: cashier decides).</summary>
    public static PaymentReference? Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        var normalized = Regex.Replace(content.ToUpperInvariant(), "[^A-Z0-9]+", " ");
        var found = Reference().Matches(normalized)
            .Select(m => (Branch: int.TryParse(m.Groups[1].Value, out var b) ? b : 0, Payment: int.TryParse(m.Groups[2].Value, out var p) ? p : 0))
            .Where(x => x.Branch > 0 && x.Payment > 0).Distinct().ToList();
        return found.Count == 1 ? new PaymentReference(found[0].Branch, found[0].Payment) : null;
    }
}

public sealed record SePayTransfer(string Id, string AccountNumber, string? SubAccount, string TransferType, decimal Amount,
    string Content, string? ReferenceCode, string? TransactionDate)
{
    public static SePayTransfer? FromJson(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            string? Text(string name) => root.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null
                ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText()) : null;
            var id = Text("id");
            var amountOk = root.TryGetProperty("transferAmount", out var amountValue) && amountValue.TryGetDecimal(out _);
            if (string.IsNullOrWhiteSpace(id) || !amountOk) return null;
            return new SePayTransfer(id.Trim(), Text("accountNumber") ?? string.Empty, Text("subAccount"), Text("transferType") ?? string.Empty,
                amountValue.GetDecimal(), Text("content") ?? string.Empty, Text("referenceCode"), Text("transactionDate"));
        }
        catch (JsonException) { return null; }
    }
}

/// <summary>The decision for one transfer, before anything is written. Pure, so every rule is unit tested.</summary>
public static class SePayMatchRules
{
    public const string AutoConfirmed = "AUTO_CONFIRMED";
    public const string AmountTooLow = "AMOUNT_TOO_LOW";
    public const string NoMatch = "NO_MATCH";
    public const string BranchMismatch = "BRANCH_MISMATCH";
    public const string NotTransfer = "NOT_TRANSFER";
    public const string PaymentNotPending = "PAYMENT_NOT_PENDING";

    /// <summary>Confirms only when the code matches a pending transfer payment of that branch and enough money arrived.</summary>
    public static string Decide(SePayContentParser.PaymentReference? reference, Payment? payment, int? paymentBranchId, decimal amount)
    {
        if (reference is null || payment is null) return NoMatch;
        if (paymentBranchId != reference.BranchId) return BranchMismatch;
        if (!Controllers.MobileCheckoutController.IsTransferPayment(payment.Channel, payment.Method)) return NotTransfer;
        if (payment.Status != "PENDING" || payment.Currency != "VND") return PaymentNotPending;
        return amount >= payment.Amount ? AutoConfirmed : AmountTooLow;
    }
}

/// <summary>What the gateway needs to notify the customer and the cashier screen. No transfer content.</summary>
public sealed record SePayWebhookResult(int StatusCode, string Outcome, int? BranchId = null, int? PaymentId = null,
    int? CustomerId = null, int? DiningSessionId = null, decimal? Amount = null, string? PaymentStatus = null);

/// <summary>Seam over PaymentFinalizationService (its transactions/locks need a real database).</summary>
public interface ISePayPaymentConfirmer
{
    Task<Payment> ConfirmAsync(int paymentId, string providerTransactionId, CancellationToken ct);
}

public sealed class SePayPaymentConfirmer(PaymentFinalizationService finalization) : ISePayPaymentConfirmer
{
    public Task<Payment> ConfirmAsync(int paymentId, string providerTransactionId, CancellationToken ct) =>
        finalization.ConfirmAsync(paymentId, null, null, providerTransactionId, ct, providerName: "SEPAY");
}

public sealed class SePayWebhookService(BillingDbContext db, IOptions<SePayOptions> options, ISePayPaymentConfirmer confirmer,
    ILogger<SePayWebhookService> logger)
{
    public const string Disabled = "DISABLED";
    public const string Ignored = "IGNORED";
    public const string Duplicate = "DUPLICATE";
    public const string Unauthorized = "UNAUTHORIZED";

    public async Task<SePayWebhookResult> ProcessAsync(string rawBody, string? timestamp, string? signature, DateTimeOffset now, CancellationToken ct)
    {
        var settings = options.Value;
        // Signature first, also while disabled: a wrong signature never reaches any table.
        if (string.IsNullOrEmpty(settings.WebhookSecret))
        {
            if (!settings.Enabled) { logger.LogInformation("SePay webhook ignored: feature disabled and no secret configured."); return new(200, Disabled); }
            logger.LogError("SePay webhook refused: Payments:SePay:WebhookSecret is not configured.");
            return new(503, Unauthorized);
        }
        if (!SePaySignature.IsValid(settings.WebhookSecret, timestamp, signature, rawBody, now, settings.MaxClockSkewSeconds))
        {
            logger.LogWarning("SePay webhook rejected: invalid signature or timestamp.");
            return new(401, Unauthorized);
        }
        if (!settings.Enabled)
        {
            logger.LogInformation("SePay webhook signature valid; feature disabled, nothing processed.");
            return new(200, Disabled);
        }
        var transfer = SePayTransfer.FromJson(rawBody);
        if (transfer is null) { logger.LogWarning("SePay webhook ignored: unreadable payload."); return new(400, Ignored); }
        if (!string.Equals(transfer.TransferType, "in", StringComparison.OrdinalIgnoreCase)) return new(200, Ignored);
        var allowed = settings.AllowedAccounts();
        if (!allowed.Contains(transfer.AccountNumber) && !(transfer.SubAccount is { Length: > 0 } sub && allowed.Contains(sub)))
        {
            logger.LogWarning("SePay webhook {SePayId} ignored: receiving account is not configured.", transfer.Id);
            return new(200, Ignored);
        }

        var row = await db.BankTransferEvents.SingleOrDefaultAsync(x => x.Provider == "SEPAY" && x.ProviderTransactionId == transfer.Id, ct);
        if (row is not null && row.Outcome != "RECEIVED")
        {
            logger.LogInformation("SePay webhook {SePayId} already processed ({Outcome}).", transfer.Id, row.Outcome);
            return new(200, Duplicate, row.BranchId, row.PaymentId);
        }
        if (row is null)
        {
            row = new BankTransferEvent { ProviderTransactionId = transfer.Id, ReferenceCode = Cut(transfer.ReferenceCode, 100),
                AccountNumber = Cut(transfer.AccountNumber, 40) ?? string.Empty, SubAccount = Cut(transfer.SubAccount, 40),
                Amount = transfer.Amount, Content = Cut(transfer.Content, 500) ?? string.Empty,
                TransactionDate = Cut(transfer.TransactionDate, 40), ReceivedAtUtc = now.UtcDateTime };
            db.BankTransferEvents.Add(row);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                // Another delivery of the same transaction won the unique key: that one does the work.
                db.ChangeTracker.Clear();
                logger.LogInformation("SePay webhook {SePayId} delivered twice at once; second delivery ignored.", transfer.Id);
                return new(200, Duplicate);
            }
        }

        var reference = SePayContentParser.Parse(transfer.Content);
        Payment? payment = null;
        int? branchId = null;
        if (reference is not null)
        {
            var found = await (from p in db.Payments.AsNoTracking()
                               join q in db.CheckoutQuotes.AsNoTracking() on p.CheckoutQuoteId equals q.CheckoutQuoteId
                               where p.PaymentId == reference.PaymentId
                               select new { Payment = p, q.BranchId }).FirstOrDefaultAsync(ct);
            payment = found?.Payment;
            branchId = found?.BranchId;
        }
        var outcome = SePayMatchRules.Decide(reference, payment, branchId, transfer.Amount);
        Payment? confirmed = null;
        if (outcome == SePayMatchRules.AutoConfirmed)
        {
            try { confirmed = await confirmer.ConfirmAsync(payment!.PaymentId, "SEPAY:" + transfer.Id, ct); }
            catch (PaymentDomainException) { outcome = SePayMatchRules.PaymentNotPending; }
        }

        row.Outcome = outcome;
        row.Overpaid = outcome == SePayMatchRules.AutoConfirmed && transfer.Amount > payment!.Amount;
        row.PaymentId = payment?.PaymentId;
        row.BranchId = branchId ?? reference?.BranchId;
        row.ProcessedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("SePay webhook {SePayId}: {Outcome} for payment {PaymentId}, amount {Amount}.",
            transfer.Id, outcome, row.PaymentId, transfer.Amount);

        return confirmed is null
            ? new(200, outcome, row.BranchId, row.PaymentId)
            : new(200, outcome, row.BranchId, confirmed.PaymentId, confirmed.CustomerId, confirmed.DiningSessionId, confirmed.Amount, confirmed.Status);
    }

    private static string? Cut(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];
}
