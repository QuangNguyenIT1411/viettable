using System;
using System.Collections.Generic;

namespace SelfRestaurant.Catalog.Api.Persistence.Entities;

public partial class EmployeeRoles
{
    public int RoleID { get; set; }

    public string RoleCode { get; set; } = null!;

    public string RoleName { get; set; } = null!;

    public virtual ICollection<Employees> Employees { get; set; } = new List<Employees>();
}
