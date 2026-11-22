using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StudyHub.Application.Common;
using StudyHub.Domain;

namespace StudyHub.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<BankVersion> BankVersions => Set<BankVersion>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<QuestionVersion> QuestionVersions => Set<QuestionVersion>();
    public DbSet<UserProfile> Profiles => Set<UserProfile>();
    public DbSet<QuestionMark> QuestionMarks => Set<QuestionMark>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<DateTime>().HaveConversion<UtcConverter>();
        builder.Properties<DateTime?>().HaveConversion<NullableUtcConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Course>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<BankVersion>().HasIndex(x => new { x.CourseId, x.FileHash });
        b.Entity<BankVersion>().HasIndex(x => new { x.CourseId, x.VersionNumber }).IsUnique();
        b.Entity<Module>().HasIndex(x => new { x.CourseId, x.Slug }).IsUnique();
        b.Entity<Question>().HasIndex(x => new { x.CourseId, x.OriginalId }).IsUnique();
        b.Entity<Question>().Ignore(x => x.NeedsReview);
        b.Entity<Question>().HasOne(x => x.Module).WithMany(m => m.Questions).HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<QuestionOption>().HasIndex(x => new { x.QuestionId, x.Key }).IsUnique();
        b.Entity<QuestionVersion>().HasIndex(x => new { x.QuestionId, x.VersionNumber }).IsUnique();
        b.Entity<QuestionMark>().HasIndex(x => new { x.ProfileId, x.QuestionId, x.Kind }).IsUnique();
        b.Entity<QuestionMark>().HasOne<UserProfile>().WithMany().HasForeignKey(x => x.ProfileId);
        b.Entity<Attempt>().HasIndex(x => new { x.ProfileId, x.QuestionId });
        b.Entity<Attempt>().HasOne<UserProfile>().WithMany().HasForeignKey(x => x.ProfileId);
        b.Entity<Exam>().HasIndex(x => x.ProfileId);
        b.Entity<Exam>().HasOne<UserProfile>().WithMany().HasForeignKey(x => x.ProfileId);
        b.Entity<Exam>().Ignore(x => x.IsFinished).Ignore(x => x.Deadline);
        b.Entity<ExamQuestion>().HasIndex(x => new { x.ExamId, x.Position }).IsUnique();
        b.Entity<ExamQuestion>().HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }

    private class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private class NullableUtcConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? v.Value.ToUniversalTime() : v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
}
