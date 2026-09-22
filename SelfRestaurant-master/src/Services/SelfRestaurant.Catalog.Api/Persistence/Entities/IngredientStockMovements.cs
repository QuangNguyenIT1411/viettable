namespace SelfRestaurant.Catalog.Api.Persistence.Entities;

public sealed class IngredientStockMovements
{
    public long MovementID { get; set; }

    public int IngredientID { get; set; }

    public int? BatchID { get; set; }

    public decimal QuantityChange { get; set; }

    public string MovementType { get; set; } = null!;

    public string? ReferenceType { get; set; }

    public int? ReferenceID { get; set; }

    public int? OrderID { get; set; }

    public int? OrderItemID { get; set; }

    public int? DishID { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? Note { get; set; }

    public Ingredients Ingredient { get; set; } = null!;

    public IngredientBatches? Batch { get; set; }
}
