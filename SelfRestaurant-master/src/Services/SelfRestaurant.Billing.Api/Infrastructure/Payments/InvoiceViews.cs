using System.Globalization;
using System.Text;
using System.Text.Json;
using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Infrastructure.Payments;

/// <summary>One dish line as sold (from the invoice's ItemsJson snapshot; old rows use PascalCase keys).</summary>
public sealed record InvoiceLine(int DishId, string DishName, int Quantity, long UnitPrice, long LineTotal);

/// <summary>
/// What the app (history, detail, the "PHIẾU THANH TOÁN" PDF) and the cashier receipt read for one invoice (owner 05/11).
/// Everything comes from the invoice snapshot; an invoice from before 05/11 (SnapshotVersion 0) falls back to its quote
/// and payment for what they still hold, and leaves the rest null so the screens hide those rows instead of guessing.
/// </summary>
public static class InvoiceViews
{
    public static IReadOnlyList<InvoiceLine> Lines(string? itemsJson)
    {
        if (string.IsNullOrWhiteSpace(itemsJson)) return Array.Empty<InvoiceLine>();
        try
        {
            using var doc = JsonDocument.Parse(itemsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<InvoiceLine>();
            return doc.RootElement.EnumerateArray().Select(x => new InvoiceLine(
                Int(x, "dishId"), Text(x, "dishName") ?? "Món", Int(x, "quantity"),
                Money(Dec(x, "unitPrice")), Money(Dec(x, "lineTotal")))).ToArray();
        }
        catch (JsonException) { return Array.Empty<InvoiceLine>(); }
    }

    public static object Map(Invoice invoice, CheckoutQuote? quote, Payment? payment, Voucher? voucher = null)
    {
        var lines = Lines(invoice.ItemsJson);
        var snapshot = invoice.SnapshotVersion >= 1;
        return new
        {
            invoice.InvoiceId,
            invoice.InvoiceCode,
            invoice.CheckoutQuoteId,
            invoice.PaymentId,
            invoice.DiningSessionId,
            diningSessionCode = quote?.DiningSessionCode,
            invoice.SnapshotVersion,
            branchId = invoice.BranchId ?? quote?.BranchId,
            branchName = invoice.BranchNameSnapshot ?? quote?.BranchNameSnapshot,
            branchAddress = invoice.BranchAddressSnapshot ?? quote?.BranchAddressSnapshot,
            tableId = quote?.TableId,
            tableName = invoice.TableNameSnapshot ?? quote?.TableNameSnapshot,
            sessionStartedAtUtc = invoice.SessionStartedAtUtc ?? quote?.SessionStartedAtUtc,
            paidAtUtc = invoice.PaidAtUtc ?? payment?.CompletedAtUtc,
            invoice.IssuedAtUtc,
            cashierName = invoice.CashierNameSnapshot,
            items = lines.Select(x => new { x.DishId, x.DishName, x.Quantity, x.UnitPrice, x.LineTotal }),
            itemCount = lines.Sum(x => x.Quantity),
            subtotal = Money(invoice.Subtotal),
            discount = Money(invoice.Discount),
            promotionName = quote?.PromotionName,
            promotionDiscount = Money(invoice.PromotionDiscount),
            voucherCode = voucher?.Code,
            voucherName = voucher?.Name,
            voucherDiscount = Money(snapshot ? invoice.VoucherDiscount : quote?.VoucherDiscount ?? 0),
            pointsUsed = snapshot ? invoice.PointsUsed : quote?.PointsApplied ?? 0,
            pointsDiscount = Money(snapshot ? invoice.PointsDiscount : quote?.PointsDiscount ?? 0),
            // Percentages only exist on snapshot invoices; before 05/11 no service fee or VAT was ever charged.
            serviceFeePercent = snapshot ? invoice.ServiceFeePercent : (decimal?)null,
            serviceFeeAmount = Money(invoice.ServiceFeeAmount),
            vatPercent = snapshot ? invoice.VatPercent : (decimal?)null,
            vatAmount = Money(invoice.VatAmount),
            depositApplied = Money(invoice.DepositApplied),
            grossTotal = Money(invoice.TotalAmount + invoice.DepositApplied),
            totalAmount = Money(invoice.TotalAmount),
            amountPaid = Money(invoice.TotalAmount),
            paymentCode = payment?.PaymentCode,
            paymentChannel = payment?.Channel,
            paymentMethod = invoice.PaymentMethodSnapshot ?? payment?.Method,
            paymentLocation = payment?.Location,
            paymentStatus = payment?.Status,
            paymentCompletedAtUtc = payment?.CompletedAtUtc,
            bankTransactionRef = invoice.BankTransactionRef ?? (IsTransfer(payment?.Method) ? payment?.ProviderTransactionId : null),
            pointsEarned = invoice.PointsEarned
        };
    }

    public static bool IsTransfer(string? method) =>
        method is not null && (method.Equals("TRANSFER", StringComparison.OrdinalIgnoreCase)
            || method.Equals("BANK_TRANSFER", StringComparison.OrdinalIgnoreCase) || method.Equals("QR", StringComparison.OrdinalIgnoreCase));

    /// <summary>"yyyy-MM" → [first day, first day of next month) in UTC+7 (the restaurant's day), as UTC.</summary>
    public static (DateTime FromUtc, DateTime ToUtc)? MonthRange(string? month)
    {
        if (string.IsNullOrWhiteSpace(month)
            || !DateTime.TryParseExact(month.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first))
            return null;
        var from = new DateTime(first.Year, first.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-7);
        return (from, from.AddMonths(1));
    }

    /// <summary>Keyset cursor "issuedTicks:invoiceId" (base64url); null when absent or unreadable.</summary>
    public static (DateTime IssuedAtUtc, int InvoiceId)? ReadCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor.Replace('-', '+').Replace('_', '/').PadRight((cursor.Length + 3) / 4 * 4, '=')));
            var parts = raw.Split(':');
            return parts.Length == 2 && long.TryParse(parts[0], out var ticks) && int.TryParse(parts[1], out var id)
                ? (new DateTime(ticks, DateTimeKind.Utc), id) : null;
        }
        catch (FormatException) { return null; }
    }

    public static string WriteCursor(DateTime issuedAtUtc, int invoiceId) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{issuedAtUtc.Ticks}:{invoiceId}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static long Money(decimal value) => checked((long)Math.Round(value, 0, MidpointRounding.AwayFromZero));
    private static JsonElement? Prop(JsonElement x, string camel)
    {
        if (x.ValueKind != JsonValueKind.Object) return null;
        if (x.TryGetProperty(camel, out var v)) return v;
        var pascal = char.ToUpperInvariant(camel[0]) + camel[1..];
        return x.TryGetProperty(pascal, out var p) ? p : null;
    }
    private static int Int(JsonElement x, string name) => Prop(x, name) is { ValueKind: JsonValueKind.Number } v && v.TryGetInt32(out var i) ? i : 0;
    private static decimal Dec(JsonElement x, string name) => Prop(x, name) is { ValueKind: JsonValueKind.Number } v && v.TryGetDecimal(out var d) ? d : 0;
    private static string? Text(JsonElement x, string name) => Prop(x, name) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;
}
