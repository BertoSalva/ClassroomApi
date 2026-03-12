using Classroom.Domain.Entities;
using Classroom.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Classroom.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<ClassroomGroup> ClassroomGroups => Set<ClassroomGroup>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<ResourceFile> ResourceFiles => Set<ResourceFile>();

    public DbSet<AdmissionsNumber> AdmissionsNumbers => Set<AdmissionsNumber>();

    public DbSet<TeacherEmailVerificationCode> TeacherEmailVerificationCodes => Set<TeacherEmailVerificationCode>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AdmissionsNumber>(e =>
        {
            e.ToTable("AdmissionsNumbers");
            e.Property(x => x.AdminId).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.AdminId).IsUnique();
        });

        builder.Entity<TeacherEmailVerificationCode>(e =>
        {
            e.ToTable("TeacherEmailVerificationCodes");

            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.Code).HasMaxLength(16).IsRequired();
            e.Property(x => x.ProtectedIdentityToken).IsRequired();

            e.HasIndex(x => new { x.Email, x.Code }).IsUnique();
            e.HasIndex(x => x.ExpiresAt);
        });

        builder.Entity<Grade>()
            .HasMany(g => g.Subjects)
            .WithOne(s => s.Grade!)
            .HasForeignKey(s => s.GradeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ClassroomGroup>()
            .HasMany(c => c.Enrollments)
            .WithOne(e => e.ClassroomGroup!)
            .HasForeignKey(e => e.ClassroomGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ClassroomGroup>()
            .HasMany(c => c.Resources)
            .WithOne(r => r.ClassroomGroup!)
            .HasForeignKey(r => r.ClassroomGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ResourceFile>()
            .Property(r => r.Title)
            .HasMaxLength(200);

        builder.Entity<Subject>()
            .Property(s => s.Name)
            .HasMaxLength(100);

        builder.Entity<Grade>()
            .Property(g => g.Name)
            .HasMaxLength(50);
    }
}
