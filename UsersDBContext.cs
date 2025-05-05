using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Scaffolding.Internal;

namespace TovUmarpeh;

public partial class UsersDBContext : DbContext
{
    public UsersDBContext()
    {
    }

    public UsersDBContext(DbContextOptions<UsersDBContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Activity> Activities { get; set; }

    public virtual DbSet<Enrollment> Enrollments { get; set; }

    public virtual DbSet<UsersFile> UsersFiles { get; set; }

    public virtual DbSet<UsersTable> UsersTables { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseMySql("Server=bzgu8ar6yf5q7tnidimj-mysql.services.clever-cloud.com;Database=bzgu8ar6yf5q7tnidimj;User=u7iuwm6vb053xgj4;Password=hMrLfQQ3BTt7eStzTSyU;",ServerVersion.Parse("8.0.22-mysql"));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8_general_ci")
            .HasCharSet("utf8");

        modelBuilder.Entity<Activity>(entity =>
        {
            entity.HasKey(e => e.IdActivities).HasName("PRIMARY");

            entity.ToTable("activities");

            entity.HasIndex(e => e.IdActivities, "idActivities_UNIQUE").IsUnique();

            entity.Property(e => e.IdActivities).HasColumnName("idActivities");
            entity.Property(e => e.DateActivity)
                .HasMaxLength(45)
                .HasColumnName("dateActivity");
            entity.Property(e => e.DetailsActivity)
                .HasMaxLength(100)
                .HasColumnName("detailsActivity");
            entity.Property(e => e.Max).HasColumnName("max");
            entity.Property(e => e.NameActivity)
                .HasMaxLength(45)
                .HasColumnName("nameActivity");
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.HasKey(e => e.EnrollmentId).HasName("PRIMARY");

            entity.ToTable("enrollments");

            entity.HasIndex(e => e.IdActivities, "idActivities");

            entity.HasIndex(e => e.IdNumber, "id_number");

            entity.Property(e => e.EnrollmentId).HasColumnName("EnrollmentID");
            entity.Property(e => e.IdActivities).HasColumnName("idActivities");
            entity.Property(e => e.IdNumber).HasColumnName("id_number");

            entity.HasOne(d => d.IdActivitiesNavigation).WithMany(p => p.Enrollments)
                .HasForeignKey(d => d.IdActivities)
                .HasConstraintName("enrollments_ibfk_1");

            entity.HasOne(d => d.IdNumberNavigation).WithMany(p => p.Enrollments)
                .HasForeignKey(d => d.IdNumber)
                .HasConstraintName("enrollments_ibfk_2");
        });

        modelBuilder.Entity<UsersFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.IdNumber, "IdNumber");

            entity.Property(e => e.Agreement).HasMaxLength(255);
            entity.Property(e => e.Identity).HasMaxLength(255);
            entity.Property(e => e.Medications).HasMaxLength(255);
            entity.Property(e => e.PersonalDetails).HasMaxLength(255);

            entity.HasOne(d => d.IdNumberNavigation).WithMany(p => p.UsersFiles)
                .HasForeignKey(d => d.IdNumber)
                .HasConstraintName("UsersFiles_ibfk_1");
        });

        modelBuilder.Entity<UsersTable>(entity =>
        {
            entity.HasKey(e => e.IdNumber).HasName("PRIMARY");

            entity.ToTable("users_table");

            entity.Property(e => e.IdNumber)
                .ValueGeneratedNever()
                .HasColumnName("id_number");
            entity.Property(e => e.Address)
                .HasMaxLength(100)
                .HasColumnName("address");
            entity.Property(e => e.BirthDate)
                .HasMaxLength(100)
                .HasColumnName("birthDate");
            entity.Property(e => e.City)
                .HasMaxLength(45)
                .HasColumnName("city");
            entity.Property(e => e.Email)
                .HasMaxLength(45)
                .HasColumnName("email");
            entity.Property(e => e.FirstName)
                .HasMaxLength(100)
                .HasColumnName("first_name");
            entity.Property(e => e.LastName)
                .HasMaxLength(100)
                .HasColumnName("last_name");
            entity.Property(e => e.Phone)
                .HasMaxLength(45)
                .HasColumnName("phone");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
