using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations;

public sealed partial class AddStudentCoursePurchases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'erpsystem.tblstudent_course_purchases', N'U') IS NULL
            BEGIN
                CREATE TABLE [erpsystem].[tblstudent_course_purchases]
                (
                    [purchase_id] INT IDENTITY(1,1) NOT NULL
                        CONSTRAINT [PK_tblstudent_course_purchases] PRIMARY KEY,
                    [user_id] NVARCHAR(450) NOT NULL,
                    [course_id] INT NOT NULL,
                    [purchase_price] DECIMAL(18,2) NOT NULL,
                    [purchased_at] DATETIME2 NOT NULL,
                    CONSTRAINT [FK_tblstudent_course_purchases_AspNetUsers_user_id]
                        FOREIGN KEY ([user_id]) REFERENCES [erpsystem].[AspNetUsers] ([Id]),
                    CONSTRAINT [FK_tblstudent_course_purchases_tbltraining_courses_course_id]
                        FOREIGN KEY ([course_id]) REFERENCES [erpsystem].[tbltraining_courses] ([course_id])
                );
            END;

            IF OBJECT_ID(N'erpsystem.tblstudent_course_enrollments', N'U') IS NOT NULL
            BEGIN
                INSERT INTO [erpsystem].[tblstudent_course_purchases]
                    ([user_id], [course_id], [purchase_price], [purchased_at])
                SELECT
                    enrollment.[user_id],
                    enrollment.[course_id],
                    CASE WHEN enrollment.[price_at_enrollment] < 0
                         THEN 0 ELSE enrollment.[price_at_enrollment] END,
                    COALESCE(enrollment.[reviewed_at], enrollment.[requested_at])
                FROM [erpsystem].[tblstudent_course_enrollments] AS enrollment
                WHERE enrollment.[status] = N'Active'
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [erpsystem].[tblstudent_course_purchases] AS purchase
                      WHERE purchase.[user_id] = enrollment.[user_id]
                        AND purchase.[course_id] = enrollment.[course_id]
                  );

                DROP TABLE [erpsystem].[tblstudent_course_enrollments];
            END;

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE [name] = N'UX_tblstudent_course_purchases_user_course'
                  AND [object_id] = OBJECT_ID(N'erpsystem.tblstudent_course_purchases')
            )
            BEGIN
                CREATE UNIQUE INDEX [UX_tblstudent_course_purchases_user_course]
                    ON [erpsystem].[tblstudent_course_purchases] ([user_id], [course_id]);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'erpsystem.tblstudent_course_enrollments', N'U') IS NULL
            BEGIN
                CREATE TABLE [erpsystem].[tblstudent_course_enrollments]
                (
                    [enrollment_id] INT IDENTITY(1,1) NOT NULL
                        CONSTRAINT [PK_tblstudent_course_enrollments] PRIMARY KEY,
                    [user_id] NVARCHAR(450) NOT NULL,
                    [course_id] INT NOT NULL,
                    [status] NVARCHAR(30) NOT NULL,
                    [price_at_enrollment] DECIMAL(18,2) NOT NULL,
                    [requested_at] DATETIME2 NOT NULL,
                    [reviewed_at] DATETIME2 NULL,
                    [reviewed_by_user_id] NVARCHAR(450) NULL,
                    CONSTRAINT [FK_tblstudent_course_enrollments_AspNetUsers]
                        FOREIGN KEY ([user_id]) REFERENCES [erpsystem].[AspNetUsers] ([Id]),
                    CONSTRAINT [FK_tblstudent_course_enrollments_courses]
                        FOREIGN KEY ([course_id]) REFERENCES [erpsystem].[tbltraining_courses] ([course_id]),
                    CONSTRAINT [FK_tblstudent_course_enrollments_reviewed_by]
                        FOREIGN KEY ([reviewed_by_user_id]) REFERENCES [erpsystem].[AspNetUsers] ([Id]),
                    CONSTRAINT [CK_tblstudent_course_enrollments_status]
                        CHECK ([status] IN (N'PendingApproval', N'Active', N'Rejected')),
                    CONSTRAINT [CK_tblstudent_course_enrollments_price]
                        CHECK ([price_at_enrollment] >= 0)
                );

                IF OBJECT_ID(N'erpsystem.tblstudent_course_purchases', N'U') IS NOT NULL
                BEGIN
                    INSERT INTO [erpsystem].[tblstudent_course_enrollments]
                        ([user_id], [course_id], [status], [price_at_enrollment], [requested_at])
                    SELECT [user_id], [course_id], N'Active', [purchase_price], [purchased_at]
                    FROM [erpsystem].[tblstudent_course_purchases];
                END;

                CREATE UNIQUE INDEX [UX_tblstudent_course_enrollments_user_course]
                    ON [erpsystem].[tblstudent_course_enrollments] ([user_id], [course_id]);

                CREATE INDEX [IX_tblstudent_course_enrollments_status_date]
                    ON [erpsystem].[tblstudent_course_enrollments] ([status], [requested_at]);
            END;

            IF OBJECT_ID(N'erpsystem.tblstudent_course_purchases', N'U') IS NOT NULL
            BEGIN
                DROP TABLE [erpsystem].[tblstudent_course_purchases];
            END;
            """);
    }
}
