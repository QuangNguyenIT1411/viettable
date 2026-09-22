namespace SelfRestaurant.Billing.Api.Persistence.Entities;

public sealed class OrderContextSnapshots
{
    public int OrderId { get; set; }
    public string? OrderCode { get; set; }
    public int? TableId { get; set; }
    public string? TableName { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
}
