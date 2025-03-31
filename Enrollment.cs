using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TovUmarpeh;

public partial class Enrollment
{
    public int EnrollmentId { get; set; }

    public int? IdActivities { get; set; }

    public int? IdNumber { get; set; }
[JsonIgnore]
    public virtual Activity? IdActivitiesNavigation { get; set; }
[JsonIgnore]
    public virtual UsersTable? IdNumberNavigation { get; set; }
}
