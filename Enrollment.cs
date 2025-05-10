using System;
using System.Collections.Generic;

namespace TovUmarpeh;

public partial class Enrollment
{
    public int EnrollmentId { get; set; }

    public int? IdActivities { get; set; }

    public int? IdNumber { get; set; }

    public virtual Activity? IdActivitiesNavigation { get; set; }

    public virtual UsersTable? IdNumberNavigation { get; set; }
}
