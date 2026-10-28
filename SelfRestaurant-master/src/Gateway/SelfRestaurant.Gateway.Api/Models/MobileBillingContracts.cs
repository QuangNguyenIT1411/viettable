namespace SelfRestaurant.Gateway.Api.Models;

public sealed record ApplyVoucherRequest(int? VoucherId);
public sealed record PromotionChoiceRequest(bool Use);
public sealed record ApplyLoyaltyRequest(int Points);
public sealed record PaymentRequestBody(int CheckoutQuoteId);
public sealed record CreateMobilePaymentBody(string Channel, string Method, string? Location);
public sealed record CreateSplitBillBody(int CheckoutQuoteId, IReadOnlyList<long>? Amounts);
public sealed record PaySplitBillBody(int SplitBillPartId, string Channel, string Method, string? Location);
