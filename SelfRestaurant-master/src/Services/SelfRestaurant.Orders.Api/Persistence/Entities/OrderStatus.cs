using System.Collections.Generic;

namespace SelfRestaurant.Orders.Api.Persistence.Entities;

public sealed class OrderStatus
{
    public int StatusID { get; set; }
    public string StatusCode { get; set; } = null!;
    public string StatusName { get; set; } = null!;
    public ICollection<Orders> Orders { get; set; } = new List<Orders>();
}
