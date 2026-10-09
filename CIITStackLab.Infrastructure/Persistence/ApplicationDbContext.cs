using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private const string ExistingSchema = "erpsystem";

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseModule> CourseModules => Set<CourseModule>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<ContentQuestion> ContentQuestions => Set<ContentQuestion>();
    public DbSet<ContentInterviewQuestion> ContentInterviewQuestions => Set<ContentInterviewQuestion>();
    public DbSet<TrainingNote> TrainingNotes => Set<TrainingNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>().ToTable("AspNetUsers", ExistingSchema);
        modelBuilder.Entity<IdentityRole>().ToTable("AspNetRoles", ExistingSchema);
        modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("AspNetUserClaims", ExistingSchema);
        modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("AspNetUserLogins", ExistingSchema);
        modelBuilder.Entity<IdentityUserToken<string>>().ToTable("AspNetUserTokens", ExistingSchema);
        modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("AspNetRoleClaims", ExistingSchema);
        modelBuilder.Entity<IdentityUserRole<string>>().ToTable("AspNetUserRoles", ExistingSchema);

        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("tbltraining_courses", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("course_id");
            entity.Property(x => x.Title).HasColumnName("course_name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.FeesAmount).HasColumnName("fees_amount");
            entity.Property(x => x.FeesChangeDate).HasColumnName("fees_change_date");
            entity.Property(x => x.InstallmentPercentage).HasColumnName("installment_percentage");
            entity.Property(x => x.Flag).HasColumnName("flag");
            entity.Property(x => x.CreatedAt).HasColumnName("InsertedAt");
            entity.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
            entity.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
            entity.Property(x => x.RestoredAt).HasColumnName("RestoredAt");
            entity.HasMany(x => x.Modules).WithOne(x => x.Course).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CourseModule>(entity =>
        {
            entity.ToTable("tbltraining_course_topics", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("course_topic_id");
            entity.Property(x => x.CourseId).HasColumnName("course_id");
            entity.Property(x => x.TopicId).HasColumnName("topic_id");
            entity.Property(x => x.Flag).HasColumnName("flag");
            entity.Property(x => x.CreatedAt).HasColumnName("InsertedAt");
            entity.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
            entity.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
            entity.Property(x => x.RestoredAt).HasColumnName("RestoredAt");
            entity.HasOne(x => x.Topic).WithMany(x => x.CourseModules).HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Topic>(entity =>
        {
            entity.ToTable("tbltraining_topics", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("topic_id");
            entity.Property(x => x.Title).HasColumnName("topic_name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.PublicFolderId).HasColumnName("publicfolderid");
            entity.Property(x => x.Price).HasColumnName("price").HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(x => x.DurationMinutes).HasColumnName("duration_minutes");
            entity.Property(x => x.Flag).HasColumnName("flag");
            entity.Property(x => x.CreatedAt).HasColumnName("InsertedAt");
            entity.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
            entity.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
            entity.Property(x => x.RestoredAt).HasColumnName("RestoredAt");
            entity.HasMany(x => x.Lessons).WithOne(x => x.Topic).HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ContentQuestion>(entity =>
        {
            entity.ToTable("tblcontent_questions", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("question_id");
            entity.Property(x => x.ContentId).HasColumnName("content_id");
            entity.Property(x => x.Question).HasColumnName("question");
            entity.Property(x => x.Option1).HasColumnName("option1");
            entity.Property(x => x.Option2).HasColumnName("option2");
            entity.Property(x => x.Option3).HasColumnName("option3");
            entity.Property(x => x.Option4).HasColumnName("option4");
            entity.Property(x => x.CorrectOptionNumber).HasColumnName("correct_option_number");
            entity.Property(x => x.Flag).HasColumnName("flag");
            entity.Property(x => x.CreatedAt).HasColumnName("InsertedAt");
            entity.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
            entity.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
            entity.Property(x => x.RestoredAt).HasColumnName("RestoredAt");
            entity.HasOne(x => x.Content).WithMany().HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ContentInterviewQuestion>(entity =>
        {
            entity.ToTable("tblcontent_interview_questions", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("question_id");
            entity.Property(x => x.ContentId).HasColumnName("content_id");
            entity.Property(x => x.Question).HasColumnName("question");
            entity.Property(x => x.Answer).HasColumnName("answer");
            entity.Property(x => x.Flag).HasColumnName("flag");
            entity.Property(x => x.CreatedAt).HasColumnName("InsertedAt");
            entity.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
            entity.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
            entity.Property(x => x.RestoredAt).HasColumnName("RestoredAt");
            entity.HasOne(x => x.Content).WithMany().HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.ToTable("tbltraining_topic_contents", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("content_id");
            entity.Property(x => x.TopicId).HasColumnName("topic_id");
            entity.Property(x => x.NoteId).HasColumnName("note_id");
            entity.Property(x => x.Title).HasColumnName("content_name").HasMaxLength(100);
            entity.Property(x => x.Slides).HasColumnName("slides");
            entity.Property(x => x.VideoName).HasColumnName("video_name").HasMaxLength(100);
            entity.Property(x => x.Flag).HasColumnName("flag");
            entity.Property(x => x.CreatedAt).HasColumnName("InsertedAt");
            entity.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
            entity.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
            entity.Property(x => x.RestoredAt).HasColumnName("RestoredAt");
            entity.HasOne(x => x.Topic).WithMany(x => x.Lessons).HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<TrainingNote>().WithMany().HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TrainingNote>(entity =>
        {
            entity.ToTable("tbltraining_notes", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("note_id");
            entity.Property(x => x.TopicId).HasColumnName("topic_id").IsRequired();
            entity.Property(x => x.ChapterId).HasColumnName("chapter_id").HasMaxLength(50).IsRequired();
            entity.Property(x => x.Title).HasColumnName("title").HasMaxLength(100).IsRequired();
            entity.Property(x => x.HtmlContent).HasColumnName("html_content").IsRequired();
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
            entity.Property(x => x.Flag).HasColumnName("flag");
            entity.Property(x => x.CreatedAt).HasColumnName("InsertedAt");
            entity.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
            entity.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
            entity.HasIndex(x => new { x.TopicId, x.ChapterId }).IsUnique().HasDatabaseName("UX_tbltraining_notes_topic_chapter");
            entity.HasOne(x => x.Topic).WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
