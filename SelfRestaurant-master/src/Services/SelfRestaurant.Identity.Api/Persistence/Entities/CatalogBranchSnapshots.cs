namespace SelfRestaurant.Identity.Api.Persistence.Entities;

public sealed class CatalogBranchSnapshots
{
    public int BranchId { get; set; }
    public string Name { get; set; } = null!;
    public string? Location { get; set; }
    public bool IsActive { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
}
