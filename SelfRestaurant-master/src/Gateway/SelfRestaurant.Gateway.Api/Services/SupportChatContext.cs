using System.Globalization;
using System.Text.Json;
using SelfRestaurant.Deposits;
using SelfRestaurant.Gateway.Api.Models;

namespace SelfRestaurant.Gateway.Api.Services;

/// <summary>
/// The items a support conversation can be about (owner 06/11), always read from the customer's real data: their
/// bookings, deposits, bank-transfer payments, invoices. Also answers "đã được xác nhận chưa?" for deposits and
/// transfers, and the live status staff see next to a conversation.
/// </summary>
public sealed class SupportChatContext(CustomersClient customers, BillingClient billing)
{
    public sealed record Option(string ContextType, int ContextId, int BranchId, string Label, string? Detail);

    /// <summary>Paid / used / to refund / refunded: the deposit transfer reached the restaurant.</summary>
    public static bool DepositConfirmed(string? status) =>
        status is DepositRules.Paid or DepositRules.Applied or DepositRules.RefundRequired or DepositRules.Refunded or DepositRules.Forfeited;

    public static string DepositStatusText(string? status) => status switch
    {
        DepositRules.Pending => "Chưa nhận được tiền cọc",
        DepositRules.Paid => "Đã nhận tiền cọc",
        DepositRules.Applied => "Đã trừ cọc vào hoá đơn",
        DepositRules.RefundRequired => "Cần hoàn cọc",
        DepositRules.Refunded => "Đã hoàn cọc",
        DepositRules.Forfeited => "Không hoàn cọc",
        DepositRules.Expired => "Quá hạn đặt cọc",
        DepositRules.Cancelled => "Đã huỷ",
        _ => status ?? "",
    };

    public static string PaymentStatusText(string? status) => status switch
    {
        "SUCCEEDED" => "Đã xác nhận",
        "PENDING" or "CREATED" or "PROCESSING" => "Chưa xác nhận",
        "CANCELLED" => "Đã huỷ",
        "FAILED" => "Không thành công",
        _ => status ?? "",
    };

    public static string ReservationStatusText(string? status) => status switch
    {
        "Pending" => "Chờ xác nhận", "Confirmed" => "Đã xác nhận", "PendingDeposit" => "Chờ đặt cọc",
        "CheckingIn" => "Đang check-in", "CheckedIn" => "Đã check-in", "Completed" => "Hoàn tất",
        "Cancelled" => "Đã huỷ", "NoShow" => "Không đến", "Rejected" => "Bị từ chối", "WaitlistHold" => "Đang giữ bàn",
        _ => status ?? "",
    };

    private static readonly TimeZoneInfo Zone = ReservationRules.RestaurantTimeZone;

    private static string Local(DateTime utc, string format) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone).ToString(format, CultureInfo.InvariantCulture);

    public static string Money(long value) => value.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', '.') + "đ";

    /// <summary>What the customer can pick for a topic's context type (newest first).</summary>
    public async Task<IReadOnlyList<Option>> OptionsAsync(int customerId, string contextType, CancellationToken ct)
    {
        switch (contextType)
        {
            case "RESERVATION":
            {
                var rows = await customers.GetCustomerReservationsAsync(customerId, ct);
                return rows.Where(r => r.Status != "WaitlistHold").OrderByDescending(r => r.ReservedAt).Take(20)
                    .Select(r => new Option("RESERVATION", r.ReservationId, r.BranchId,
                        $"Đặt bàn #{r.ReservationId} · {Local(r.ReservedAt, "HH:mm dd/MM")}",
                        $"{ReservationStatusText(r.Status)} · {r.PartySize} khách")).ToList();
            }
            case "DEPOSIT":
            {
                var rows = (await customers.GetCustomerReservationsAsync(customerId, ct))
                    .Where(r => r.DepositAmount is > 0).OrderByDescending(r => r.ReservedAt).Take(10).ToList();
                var list = new List<Option>();
                foreach (var r in rows)
                {
                    var deposit = await billing.GetDepositAsync(r.ReservationId, false, ct);
                    if (deposit is null) continue;
                    var d = Dep(deposit.Value);
                    list.Add(new Option("DEPOSIT", r.ReservationId, r.BranchId,
                        $"Cọc đặt bàn #{r.ReservationId} · {Money(Long(d, "amount"))}",
                        $"{DepositStatusText(Str(d, "status"))} · {Local(r.ReservedAt, "HH:mm dd/MM")}"));
                }
                return list;
            }
            case "PAYMENT":
            {
                var data = await billing.GetSupportTransfersAsync(customerId, ct);
                return Items(data).Select(p => new Option("PAYMENT", Int(p, "paymentId"), Int(p, "branchId"),
                    $"Chuyển khoản {Money(Long(p, "amount"))} · {Str(p, "transferContent")}",
                    $"{PaymentStatusText(Str(p, "status"))} · {Local(Date(p, "createdAtUtc") ?? DateTime.UtcNow, "HH:mm dd/MM")}")).ToList();
            }
            case "INVOICE":
            {
                var data = await billing.GetCustomerInvoicesAsync(customerId, 20, ct);
                return Items(data).Select(i =>
                {
                    var at = Date(i, "paidAtUtc") ?? Date(i, "issuedAtUtc");
                    return new Option("INVOICE", Int(i, "invoiceId"), Int(i, "branchId"),
                        $"Hoá đơn #{Int(i, "invoiceId")} · {Money(Long(i, "totalAmount"))}",
                        $"{Str(i, "branchName")}{(at is null ? "" : " · " + Local(at.Value, "HH:mm dd/MM"))}");
                }).ToList();
            }
            default:
                return Array.Empty<Option>();
        }
    }

    public sealed record PaymentCheck(bool Confirmed, DateTime? ConfirmedAtUtc, long Amount, string? TransferContent, string Status, string StatusText, int BranchId);

    /// <summary>Topic 1/2: has the transfer been confirmed? Null when the item is not the customer's.</summary>
    public async Task<PaymentCheck?> CheckAsync(int customerId, string topic, int contextId, CancellationToken ct)
    {
        if (topic == "DEPOSIT_TRANSFER")
        {
            var reservation = (await customers.GetCustomerReservationsAsync(customerId, ct)).FirstOrDefault(r => r.ReservationId == contextId);
            if (reservation is null) return null;
            var deposit = await billing.GetDepositAsync(contextId, false, ct);
            if (deposit is null) return null;
            var d = Dep(deposit.Value);
            var status = Str(d, "status") ?? "";
            return new PaymentCheck(DepositConfirmed(status), Date(d, "paidAtUtc"), Long(d, "amount"), Str(d, "transferContent"), status,
                DepositStatusText(status), reservation.BranchId);
        }
        if (topic == "BILL_TRANSFER")
        {
            var p = Items(await billing.GetSupportTransfersAsync(customerId, ct)).FirstOrDefault(x => Int(x, "paymentId") == contextId);
            if (p.ValueKind == JsonValueKind.Undefined) return null;
            var status = Str(p, "status") ?? "";
            return new PaymentCheck(status == "SUCCEEDED", Date(p, "completedAtUtc"), Long(p, "amount"), Str(p, "transferContent"), status,
                PaymentStatusText(status), Int(p, "branchId"));
        }
        return null;
    }

    public sealed record LiveContext(string Type, int Id, string Label, string Status, string? ActionLabel, string? ActionTab);

    /// <summary>What staff see next to a conversation: the item's status now and where to handle it.</summary>
    public async Task<LiveContext?> LiveAsync(int customerId, string contextType, int? contextId, CancellationToken ct)
    {
        if (contextId is not > 0) return null;
        var id = contextId.Value;
        try
        {
            switch (contextType)
            {
                case "RESERVATION":
                {
                    var r = await customers.GetReservationByIdAsync(id, ct);
                    if (r is null || r.CustomerId != customerId) return null;
                    return new LiveContext(contextType, id, $"Đặt bàn #{id} · {Local(r.ReservedAt, "HH:mm dd/MM")} · {r.PartySize} khách",
                        ReservationStatusText(r.Status), "Mở Đặt bàn", "dat-ban");
                }
                case "DEPOSIT":
                {
                    var r = await customers.GetReservationByIdAsync(id, ct);
                    if (r is null || r.CustomerId != customerId) return null;
                    var deposit = await billing.GetDepositAsync(id, false, ct);
                    var status = deposit is null ? null : Str(Dep(deposit.Value), "status");
                    var amount = deposit is null ? 0 : Long(Dep(deposit.Value), "amount");
                    var action = status == DepositRules.RefundRequired ? "Cần hoàn cọc" : "Mở Đặt cọc";
                    return new LiveContext(contextType, id, $"Cọc đặt bàn #{id} · {Money(amount)} · ND: {DepositRules.TransferContent(id)}",
                        DepositStatusText(status), action, "dat-ban");
                }
                case "PAYMENT":
                {
                    var p = Items(await billing.GetSupportTransfersAsync(customerId, ct)).FirstOrDefault(x => Int(x, "paymentId") == id);
                    if (p.ValueKind == JsonValueKind.Undefined) return null;
                    var status = Str(p, "status");
                    return new LiveContext(contextType, id, $"Chuyển khoản {Money(Long(p, "amount"))} · ND: {Str(p, "transferContent")}",
                        PaymentStatusText(status), status == "SUCCEEDED" ? null : "Chuyển khoản cần xử lý", "chuyen-khoan");
                }
                case "INVOICE":
                {
                    var i = await billing.GetInvoiceAsync(id, customerId, ct);
                    return new LiveContext(contextType, id, $"Hoá đơn #{id} · {Money(Long(i, "totalAmount"))}",
                        Str(i, "paymentStatus") == "SUCCEEDED" ? "Đã thanh toán" : (Str(i, "paymentStatus") ?? ""), null, null);
                }
            }
        }
        catch (ApiClientException) { return null; }
        return null;
    }

    // ---- JSON helpers ----
    /// <summary>Billing answers { deposit, transfer }; the deposit row itself.</summary>
    private static JsonElement Dep(JsonElement v) =>
        v.ValueKind == JsonValueKind.Object && v.TryGetProperty("deposit", out var d) && d.ValueKind == JsonValueKind.Object ? d : v;

    public static IEnumerable<JsonElement> Items(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Array) return data.EnumerateArray().ToArray();
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            return items.EnumerateArray().ToArray();
        return Array.Empty<JsonElement>();
    }

    public static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static int Int(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : 0;

    public static long Long(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? (v.TryGetInt64(out var l) ? l : (long)Math.Round(v.GetDecimal())) : 0;

    public static DateTime? Date(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            && DateTime.TryParse(v.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)
            ? d : null;
}
