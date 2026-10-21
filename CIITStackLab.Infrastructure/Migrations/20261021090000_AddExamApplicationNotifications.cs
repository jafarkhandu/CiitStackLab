using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261021090000_AddExamApplicationNotifications")]
    public partial class AddExamApplicationNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This table may already have been created by the deployment SQL script.
            // Make the migration safe for databases where the table exists already.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[erpsystem].[tbl_exam_application_notifications]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [erpsystem].[tbl_exam_application_notifications]
                    (
                        [notification_id] BIGINT IDENTITY(1,1) NOT NULL,
                        [dedupe_key] CHAR(64) NOT NULL,
                        [admin_user_id] NVARCHAR(450) NOT NULL,
                        [student_user_id] NVARCHAR(450) NOT NULL,
                        [student_name] NVARCHAR(256) NOT NULL,
                        [course_id] INT NOT NULL,
                        [course_title] NVARCHAR(200) NOT NULL,
                        [topic_id] INT NOT NULL,
                        [topic_title] NVARCHAR(200) NOT NULL,
                        [valid_question_count] INT NOT NULL,
                        [required_question_count] INT NOT NULL CONSTRAINT [DF_exam_app_required_question_count] DEFAULT (50),
                        [reason] NVARCHAR(1000) NOT NULL,
                        [notification_day] DATE NOT NULL,
                        [created_at_utc] DATETIME2 NOT NULL,
                        [is_delivered] BIT NOT NULL CONSTRAINT [DF_exam_app_is_delivered] DEFAULT (0),
                        [is_read] BIT NOT NULL CONSTRAINT [DF_exam_app_is_read] DEFAULT (0),
                        CONSTRAINT [PK_tbl_exam_application_notifications]
                            PRIMARY KEY ([notification_id])
                    );
                END;

                IF OBJECT_ID(N'[erpsystem].[tbl_exam_application_notifications]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'UX_exam_app_notification_dedupe_key'
                          AND [object_id] = OBJECT_ID(N'[erpsystem].[tbl_exam_application_notifications]'))
                        CREATE UNIQUE INDEX [UX_exam_app_notification_dedupe_key]
                            ON [erpsystem].[tbl_exam_application_notifications] ([dedupe_key]);

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'IX_exam_app_notification_admin_read_created'
                          AND [object_id] = OBJECT_ID(N'[erpsystem].[tbl_exam_application_notifications]'))
                        CREATE INDEX [IX_exam_app_notification_admin_read_created]
                            ON [erpsystem].[tbl_exam_application_notifications]
                                ([admin_user_id], [is_read], [created_at_utc]);

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'IX_exam_app_notification_admin_delivered'
                          AND [object_id] = OBJECT_ID(N'[erpsystem].[tbl_exam_application_notifications]'))
                        CREATE INDEX [IX_exam_app_notification_admin_delivered]
                            ON [erpsystem].[tbl_exam_application_notifications]
                                ([admin_user_id], [is_delivered]);
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally preserve this table and its data. It may have existed
            // before EF recorded this migration in __EFMigrationsHistory.
        }
    }
}