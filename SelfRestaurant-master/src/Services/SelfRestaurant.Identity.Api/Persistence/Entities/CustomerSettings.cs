namespace SelfRestaurant.Identity.Api.Persistence.Entities;

public sealed class CustomerSettings
{
    public int CustomerId { get; set; }
    public bool PushNotificationsEnabled { get; set; } = true;
    public bool SmsReservationRemindersEnabled { get; set; } = true;
    public bool BiometricLoginEnabled { get; set; }
    public string LanguageCode { get; set; } = "vi-VN";
    public int? DefaultBranchId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Customers Customer { get; set; } = null!;
}
