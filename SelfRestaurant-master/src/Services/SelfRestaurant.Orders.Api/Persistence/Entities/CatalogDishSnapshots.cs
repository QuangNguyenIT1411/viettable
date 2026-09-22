namespace SelfRestaurant.Orders.Api.Persistence.Entities;

public sealed class CatalogDishSnapshots
{
    public int DishId { get; set; }
    public string Name { get; set; } = null!;
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public decimal Price { get; set; }
    public string? Unit { get; set; }
    public string? Image { get; set; }
    public bool IsActive { get; set; }
    public bool Available { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
}
