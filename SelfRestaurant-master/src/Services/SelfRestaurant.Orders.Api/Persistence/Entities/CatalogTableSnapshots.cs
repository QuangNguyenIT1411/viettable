namespace SelfRestaurant.Orders.Api.Persistence.Entities;

public sealed class CatalogTableSnapshots
{
    public int TableId { get; set; }
    public int BranchId { get; set; }
    public int TableNumber { get; set; }
    public string? QrCode { get; set; }
    public bool IsActive { get; set; }
    public int StatusId { get; set; }
    public string? StatusCode { get; set; }
    public string? StatusName { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
}
