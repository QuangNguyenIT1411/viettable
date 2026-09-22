namespace SelfRestaurant.Orders.Api.Persistence.Entities;

public sealed class OrderItemIngredients
{
    public int OrderItemIngredientID { get; set; }
    public int OrderItemID { get; set; }
    public int IngredientID { get; set; }
    public string? IngredientName { get; set; }
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    public string? Note { get; set; }
    public bool IsRemoved { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
