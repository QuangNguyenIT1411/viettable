namespace SelfRestaurant.Billing.Api.Persistence.Entities;

/// <summary>
/// Service fee and VAT of one branch (owner 05/11), edited in Admin → Chi nhánh. 0 = off; a branch with no row is 0/0.
/// The percentages are copied onto every quote and bill when it is priced, so changing them never alters an old bill.
/// </summary>
public sealed class BranchChargeSetting
{
    public int BranchId { get; set; }
    public decimal ServiceFeePercent { get; set; }
    public decimal VatPercent { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int? UpdatedByEmployeeId { get; set; }
    public string? UpdatedByName { get; set; }
}
