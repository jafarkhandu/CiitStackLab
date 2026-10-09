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
    public DbSet<TrainingNote> TrainingNotes => Set<TrainingNote>();
    public DbSet<StudentLessonProgress> StudentLessonProgress => Set<StudentLessonProgress>();
    public DbSet<StudentAssessmentAttempt> StudentAssessmentAttempts => Set<StudentAssessmentAttempt>();
    public DbSet<CourseEnrollment> CourseEnrollments => Set<CourseEnrollment>();

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
            entity.HasMany(x => x.Modules)
                .WithOne(x => x.Course)
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.NoAction);
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
            entity.HasOne(x => x.Topic).WithMany(x => x.CourseModules)
                .HasForeignKey(x => x.TopicId)
                .OnDelete(DeleteBehavior.NoAction);
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
            entity.HasMany(x => x.Lessons).WithOne(x => x.Topic)
                .HasForeignKey(x => x.TopicId)
                .OnDelete(DeleteBehavior.NoAction);
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

        modelBuilder.Entity<StudentLessonProgress>(entity =>
        {
            entity.ToTable("tblstudent_lesson_progress", ExistingSchema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("progress_id")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.UserId)
                .HasColumnName("user_id")
                .HasMaxLength(450)
                .IsRequired();

            entity.Property(x => x.CourseId)
                .HasColumnName("course_id")
                .IsRequired();

            entity.Property(x => x.ContentId)
                .HasColumnName("content_id")
                .IsRequired();

            entity.Property(x => x.IsCompleted)
                .HasColumnName("is_completed")
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(x => x.LastAccessedAt)
                .HasColumnName("last_accessed_at")
                .HasColumnType("datetime2")
                .IsRequired();

            entity.Property(x => x.CompletedAt)
                .HasColumnName("completed_at")
                .HasColumnType("datetime2");

            entity.HasIndex(x => new { x.UserId, x.CourseId, x.ContentId })
                .IsUnique()
                .HasDatabaseName("UX_tblstudent_lesson_progress_user_course_content");
        });

        modelBuilder.Entity<StudentAssessmentAttempt>(entity =>
        {
            entity.ToTable("tblstudent_assessment_attempts", ExistingSchema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("attempt_id")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.UserId)
                .HasColumnName("user_id")
                .HasMaxLength(450)
                .IsRequired();

            entity.Property(x => x.CourseId)
                .HasColumnName("course_id")
                .IsRequired();

            entity.Property(x => x.ContentId)
                .HasColumnName("content_id")
                .IsRequired();

            entity.Property(x => x.Score)
                .HasColumnName("score")
                .IsRequired();

            entity.Property(x => x.TotalQuestions)
                .HasColumnName("total_questions")
                .IsRequired();

            entity.Property(x => x.AnswersJson)
                .HasColumnName("answers_json")
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(x => x.SubmittedAt)
                .HasColumnName("submitted_at")
                .HasColumnType("datetime2")
                .IsRequired();

            entity.HasIndex(x => new { x.UserId, x.CourseId, x.ContentId, x.SubmittedAt })
                .IsDescending(false, false, false, true)
                .HasDatabaseName("IX_tblstudent_assessment_attempts_user_course_content_date");
        });

        modelBuilder.Entity<CourseEnrollment>(entity =>
        {
            entity.ToTable("tblstudent_course_enrollments", ExistingSchema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("enrollment_id")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.UserId)
                .HasColumnName("user_id")
                .HasMaxLength(450)
                .IsRequired();

            entity.Property(x => x.CourseId)
                .HasColumnName("course_id")
                .IsRequired();

            entity.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.PriceAtEnrollment)
                .HasColumnName("price_at_enrollment")
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(x => x.RequestedAt)
                .HasColumnName("requested_at")
                .HasColumnType("datetime2")
                .IsRequired();

            entity.Property(x => x.ReviewedAt)
                .HasColumnName("reviewed_at")
                .HasColumnType("datetime2");

            entity.Property(x => x.ReviewedByUserId)
                .HasColumnName("reviewed_by_user_id")
                .HasMaxLength(450);

            entity.HasIndex(x => new { x.UserId, x.CourseId })
                .IsUnique()
                .HasDatabaseName("UX_tblstudent_course_enrollments_user_course");

            entity.HasIndex(x => new { x.Status, x.RequestedAt })
                .HasDatabaseName("IX_tblstudent_course_enrollments_status_date");
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
            entity.HasIndex(x => new { x.TopicId, x.ChapterId })
                .IsUnique()
                .HasDatabaseName("UX_tbltraining_notes_topic_chapter");
            entity.HasOne(x => x.Topic)
                .WithMany()
                .HasForeignKey(x => x.TopicId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
