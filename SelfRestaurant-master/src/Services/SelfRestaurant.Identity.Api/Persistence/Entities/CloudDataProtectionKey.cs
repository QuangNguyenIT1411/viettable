namespace SelfRestaurant.Identity.Api.Persistence;

public sealed class CloudDataProtectionKey
{
    public Guid Id { get; set; }
    public string FriendlyName { get; set; } = "";
    public string Xml { get; set; } = "";
}
