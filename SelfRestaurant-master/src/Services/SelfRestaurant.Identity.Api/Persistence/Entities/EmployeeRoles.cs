using System.Collections.Generic;

namespace SelfRestaurant.Identity.Api.Persistence.Entities;

public sealed class EmployeeRoles
{
    public int RoleID { get; set; }
    public string RoleCode { get; set; } = null!;
    public string RoleName { get; set; } = null!;

    public ICollection<Employees> Employees { get; set; } = new List<Employees>();
}
