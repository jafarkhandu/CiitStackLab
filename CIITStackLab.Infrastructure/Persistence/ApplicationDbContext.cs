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
        : base(options)
    {
    }

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseModule> CourseModules => Set<CourseModule>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Topic> Topics => Set<Topic>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The existing ERP database already contains the ASP.NET Identity tables.
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

            entity.Property(x => x.Id)
                .HasColumnName("course_id");

            entity.Property(x => x.Title)
                .HasColumnName("course_name")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.FeesAmount)
                .HasColumnName("fees_amount");

            entity.Property(x => x.FeesChangeDate)
                .HasColumnName("fees_change_date");

            entity.Property(x => x.InstallmentPercentage)
                .HasColumnName("installment_percentage");

            entity.Property(x => x.Flag)
                .HasColumnName("flag");

            entity.Property(x => x.CreatedAt)
                .HasColumnName("InsertedAt");

            entity.Property(x => x.UpdatedAt)
                .HasColumnName("UpdatedAt");

            entity.Property(x => x.DeletedAt)
                .HasColumnName("DeletedAt");

            entity.Property(x => x.RestoredAt)
                .HasColumnName("RestoredAt");

            entity.HasMany(x => x.Modules)
                .WithOne(x => x.Course)
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CourseModule>(entity =>
        {
            entity.ToTable("tbltraining_course_topics", ExistingSchema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("course_topic_id");

            entity.Property(x => x.CourseId)
                .HasColumnName("course_id");

            entity.Property(x => x.TopicId)
                .HasColumnName("topic_id");

            entity.Property(x => x.Flag)
                .HasColumnName("flag");

            entity.Property(x => x.CreatedAt)
                .HasColumnName("InsertedAt");

            entity.Property(x => x.UpdatedAt)
                .HasColumnName("UpdatedAt");

            entity.Property(x => x.DeletedAt)
                .HasColumnName("DeletedAt");

            entity.Property(x => x.RestoredAt)
                .HasColumnName("RestoredAt");

            entity.HasOne(x => x.Topic)
                .WithMany(x => x.CourseModules)
                .HasForeignKey(x => x.TopicId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Topic>(entity =>
        {
            entity.ToTable("tbltraining_topics", ExistingSchema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("topic_id");

            entity.Property(x => x.Title)
                .HasColumnName("topic_name")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.PublicFolderId)
                .HasColumnName("publicfolderid");

            entity.Property(x => x.Flag)
                .HasColumnName("flag");

            entity.Property(x => x.CreatedAt)
                .HasColumnName("InsertedAt");

            entity.Property(x => x.UpdatedAt)
                .HasColumnName("UpdatedAt");

            entity.Property(x => x.DeletedAt)
                .HasColumnName("DeletedAt");

            entity.Property(x => x.RestoredAt)
                .HasColumnName("RestoredAt");

            entity.HasMany(x => x.Lessons)
                .WithOne(x => x.Topic)
                .HasForeignKey(x => x.TopicId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.ToTable("tbltraining_topic_contents", ExistingSchema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("content_id");

            entity.Property(x => x.TopicId)
                .HasColumnName("topic_id");

            entity.Property(x => x.Title)
                .HasColumnName("content_name")
                .HasMaxLength(100);

            entity.Property(x => x.Slides)
                .HasColumnName("slides");

            entity.Property(x => x.VideoName)
                .HasColumnName("video_name")
                .HasMaxLength(100);

            entity.Property(x => x.Flag)
                .HasColumnName("flag");

            entity.Property(x => x.CreatedAt)
                .HasColumnName("InsertedAt");

            entity.Property(x => x.UpdatedAt)
                .HasColumnName("UpdatedAt");

            entity.Property(x => x.DeletedAt)
                .HasColumnName("DeletedAt");

            entity.Property(x => x.RestoredAt)
                .HasColumnName("RestoredAt");
        });
    }
}
