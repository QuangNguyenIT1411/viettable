using SelfRestaurant.Billing.Api.Persistence.Entities;

namespace SelfRestaurant.Billing.Api.Infrastructure.Payments;

public interface IPaymentProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<ProviderPaymentSession> CreateAsync(Payment payment, string clientIpAddress, CancellationToken cancellationToken);
    ProviderWebhookResult VerifyWebhook(IReadOnlyDictionary<string, string> values);
}

public sealed record ProviderPaymentSession(
    string ProviderName,
    string ProviderReference,
    string PaymentUrl,
    string Currency,
    DateTime ExpiresAtUtc);

public sealed record ProviderWebhookResult(
    bool SignatureValid,
    string? ProviderReference,
    string? ProviderTransactionId,
    decimal? Amount,
    string? Currency,
    string? PaymentStatus,
    string? FailureCode);

public sealed class PaymentProviderUnavailableException(string message) : InvalidOperationException(message);
