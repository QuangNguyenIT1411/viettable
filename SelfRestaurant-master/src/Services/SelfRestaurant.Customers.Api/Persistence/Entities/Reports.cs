using System;
using System.Collections.Generic;

namespace SelfRestaurant.Customers.Api.Persistence.Entities;

public partial class Reports
{
    public int ReportID { get; set; }

    public string ReportType { get; set; } = null!;

    public DateTime GeneratedDate { get; set; }

    public string? Content { get; set; }

    public string? FilePath { get; set; }
}
