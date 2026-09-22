namespace SelfRestaurant.Catalog.Api.Persistence.Entities;

public sealed class Units
{
    public int UnitID { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
