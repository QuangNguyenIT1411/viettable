using System;

namespace SelfRestaurant.Identity.Api.Persistence.Entities;

public sealed class Employees
{
    public int EmployeeID { get; set; }
    public string Name { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public decimal? Salary { get; set; }
    public string? Shift { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int BranchID { get; set; }
    public int RoleID { get; set; }

    public EmployeeRoles Role { get; set; } = null!;
}
