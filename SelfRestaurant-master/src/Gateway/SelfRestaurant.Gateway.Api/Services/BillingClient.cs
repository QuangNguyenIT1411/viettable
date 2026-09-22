using SelfRestaurant.Gateway.Api.Models;
using System.Text.Json;

namespace SelfRestaurant.Gateway.Api.Services;

public sealed class BillingClient : ApiClientBase
{
    public BillingClient(HttpClient http) : base(http)
    {
    }

    public async Task<IReadOnlyList<CashierOrderDto>> GetCashierOrdersAsync(int branchId, CancellationToken cancellationToken)
    {
        var list = await GetAsync<IReadOnlyList<CashierOrderDto>>($"/api/branches/{branchId}/cashier/orders", cancellationToken);
        return list ?? Array.Empty<CashierOrderDto>();
    }

    public Task<CashierCheckoutResponse?> CheckoutAsync(int orderId, CashierCheckoutRequest request, CancellationToken cancellationToken) =>
        PostForAsync<CashierCheckoutRequest, CashierCheckoutResponse>($"/api/orders/{orderId}/checkout", request, cancellationToken);

    public async Task<IReadOnlyList<CashierBillSummaryDto>> GetBillsAsync(int employeeId, int branchId, DateOnly? date, int take, CancellationToken cancellationToken)
    {
        var dateQs = date is null ? string.Empty : $"&date={date:yyyy-MM-dd}";
        var list = await GetAsync<IReadOnlyList<CashierBillSummaryDto>>(
            $"/api/employees/{employeeId}/cashier/bills?branchId={branchId}&take={take}{dateQs}",
            cancellationToken);
        return list ?? Array.Empty<CashierBillSummaryDto>();
    }

    public Task<CashierReportDto?> GetReportAsync(int employeeId, int branchId, DateOnly? date, CancellationToken cancellationToken)
    {
        var dateQs = date is null ? string.Empty : $"&date={date:yyyy-MM-dd}";
        return GetAsync<CashierReportDto>($"/api/employees/{employeeId}/cashier/report?branchId={branchId}{dateQs}", cancellationToken);
    }

    public Task<BranchCashierReportDto?> GetBranchReportAsync(int branchId, DateOnly? date, CancellationToken cancellationToken)
    {
        var dateQs = date is null ? string.Empty : $"?date={date:yyyy-MM-dd}";
        return GetAsync<BranchCashierReportDto>($"/api/branches/{branchId}/cashier/report{dateQs}", cancellationToken);
    }

    public Task ResetDevTestStateAsync(CancellationToken cancellationToken) =>
        PostAsync<object>("/api/dev/reset-test-state", new { }, cancellationToken);

    public Task<JsonElement> CreateMobileQuoteAsync(int sessionId, object request, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/dining-sessions/{sessionId}/checkout/quote", request, ct);
    public async Task<IReadOnlyList<JsonElement>> GetMobileVouchersAsync(int customerId, CancellationToken ct) => await GetAsync<IReadOnlyList<JsonElement>>($"/api/internal/mobile/me/vouchers?customerId={customerId}", ct) ?? Array.Empty<JsonElement>();
    public Task<JsonElement> ApplyVoucherAsync(int quoteId, object request, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/checkout/quotes/{quoteId}/voucher", request, ct);
    public Task<JsonElement> ApplyLoyaltyAsync(int quoteId, object request, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/checkout/quotes/{quoteId}/loyalty", request, ct);
    public Task<JsonElement> RequestMobilePaymentAsync(int sessionId, object request, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/dining-sessions/{sessionId}/payment-request", request, ct);
    public Task<JsonElement> CancelMobilePaymentRequestAsync(int sessionId, object request, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/dining-sessions/{sessionId}/payment-request/cancel", request, ct);
    public Task<JsonElement> CreateMobilePaymentAsync(int quoteId, object request, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/checkout/quotes/{quoteId}/payments", request, ct);
    public Task<JsonElement> GetMobilePaymentAsync(int paymentId, int customerId, CancellationToken ct) => GetAsync<JsonElement>($"/api/internal/mobile/payments/{paymentId}?customerId={customerId}", ct);
    public Task<JsonElement> RetryMobilePaymentAsync(int paymentId, object request, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/payments/{paymentId}/retry", request, ct);
    public Task<JsonElement> GetSessionInvoiceAsync(int sessionId, int customerId, CancellationToken ct) => GetAsync<JsonElement>($"/api/internal/mobile/dining-sessions/{sessionId}/invoice?customerId={customerId}", ct);
    public Task<JsonElement> GetInvoiceAsync(int invoiceId, int customerId, CancellationToken ct) => GetAsync<JsonElement>($"/api/internal/mobile/invoices/{invoiceId}?customerId={customerId}", ct);
    public Task<JsonElement> CreateSplitBillAsync(int sessionId, object request, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/dining-sessions/{sessionId}/split-bill", request, ct);
    public Task<JsonElement> GetSessionSplitBillAsync(int sessionId, int customerId, CancellationToken ct) => GetAsync<JsonElement>($"/api/internal/mobile/dining-sessions/{sessionId}/split-bill?customerId={customerId}", ct);
    public Task<JsonElement> GetSplitBillAsync(int splitBillId, int customerId, CancellationToken ct) => GetAsync<JsonElement>($"/api/internal/mobile/split-bills/{splitBillId}?customerId={customerId}", ct);
    public Task<JsonElement> PaySplitBillAsync(int splitBillId, object request, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/split-bills/{splitBillId}/payments", request, ct);
}
