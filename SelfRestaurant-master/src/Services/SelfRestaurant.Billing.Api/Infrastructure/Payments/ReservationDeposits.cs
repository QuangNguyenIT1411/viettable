using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;
using SelfRestaurant.Deposits;

namespace SelfRestaurant.Billing.Api.Infrastructure.Payments;

public sealed record CreateDepositRequest(int ReservationId, int CustomerId, int BranchId, int PartySize, decimal Amount,
    DateTime ReservedAtUtc, DateTime DueAtUtc);

public sealed record CloseDepositRequest(string ReservationStatus, DateTime ReservedAtUtc, DateTime ClosedAtUtc);

/// <summary>
/// Reservation deposits (docs/DEPOSIT_PLAN.md). Billing owns the money: create, read (a waiting deposit past its
/// 15 minutes reads EXPIRED), close by the 2-hour rule, list for staff, "Đã hoàn cọc", and the paid deposit that comes
/// off the final bill. Every state change of one deposit runs under the lock "deposit:{reservationId}".
/// </summary>
public sealed class ReservationDepositService(BillingDbContext db, IOptions<DepositOptions> options)
{
    public static string LockName(int reservationId) => "deposit:" + reservationId;

    public async Task<ReservationDeposit> CreateAsync(CreateDepositRequest request, CancellationToken ct)
    {
        if (request.ReservationId <= 0 || request.CustomerId <= 0 || request.BranchId <= 0 || request.PartySize <= 0 || request.Amount <= 0)
            throw new PaymentDomainException("validation_failed", "Dữ liệu đặt cọc không hợp lệ.", 400);
        var existing = await db.ReservationDeposits.FirstOrDefaultAsync(x => x.ReservationId == request.ReservationId, ct);
        if (existing is not null) return existing; // a retried create (e.g. the app re-reading the deposit) keeps the first row
        var now = DateTime.UtcNow;
        var deposit = new ReservationDeposit
        {
            ReservationId = request.ReservationId, CustomerId = request.CustomerId, BranchId = request.BranchId,
            PartySize = request.PartySize, Amount = decimal.Round(request.Amount, 0), Status = DepositRules.Pending,
            ReservedAtUtc = Utc(request.ReservedAtUtc), DueAtUtc = Utc(request.DueAtUtc), CreatedAtUtc = now, UpdatedAtUtc = now,
        };
        db.ReservationDeposits.Add(deposit);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Two creates at once: the unique reservation index let only one in.
            db.ChangeTracker.Clear();
            return await db.ReservationDeposits.SingleAsync(x => x.ReservationId == request.ReservationId, ct);
        }
        return deposit;
    }

    /// <summary>The deposit of a booking, a waiting one past its time stored as EXPIRED; null when there is none.</summary>
    public async Task<ReservationDeposit?> GetAsync(int reservationId, CancellationToken ct)
    {
        var deposit = await db.ReservationDeposits.FirstOrDefaultAsync(x => x.ReservationId == reservationId, ct);
        if (deposit is null || !DepositRules.IsOverdue(deposit.Status, deposit.DueAtUtc, DateTime.UtcNow)) return deposit;
        return await ChangeAsync(reservationId, d =>
        {
            if (!DepositRules.IsOverdue(d.Status, d.DueAtUtc, DateTime.UtcNow)) return false;
            d.Status = DepositRules.Expired;
            d.ClosedReason = "Hết hạn đặt cọc";
            return true;
        }, ct);
    }

    /// <summary>The booking closed without a meal (cancelled, rejected, no-show): apply the 2-hour rule once.</summary>
    public Task<ReservationDeposit?> CloseAsync(int reservationId, CloseDepositRequest request, CancellationToken ct) =>
        ChangeAsync(reservationId, d =>
        {
            var next = DepositRules.OnReservationClosed(d.Status, request.ReservationStatus, Utc(request.ReservedAtUtc),
                Utc(request.ClosedAtUtc), options.Value);
            if (next is null) return false;
            d.Status = next.Value.Status;
            d.ClosedReason = next.Value.Reason;
            return true;
        }, ct);

    /// <summary>Staff refunded the money by hand ("Đã hoàn cọc"). Only a deposit waiting for a refund; repeats are no-ops.</summary>
    public async Task<ReservationDeposit> MarkRefundedAsync(int depositId, int employeeId, int? branchId, CancellationToken ct)
    {
        var reservationId = await db.ReservationDeposits.Where(x => x.ReservationDepositId == depositId)
            .Select(x => (int?)x.ReservationId).FirstOrDefaultAsync(ct)
            ?? throw new PaymentDomainException("deposit_not_found", "Không tìm thấy khoản đặt cọc.", 404);
        ReservationDeposit? found = null;
        var changed = await ChangeAsync(reservationId, d =>
        {
            found = d;
            if (branchId is > 0 && d.BranchId != branchId)
                throw new PaymentDomainException("branch_mismatch", "Khoản cọc không thuộc chi nhánh của bạn.", 403);
            if (d.Status == DepositRules.Refunded) return false;
            if (d.Status != DepositRules.RefundRequired)
                throw new PaymentDomainException("deposit_not_refundable", "Khoản cọc này không ở trạng thái cần hoàn.");
            d.Status = DepositRules.Refunded;
            d.RefundedAtUtc = DateTime.UtcNow;
            d.RefundedByEmployeeId = employeeId > 0 ? employeeId : null;
            return true;
        }, ct);
        return changed ?? found!;
    }

    public Task<List<ReservationDeposit>> ListAsync(int? branchId, IReadOnlyCollection<int>? reservationIds, CancellationToken ct)
    {
        var query = db.ReservationDeposits.AsNoTracking();
        if (branchId is > 0) query = query.Where(x => x.BranchId == branchId.Value);
        if (reservationIds is { Count: > 0 }) query = query.Where(x => reservationIds.Contains(x.ReservationId));
        return query.OrderByDescending(x => x.ReservationDepositId).Take(200).ToListAsync(ct);
    }

    /// <summary>The paid deposit of a booking that may come off a bill (null when none or already used).</summary>
    public static Task<ReservationDeposit?> PaidForReservationAsync(BillingDbContext db, int? reservationId, CancellationToken ct) =>
        reservationId is > 0
            ? db.ReservationDeposits.AsNoTracking().FirstOrDefaultAsync(x => x.ReservationId == reservationId.Value && x.Status == DepositRules.Paid, ct)
            : Task.FromResult<ReservationDeposit?>(null);

    /// <summary>Puts a paid deposit on a new quote (before voucher and points are chosen).</summary>
    public static void AttachToQuote(CheckoutQuote quote, ReservationDeposit? deposit)
    {
        if (deposit is null) return;
        quote.ReservationDepositId = deposit.ReservationDepositId;
        quote.DepositAmount = deposit.Amount;
        Recalculate(quote);
    }

    /// <summary>Total = subtotal − voucher − points − deposit (deposit capped at what is left, never below 0).</summary>
    public static void Recalculate(CheckoutQuote quote)
    {
        var afterDiscounts = Math.Max(0, quote.Subtotal - quote.VoucherDiscount - quote.PointsDiscount);
        quote.DepositApplied = DepositRules.Applicable(quote.DepositAmount, afterDiscounts);
        quote.TotalAmount = afterDiscounts - quote.DepositApplied;
        quote.UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>The bill is paid: its deposit is used up. Caller owns the transaction (PaymentFinalizationService).</summary>
    public static async Task MarkAppliedAsync(BillingDbContext db, CheckoutQuote quote, CancellationToken ct)
    {
        if (quote.ReservationDepositId is not > 0 || quote.DepositApplied <= 0) return;
        var deposit = await db.ReservationDeposits.FirstOrDefaultAsync(x => x.ReservationDepositId == quote.ReservationDepositId.Value, ct);
        if (deposit is null || deposit.Status != DepositRules.Paid) return;
        deposit.Status = DepositRules.Applied;
        deposit.AppliedCheckoutQuoteId = quote.CheckoutQuoteId;
        deposit.AppliedAmount = quote.DepositApplied;
        deposit.AppliedAtUtc = deposit.UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>One change of one deposit under its lock; the action returns false when nothing changes.</summary>
    private async Task<ReservationDeposit?> ChangeAsync(int reservationId, Func<ReservationDeposit, bool> change, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (tx is not null) await SelfRestaurant.CloudCompatibility.TransactionLocks.AcquireAsync(db, LockName(reservationId), ct);
        var deposit = await db.ReservationDeposits.FirstOrDefaultAsync(x => x.ReservationId == reservationId, ct);
        if (deposit is null) return null;
        if (tx is not null) await db.Entry(deposit).ReloadAsync(ct);
        if (change(deposit))
        {
            deposit.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        if (tx is not null) await tx.CommitAsync(ct);
        return deposit;
    }

    private static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    public static object Map(ReservationDeposit d, DateTime nowUtc) => new
    {
        d.ReservationDepositId, d.ReservationId, d.CustomerId, d.BranchId, d.PartySize,
        amount = Money(d.Amount), status = DepositRules.Effective(d.Status, d.DueAtUtc, nowUtc),
        d.ReservedAtUtc, d.DueAtUtc, paidAmount = d.PaidAmount is null ? (long?)null : Money(d.PaidAmount.Value), d.PaidAtUtc,
        appliedAmount = d.AppliedAmount is null ? (long?)null : Money(d.AppliedAmount.Value), d.AppliedAtUtc,
        d.ClosedReason, d.RefundedAtUtc, d.RefundedByEmployeeId, transferContent = DepositRules.TransferContent(d.ReservationId),
        d.CreatedAtUtc,
    };

    private static long Money(decimal value) => checked((long)Math.Round(value, 0, MidpointRounding.AwayFromZero));
}
