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
    public DbSet<TopicExamQuestion> TopicExamQuestions => Set<TopicExamQuestion>();
    public DbSet<ContentInterviewQuestion> ContentInterviewQuestions => Set<ContentInterviewQuestion>();
    public DbSet<ContentProgramQuestion> ContentProgramQuestions => Set<ContentProgramQuestion>();
    public DbSet<ContentProgramAnswer> ContentProgramAnswers => Set<ContentProgramAnswer>();
    public DbSet<TrainingNote> TrainingNotes => Set<TrainingNote>();
    public DbSet<StudentPayment> StudentPayments => Set<StudentPayment>();
    public DbSet<StudentLoginActivity> StudentLoginActivities => Set<StudentLoginActivity>();
    public DbSet<StudentLessonProgress> StudentLessonProgress => Set<StudentLessonProgress>();
    public DbSet<StudentAssessmentAttempt> StudentAssessmentAttempts => Set<StudentAssessmentAttempt>();
    public DbSet<StudentCoursePurchase> StudentCoursePurchases => Set<StudentCoursePurchase>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<TopicExamAttempt> TopicExamAttempts => Set<TopicExamAttempt>();
    public DbSet<TopicExamSecurityEvent> TopicExamSecurityEvents => Set<TopicExamSecurityEvent>();
    public DbSet<ExamApplicationNotification> ExamApplicationNotifications => Set<ExamApplicationNotification>();

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
            entity.Property(x => x.ImagePath).HasColumnName("course_image_path").HasMaxLength(500);
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

        modelBuilder.Entity<TopicExamQuestion>(entity =>
        {
            entity.ToTable("tblstudent_topic_exam_questions", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("exam_question_id");
            entity.Property(x => x.TopicId).HasColumnName("topic_id");
            entity.Property(x => x.Question).HasColumnName("question").HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Option1).HasColumnName("option1").HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Option2).HasColumnName("option2").HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Option3).HasColumnName("option3").HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Option4).HasColumnName("option4").HasMaxLength(1000).IsRequired();
            entity.Property(x => x.CorrectOptionNumber).HasColumnName("correct_option_number");
            entity.Property(x => x.Flag).HasColumnName("flag").HasDefaultValue(0);
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.HasIndex(x => x.TopicId).HasDatabaseName("IX_topic_exam_questions_topic_id");
        });

        modelBuilder.Entity<ContentQuestion>(entity =>
        {
            entity.ToTable("tblcontent_questions", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("question_id");
            entity.Property(x => x.ContentId).HasColumnName("content_id");
            entity.Property(x => x.TopicId).HasColumnName("topic_id");
            entity.Property(x => x.NoteId).HasColumnName("note_id");
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
            entity.HasOne(x => x.Topic).WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Note).WithMany().HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(x => x.TopicId).HasDatabaseName("IX_tblcontent_questions_topic_id");
            entity.HasIndex(x => x.NoteId).HasDatabaseName("IX_tblcontent_questions_note_id");
        });

        modelBuilder.Entity<ContentInterviewQuestion>(entity =>
        {
            entity.ToTable("tblcontent_interview_questions", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("question_id");
            entity.Property(x => x.ContentId).HasColumnName("content_id");
            entity.Property(x => x.TopicId).HasColumnName("topic_id");
            entity.Property(x => x.NoteId).HasColumnName("note_id");
            entity.Property(x => x.Question).HasColumnName("question");
            entity.Property(x => x.Answer).HasColumnName("answer");
            entity.Property(x => x.Flag).HasColumnName("flag");
            entity.Property(x => x.CreatedAt).HasColumnName("InsertedAt");
            entity.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
            entity.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
            entity.Property(x => x.RestoredAt).HasColumnName("RestoredAt");
            entity.HasOne(x => x.Content).WithMany().HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Topic).WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Note).WithMany().HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(x => x.TopicId).HasDatabaseName("IX_tblcontent_interview_questions_topic_id");
            entity.HasIndex(x => x.NoteId).HasDatabaseName("IX_tblcontent_interview_questions_note_id");
        });

        modelBuilder.Entity<ContentProgramQuestion>(entity =>
        {
            entity.ToTable("tblcontent_program_questions", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("program_question_id");
            entity.Property(x => x.ContentId).HasColumnName("content_id");
            entity.Property(x => x.NoteId).HasColumnName("note_id");
            entity.Property(x => x.QuestionTitle).HasColumnName("question_title");
            entity.Property(x => x.QuestionDescription).HasColumnName("question_description");
            entity.Property(x => x.Flag).HasColumnName("flag");
            entity.Property(x => x.CreatedAt).HasColumnName("InsertedAt");
            entity.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
            entity.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
            entity.Property(x => x.RestoredAt).HasColumnName("RestoredAt");
            entity.HasOne(x => x.Content).WithMany().HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Note).WithMany().HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(x => x.NoteId).HasDatabaseName("IX_tblcontent_program_questions_note_id");
            entity.HasMany(x => x.Answers).WithOne(x => x.ProgramQuestion).HasForeignKey(x => x.ProgramQuestionId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ContentProgramAnswer>(entity =>
        {
            entity.ToTable("tblcontent_program_answers", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("program_answer_id");
            entity.Property(x => x.ProgramQuestionId).HasColumnName("program_question_id");
            entity.Property(x => x.Answer).HasColumnName("program_answer");
            entity.Property(x => x.Description).HasColumnName("program_description");
            entity.Property(x => x.Flag).HasColumnName("flag");
            entity.Property(x => x.CreatedAt).HasColumnName("InsertedAt");
            entity.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
            entity.Property(x => x.DeletedAt).HasColumnName("DeletedAt");
            entity.Property(x => x.RestoredAt).HasColumnName("RestoredAt");
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
            entity.HasOne(x => x.Note).WithMany().HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<StudentLessonProgress>(entity =>
        {
            entity.ToTable("tblstudent_lesson_progress", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("progress_id").ValueGeneratedOnAdd();
            entity.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(450).IsRequired();
            entity.Property(x => x.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(x => x.ContentId).HasColumnName("content_id").IsRequired();
            entity.Property(x => x.IsCompleted).HasColumnName("is_completed").HasDefaultValue(false).IsRequired();
            entity.Property(x => x.LastAccessedAt).HasColumnName("last_accessed_at").HasColumnType("datetime2").IsRequired();
            entity.Property(x => x.CompletedAt).HasColumnName("completed_at").HasColumnType("datetime2");
            entity.HasIndex(x => new { x.UserId, x.CourseId, x.ContentId }).IsUnique().HasDatabaseName("UX_tblstudent_lesson_progress_user_course_content");
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
                .HasColumnName("content_id");

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

        modelBuilder.Entity<StudentCoursePurchase>(entity =>
        {
            entity.ToTable("tblstudent_course_purchases", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("purchase_id").ValueGeneratedOnAdd();
            entity.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(450).IsRequired();
            entity.Property(x => x.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(x => x.PurchasePrice).HasColumnName("purchase_price").HasPrecision(18, 2).IsRequired();
            entity.Property(x => x.PurchasedAt).HasColumnName("purchased_at").HasColumnType("datetime2").IsRequired();
            entity.HasIndex(x => new { x.UserId, x.CourseId }).IsUnique().HasDatabaseName("UX_tblstudent_course_purchases_user_course");
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("tblpayment_transactions", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("payment_transaction_id").ValueGeneratedOnAdd();
            entity.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(450).IsRequired();
            entity.Property(x => x.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(x => x.MerchantOrderId).HasColumnName("merchant_order_id").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.PhonePeOrderId).HasColumnName("phonepe_order_id").HasMaxLength(100);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime2").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime2").IsRequired();
            entity.Property(x => x.CompletedAt).HasColumnName("completed_at").HasColumnType("datetime2");
            entity.HasIndex(x => x.MerchantOrderId).IsUnique().HasDatabaseName("UX_tblpayment_transactions_merchant_order_id");
            entity.HasIndex(x => new { x.UserId, x.CourseId, x.Status }).HasDatabaseName("IX_tblpayment_transactions_user_course_status");
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<TopicExamAttempt>(entity =>
        {
            entity.ToTable("tblstudent_topic_exam_attempts", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("exam_attempt_id").ValueGeneratedOnAdd();
            entity.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(450).IsRequired();
            entity.Property(x => x.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(x => x.TopicId).HasColumnName("topic_id").IsRequired();
            entity.Property(x => x.ScheduledAtUtc).HasColumnName("scheduled_at_utc").HasColumnType("datetime2").IsRequired();
            entity.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc").HasColumnType("datetime2");
            entity.Property(x => x.SubmittedAtUtc).HasColumnName("submitted_at_utc").HasColumnType("datetime2");
            entity.Property(x => x.DeadlineAtUtc).HasColumnName("deadline_at_utc").HasColumnType("datetime2");
            entity.Property(x => x.VerificationStartedAtUtc).HasColumnName("verification_started_at_utc").HasColumnType("datetime2");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
            entity.Property(x => x.QuestionIdsJson).HasColumnName("question_ids_json").HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.OptionOrderJson).HasColumnName("option_order_json").HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.AnswersJson).HasColumnName("answers_json").HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.DeviceTokenHash).HasColumnName("device_token_hash").HasMaxLength(128);
            entity.Property(x => x.WarningCount).HasColumnName("warning_count").HasDefaultValue(0).IsRequired();
            entity.Property(x => x.Score).HasColumnName("score").HasDefaultValue(0).IsRequired();
            entity.Property(x => x.TotalQuestions).HasColumnName("total_questions").HasDefaultValue(0).IsRequired();
            entity.Property(x => x.Passed).HasColumnName("passed").HasDefaultValue(false).IsRequired();
            entity.Property(x => x.TerminationReason).HasColumnName("termination_reason").HasMaxLength(200);
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime2").IsRequired();
            entity.HasIndex(x => new { x.UserId, x.CourseId, x.TopicId, x.CreatedAtUtc })
                .HasDatabaseName("IX_topic_exam_user_course_topic");
            entity.HasIndex(x => new { x.UserId, x.CourseId, x.TopicId, x.Status })
                .HasDatabaseName("IX_topic_exam_user_topic_status");
            entity.HasIndex(x => new { x.UserId, x.CourseId, x.TopicId }).IsUnique()
                .HasFilter("[status] <> 'Passed' AND [status] <> 'Failed' AND [status] <> 'Terminated' AND [status] <> 'TimedOut'")
                .HasDatabaseName("UX_topic_exam_one_active_attempt");
        });

        modelBuilder.Entity<TopicExamSecurityEvent>(entity =>
        {
            entity.ToTable("tblstudent_topic_exam_security_events", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("event_id").ValueGeneratedOnAdd();
            entity.Property(x => x.ExamAttemptId).HasColumnName("exam_attempt_id").IsRequired();
            entity.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(80).IsRequired();
            entity.Property(x => x.Details).HasColumnName("details").HasMaxLength(1000);
            entity.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("datetime2").IsRequired();
            entity.HasIndex(x => new { x.ExamAttemptId, x.OccurredAtUtc })
                .HasDatabaseName("IX_topic_exam_events_attempt_date");
        });

        modelBuilder.Entity<ExamApplicationNotification>(entity =>
        {
            entity.ToTable("tbl_exam_application_notifications", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .HasColumnName("notification_id")
                .ValueGeneratedOnAdd();
            entity.Property(x => x.DedupeKey)
                .HasColumnName("dedupe_key")
                .HasColumnType("char(64)")
                .HasMaxLength(64)
                .IsFixedLength()
                .IsRequired();
            entity.Property(x => x.AdminUserId)
                .HasColumnName("admin_user_id")
                .HasMaxLength(450)
                .IsRequired();
            entity.Property(x => x.StudentUserId)
                .HasColumnName("student_user_id")
                .HasMaxLength(450)
                .IsRequired();
            entity.Property(x => x.StudentName)
                .HasColumnName("student_name")
                .HasMaxLength(256)
                .IsRequired();
            entity.Property(x => x.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(x => x.CourseTitle)
                .HasColumnName("course_title")
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(x => x.TopicId).HasColumnName("topic_id").IsRequired();
            entity.Property(x => x.TopicTitle)
                .HasColumnName("topic_title")
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(x => x.ValidQuestionCount).HasColumnName("valid_question_count").IsRequired();
            entity.Property(x => x.RequiredQuestionCount)
                .HasColumnName("required_question_count")
                .HasDefaultValue(50)
                .IsRequired();
            entity.Property(x => x.Reason)
                .HasColumnName("reason")
                .HasMaxLength(1000)
                .IsRequired();
            entity.Property(x => x.NotificationDay)
                .HasColumnName("notification_day")
                .HasColumnType("date")
                .IsRequired();
            entity.Property(x => x.CreatedAtUtc)
                .HasColumnName("created_at_utc")
                .HasColumnType("datetime2")
                .IsRequired();
            entity.Property(x => x.IsDelivered)
                .HasColumnName("is_delivered")
                .HasDefaultValue(false)
                .IsRequired();
            entity.Property(x => x.IsRead)
                .HasColumnName("is_read")
                .HasDefaultValue(false)
                .IsRequired();

            entity.HasIndex(x => x.DedupeKey)
                .IsUnique()
                .HasDatabaseName("UX_exam_app_notification_dedupe_key");
            entity.HasIndex(x => new { x.AdminUserId, x.IsRead, x.CreatedAtUtc })
                .HasDatabaseName("IX_exam_app_notification_admin_read_created");
            entity.HasIndex(x => new { x.AdminUserId, x.IsDelivered })
                .HasDatabaseName("IX_exam_app_notification_admin_delivered");
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

        modelBuilder.Entity<StudentPayment>(entity =>
        {
            entity.ToTable("tblstudent_payments", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("payment_id");
            entity.Property(x => x.RegistrationId).HasColumnName("registration_id");
            entity.Property(x => x.PaymentDate).HasColumnName("payment_date");
            entity.Property(x => x.PaymentAmount).HasColumnName("payment_amount").HasPrecision(18, 2);
            entity.Property(x => x.PaymentMode).HasColumnName("payment_mode");
            entity.Property(x => x.PaymentDescription).HasColumnName("payment_description");
            entity.Property(x => x.Flag).HasColumnName("flag");
            entity.Property(x => x.IsPaid).HasColumnName("is_paid");
        });

        modelBuilder.Entity<StudentLoginActivity>(entity =>
        {
            entity.ToTable("tblstudent_logins", ExistingSchema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("activity_id");
            entity.Property(x => x.StudentId).HasColumnName("student_id");
            entity.Property(x => x.LoginTime).HasColumnName("login_time");
            entity.Property(x => x.LogoutTime).HasColumnName("logout_time");
            entity.Property(x => x.IpAddress).HasColumnName("ip_address");
            entity.Property(x => x.Flag).HasColumnName("flag");
        });
    }
}
