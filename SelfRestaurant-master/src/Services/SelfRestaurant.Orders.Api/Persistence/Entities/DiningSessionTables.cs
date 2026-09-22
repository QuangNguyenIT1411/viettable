namespace SelfRestaurant.Orders.Api.Persistence.Entities;

public sealed class DiningSessionTables
{
    public int DiningSessionTableID { get; set; }
    public string DiningSessionCode { get; set; } = null!;
    public int TableID { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
