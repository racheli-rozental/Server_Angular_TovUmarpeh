using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TovUmarpeh;

public partial class UsersFile
{
    public int Id { get; set; }

    public int IdNumber { get; set; }

    public string Medications { get; set; } = null!;

    public string Agreement { get; set; } = null!;

    public string PersonalDetails { get; set; } = null!;

    public string Identity { get; set; } = null!;
[JsonIgnore]
    public virtual UsersTable IdNumberNavigation { get; set; } = null!;
}
