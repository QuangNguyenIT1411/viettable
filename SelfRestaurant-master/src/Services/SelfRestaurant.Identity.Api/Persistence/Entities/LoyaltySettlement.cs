namespace SelfRestaurant.Identity.Api.Persistence.Entities;

public sealed class LoyaltySettlement
{
    public string SettlementKey { get; set; } = "";
    public int CustomerId { get; set; }
    public int PointsUsed { get; set; }
    public decimal AmountPaid { get; set; }
    public string ResponseJson { get; set; } = "";
}
