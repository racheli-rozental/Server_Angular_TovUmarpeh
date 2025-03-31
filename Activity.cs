using System;
using System.Collections.Generic;

namespace TovUmarpeh;

public partial class Activity
{
    public int IdActivities { get; set; }

    public string NameActivity { get; set; } = null!;

    public string DateActivity { get; set; } = null!;

    public string? DetailsActivity { get; set; }

    public int Max { get; set; }

    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
