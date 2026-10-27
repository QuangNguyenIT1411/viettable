using System.Text.Json;
using Microsoft.Extensions.Options;
using SelfRestaurant.Deposits;
using SelfRestaurant.Gateway.Api.Models;

namespace SelfRestaurant.Gateway.Api.Services;

/// <summary>A booking's deposit as Billing sees it, with the VietQR while it still waits for the money.</summary>
public sealed record ReservationDepositView(ReservationDto Reservation, JsonElement Deposit, JsonElement? Transfer)
{
    public string Status => Text(Deposit, "status") ?? DepositRules.Pending;
    public static string? Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

/// <summary>
/// Keeps a booking (Customers) and its deposit (Billing) in step (docs/DEPOSIT_PLAN.md). The booking says whether a
/// deposit is owed; Billing says whether the money arrived. Every read repairs a step a failed call left out: a deposit
/// row never created, a paid deposit whose booking still waits, or a closed booking whose deposit was not settled.
/// </summary>
public sealed class ReservationDepositCoordinator(BillingClient billing, CustomersClient customers, IOptions<DepositOptions> options,
    ILogger<ReservationDepositCoordinator> logger)
{
    private static readonly string[] ClosedStatuses = ["Cancelled", "Rejected", "NoShow"];

    public DepositOptions Options => options.Value;

    /// <summary>What the app shows before booking (the amounts are never hard-coded in the app).</summary>
    public object Policy() => new
    {
        enabled = Options.Enabled,
        minGuests = Options.MinGuests,
        perGuestVnd = (long)Options.PerGuestVnd,
        paymentMinutes = Options.PaymentMinutes,
        refundCutoffHours = Options.RefundCutoffHours,
    };

    public decimal RequiredAmount(int partySize) => DepositRules.RequiredAmount(Options, partySize);

    /// <summary>Creates the Billing row of a booking waiting for its deposit (idempotent on the reservation).</summary>
    public async Task EnsureCreatedAsync(ReservationDto reservation, CancellationToken ct)
    {
        if (!string.Equals(reservation.Status, DepositRules.ReservationPendingDeposit, StringComparison.OrdinalIgnoreCase)
            || reservation.DepositAmount is not > 0 || reservation.DepositDueAtUtc is null || reservation.CustomerId is not > 0) return;
        await billing.CreateDepositAsync(new
        {
            reservation.ReservationId, CustomerId = reservation.CustomerId.Value, reservation.BranchId, reservation.PartySize,
            Amount = reservation.DepositAmount.Value, ReservedAtUtc = reservation.ReservedAt, DueAtUtc = reservation.DepositDueAtUtc.Value,
        }, ct);
    }

    /// <summary>The deposit of a booking after repairing any missed step; null when the booking needs none.</summary>
    public async Task<ReservationDepositView?> SyncAsync(ReservationDto reservation, bool includeQr, CancellationToken ct)
    {
        if (reservation.DepositAmount is not > 0) return null;
        var found = await billing.GetDepositAsync(reservation.ReservationId, includeQr, ct);
        if (found is null && string.Equals(reservation.Status, DepositRules.ReservationPendingDeposit, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureCreatedAsync(reservation, ct);
            found = await billing.GetDepositAsync(reservation.ReservationId, includeQr, ct);
        }
        if (found is null) return null;
        var deposit = found.Value.GetProperty("deposit");
        var status = ReservationDepositView.Text(deposit, "status");

        if (status == DepositRules.Paid
            && string.Equals(reservation.Status, DepositRules.ReservationPendingDeposit, StringComparison.OrdinalIgnoreCase))
        {
            try { reservation = await customers.MarkDepositPaidAsync(reservation.ReservationId, ct) ?? reservation; }
            catch (ApiClientException ex) when (ex.StatusCode == 409)
            {
                // Paid in time, but the booking closed just before: the money is a refund to do, never kept.
                logger.LogWarning("Deposit of reservation {ReservationId} paid after the booking closed.", reservation.ReservationId);
                deposit = await billing.CloseDepositAsync(reservation.ReservationId, new
                {
                    ReservationStatus = DepositRules.ReservationClosedBeforePayment, ReservedAtUtc = reservation.ReservedAt, ClosedAtUtc = DateTime.UtcNow,
                }, ct);
                reservation = await customers.GetReservationByIdAsync(reservation.ReservationId, ct) ?? reservation;
            }
        }
        else if (status is DepositRules.Pending or DepositRules.Paid
                 && ClosedStatuses.Contains(reservation.Status, StringComparer.OrdinalIgnoreCase))
        {
            deposit = await billing.CloseDepositAsync(reservation.ReservationId, new
            {
                ReservationStatus = reservation.Status, ReservedAtUtc = reservation.ReservedAt,
                ClosedAtUtc = reservation.CancelledAtUtc ?? reservation.StatusChangedAtUtc ?? reservation.UpdatedAtUtc ?? DateTime.UtcNow,
            }, ct);
        }

        JsonElement? transfer = ReservationDepositView.Text(deposit, "status") == DepositRules.Pending
            && found.Value.TryGetProperty("transfer", out var qr) && qr.ValueKind == JsonValueKind.Object ? qr : null;
        return new ReservationDepositView(reservation, deposit, transfer);
    }

    /// <summary>
    /// After SePay: Billing recorded deposit money for this booking; move the booking on (PendingDeposit → Pending, or
    /// a refund to do when it closed first). Any failure other than "booking gone" is thrown, so the webhook answers
    /// 5xx and SePay delivers again; every step here is idempotent.
    /// </summary>
    public async Task<ReservationDepositView?> RecordPaymentAsync(int reservationId, CancellationToken ct)
    {
        var booking = await customers.GetReservationByIdAsync(reservationId, ct);
        return booking is null ? null : await SyncAsync(booking, false, ct);
    }

    /// <summary>Support FAQ: the deposit question only while the feature is on, with the configured numbers.</summary>
    public static System.Text.Json.Nodes.JsonNode WithDepositFaq(JsonElement faqs, DepositOptions options) =>
        WithDepositFaq(faqs, options.Enabled, options.MinGuests, options.PerGuestVnd, options.PaymentMinutes, options.RefundCutoffHours);

    public static System.Text.Json.Nodes.JsonNode WithDepositFaq(JsonElement faqs, bool enabled, int minGuests, decimal perGuestVnd,
        int paymentMinutes, int refundCutoffHours)
    {
        var options = new { Enabled = enabled, MinGuests = minGuests, PerGuestVnd = perGuestVnd, PaymentMinutes = paymentMinutes, RefundCutoffHours = refundCutoffHours };
        var node = System.Text.Json.Nodes.JsonNode.Parse(faqs.ValueKind is JsonValueKind.Object ? faqs.GetRawText() : "{\"items\":[]}")!;
        if (!options.Enabled || options.MinGuests <= 0 || options.PerGuestVnd <= 0) return node;
        var items = node["items"] as System.Text.Json.Nodes.JsonArray ?? new System.Text.Json.Nodes.JsonArray();
        node["items"] = items;
        var perGuest = options.PerGuestVnd.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));
        items.Add(new System.Text.Json.Nodes.JsonObject
        {
            ["faqId"] = "deposit",
            ["question"] = "Khi nào cần đặt cọc?",
            ["answer"] = $"Đặt bàn từ {options.MinGuests} khách cần đặt cọc {perGuest}đ/khách, chuyển khoản VietQR trong "
                + $"{options.PaymentMinutes} phút để giữ bàn (quá hạn đặt bàn tự hủy). Tiền cọc được trừ vào hóa đơn khi dùng bữa. "
                + $"Hủy trước giờ đến từ {options.RefundCutoffHours} giờ hoặc nhà hàng từ chối: được hoàn cọc (nhà hàng hoàn thủ công); "
                + "hủy sát giờ hoặc không đến: mất cọc.",
        });
        return node;
    }

    /// <summary>Best effort after a booking closed: a failure is repaired by the next read.</summary>
    public async Task SyncQuietlyAsync(ReservationDto? reservation, CancellationToken ct)
    {
        if (reservation?.DepositAmount is not > 0) return;
        try { await SyncAsync(reservation, false, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { logger.LogWarning("Deposit sync skipped for reservation {ReservationId} ({ErrorType}).", reservation.ReservationId, ex.GetType().Name); }
    }

    /// <summary>Staff list: deposits of a branch (all branches when null) with their bookings, repaired first.</summary>
    public async Task<IReadOnlyList<object>> StaffListAsync(int? branchId, CancellationToken ct)
    {
        var deposits = await billing.ListDepositsAsync(branchId, ct);
        var ids = deposits.Select(x => x.GetProperty("reservationId").GetInt32()).Distinct().ToArray();
        var bookings = (await customers.GetReservationsByIdsAsync(ids, ct)).ToDictionary(x => x.ReservationId);
        var items = new List<object>();
        foreach (var deposit in deposits)
        {
            var current = deposit;
            var reservationId = deposit.GetProperty("reservationId").GetInt32();
            bookings.TryGetValue(reservationId, out var booking);
            if (booking is not null && NeedsSync(ReservationDepositView.Text(deposit, "status"), booking.Status))
            {
                try
                {
                    var view = await SyncAsync(booking, false, ct);
                    if (view is not null) { current = view.Deposit; booking = view.Reservation; }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { logger.LogWarning("Deposit sync skipped for reservation {ReservationId} ({ErrorType}).", reservationId, ex.GetType().Name); }
            }
            items.Add(new
            {
                deposit = current,
                reservation = booking is null ? null : new
                {
                    booking.ReservationId, booking.ReservationCode, booking.CustomerName, booking.PhoneNumber,
                    booking.ReservedAt, booking.PartySize, booking.Status, booking.StatusReason,
                },
            });
        }
        return items;
    }

    private static bool NeedsSync(string? depositStatus, string reservationStatus) =>
        (depositStatus == DepositRules.Paid && string.Equals(reservationStatus, DepositRules.ReservationPendingDeposit, StringComparison.OrdinalIgnoreCase))
        || (depositStatus is DepositRules.Pending or DepositRules.Paid && ClosedStatuses.Contains(reservationStatus, StringComparer.OrdinalIgnoreCase));
}
