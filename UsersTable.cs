using System;
using System.Collections.Generic;

namespace TovUmarpeh;

public partial class UsersTable
{
    public int IdNumber { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Address { get; set; }

    public string Phone { get; set; } = null!;

    public string City { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string BirthDate { get; set; } = null!;

    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    public virtual ICollection<UsersFile> UsersFiles { get; set; } = new List<UsersFile>();
}
