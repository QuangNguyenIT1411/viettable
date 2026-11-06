namespace SelfRestaurant.Catalog.Api.Persistence.Entities;

/// <summary>
/// Two tables the admin says can be pushed together for a large party (owner 06/11). Stored both ways (5→6 and 6→5)
/// so either table finds its neighbours with one lookup.
/// </summary>
public sealed class DiningTableLink
{
    public int TableId { get; set; }
    public int LinkedTableId { get; set; }
}
