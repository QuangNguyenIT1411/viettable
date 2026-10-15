using SelfRestaurant.Gateway.Api.Models;
using System.Text.Json;

namespace SelfRestaurant.Gateway.Api.Services;

public sealed class BillingClient : ApiClientBase
{
    private readonly IConfiguration _configuration;
    public BillingClient(HttpClient http, IConfiguration configuration) : base(http)
    {
        _configuration = configuration;
    }

    public Task<JsonElement> CashierMobilePaymentAsync(StaffSessionUserDto staff, int? paymentId, CancellationToken ct) =>
        CashierRequestAsync(staff, paymentId.HasValue ? HttpMethod.Post : HttpMethod.Get,
            "/api/internal/cashier/onsite-payments" + (paymentId.HasValue ? $"/{paymentId.Value}/confirm" : ""), "Không thể xác nhận giao dịch.", ct);

    /// <summary>VietQR for the counter screen when the cashier checks out one order by bank transfer.</summary>
    public Task<JsonElement> CashierOrderTransferQrAsync(StaffSessionUserDto staff, int orderId, decimal amount, CancellationToken ct) =>
        CashierRequestAsync(staff, HttpMethod.Get,
            $"/api/internal/cashier/transfer-qr?orderId={orderId}&amount={amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            "Không tạo được mã QR chuyển khoản.", ct);

    /// <summary>VietQR of a pending app payment by bank transfer, shown on the counter screen.</summary>
    public Task<JsonElement> CashierPaymentTransferQrAsync(StaffSessionUserDto staff, int paymentId, CancellationToken ct) =>
        CashierRequestAsync(staff, HttpMethod.Get, $"/api/internal/cashier/onsite-payments/{paymentId}/transfer-qr",
            "Không tạo được mã QR chuyển khoản.", ct);

    // Billing's cashier endpoints trust a short-lived token that carries the cashier's employee and branch.
    private async Task<JsonElement> CashierRequestAsync(StaffSessionUserDto staff, HttpMethod method, string path, string fallbackMessage, CancellationToken ct)
    {
        var key = _configuration["MobileJwt:SigningKey"] ?? throw new InvalidOperationException("Signing key unavailable");
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken("SelfRestaurant.Gateway", "SelfRestaurant.Billing.Cashier",
            new[] { new System.Security.Claims.Claim("employeeId", staff.EmployeeId.ToString()),
                new System.Security.Claims.Claim("branchId", staff.BranchId.ToString()),
                new System.Security.Claims.Claim("role", staff.RoleCode.ToUpperInvariant()) },
            expires: DateTime.UtcNow.AddSeconds(30), signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key)), Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token));
        using var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
            throw new ApiClientException(body.ValueKind == JsonValueKind.Object && body.TryGetProperty("message", out var message) ? message.GetString()! : fallbackMessage,
                (int)response.StatusCode, body.ValueKind == JsonValueKind.Object && body.TryGetProperty("code", out var code) ? code.GetString() : "cashier_payment_failed");
        return body;
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

    /// <summary>Money actually collected per day and branch (the same figures as the cashier report), split by payment method.</summary>
    public Task<AdminRevenueReportDto?> GetAdminRevenueReportAsync(int days, int? branchId, CancellationToken cancellationToken) =>
        GetAsync<AdminRevenueReportDto>($"/api/admin/reports/revenue?days={Math.Clamp(days, 1, 365)}{(branchId is > 0 ? $"&branchId={branchId}" : "")}", cancellationToken);

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
    public Task<JsonElement> GetMobilePaymentOptionsAsync(CancellationToken ct) => GetRequiredAsync("/api/internal/mobile/payment-options", "payment_options_unavailable", ct);
    public Task<JsonElement> GetMobileTransferQrAsync(int paymentId, int customerId, CancellationToken ct) => GetRequiredAsync($"/api/internal/mobile/payments/{paymentId}/transfer-qr?customerId={customerId}", "payment_not_found", ct);
    public Task<JsonElement> GetMobilePaymentAsync(int paymentId, int customerId, CancellationToken ct) => GetRequiredAsync($"/api/internal/mobile/payments/{paymentId}?customerId={customerId}", "payment_not_found", ct);
    public Task<JsonElement> RetryMobilePaymentAsync(int paymentId, object request, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/payments/{paymentId}/retry", request, ct);
    public Task<JsonElement> GetSessionInvoiceAsync(int sessionId, int customerId, CancellationToken ct) => GetRequiredAsync($"/api/internal/mobile/dining-sessions/{sessionId}/invoice?customerId={customerId}", "invoice_not_found", ct);
    public Task<JsonElement> GetInvoiceAsync(int invoiceId, int customerId, CancellationToken ct) => GetRequiredAsync($"/api/internal/mobile/invoices/{invoiceId}?customerId={customerId}", "invoice_not_found", ct);
    public Task<JsonElement> GetCustomerInvoicesAsync(int customerId, int limit, CancellationToken ct) =>
        GetRequiredAsync($"/api/internal/mobile/invoices?customerId={customerId}&limit={limit}", "invoice_history_unavailable", ct);

    private async Task<JsonElement> GetRequiredAsync(string path, string code, CancellationToken ct)
    {
        var value = await GetAsync<JsonElement>(path, ct);
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new ApiClientException("Không tìm thấy dữ liệu thanh toán.", 404, code);
        return value;
    }
    public Task<JsonElement> CreateSplitBillAsync(int sessionId, object request, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/dining-sessions/{sessionId}/split-bill", request, ct);
    public Task<JsonElement> GetSessionSplitBillAsync(int sessionId, int customerId, CancellationToken ct) => GetRequiredAsync($"/api/internal/mobile/dining-sessions/{sessionId}/split-bill?customerId={customerId}", "split_bill_not_found", ct);
    public Task<JsonElement> GetSplitBillAsync(int splitBillId, int customerId, CancellationToken ct) => GetRequiredAsync($"/api/internal/mobile/split-bills/{splitBillId}?customerId={customerId}", "split_bill_not_found", ct);
    public Task<JsonElement> PaySplitBillAsync(int splitBillId, object request, CancellationToken ct) => PostForAsync<object, JsonElement>($"/api/internal/mobile/split-bills/{splitBillId}/payments", request, ct);
}
