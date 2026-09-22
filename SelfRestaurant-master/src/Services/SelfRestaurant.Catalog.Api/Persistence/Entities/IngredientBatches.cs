namespace SelfRestaurant.Catalog.Api.Persistence.Entities;

public sealed class IngredientBatches
{
    public int BatchID { get; set; }

    public int IngredientID { get; set; }

    public string? BatchCode { get; set; }

    public decimal QuantityInitial { get; set; }

    public decimal QuantityRemaining { get; set; }

    public string Unit { get; set; } = null!;

    public DateOnly ExpiryDate { get; set; }

    public DateOnly ReceivedDate { get; set; }

    public string? SupplierName { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Ingredients Ingredient { get; set; } = null!;

    public ICollection<IngredientStockMovements> StockMovements { get; set; } = new List<IngredientStockMovements>();
}
