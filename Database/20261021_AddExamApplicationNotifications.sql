IF OBJECT_ID(N'[erpsystem].[tbl_exam_application_notifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [erpsystem].[tbl_exam_application_notifications]
    (
        [notification_id] BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT [PK_tbl_exam_application_notifications] PRIMARY KEY,
        [dedupe_key] CHAR(64) NOT NULL,
        [admin_user_id] NVARCHAR(450) NOT NULL,
        [student_user_id] NVARCHAR(450) NOT NULL,
        [student_name] NVARCHAR(256) NOT NULL,
        [course_id] INT NOT NULL,
        [course_title] NVARCHAR(200) NOT NULL,
        [topic_id] INT NOT NULL,
        [topic_title] NVARCHAR(200) NOT NULL,
        [valid_question_count] INT NOT NULL,
        [required_question_count] INT NOT NULL
            CONSTRAINT [DF_exam_app_notification_required_count] DEFAULT (50),
        [reason] NVARCHAR(1000) NOT NULL,
        [notification_day] DATE NOT NULL,
        [created_at_utc] DATETIME2 NOT NULL,
        [is_delivered] BIT NOT NULL
            CONSTRAINT [DF_exam_app_notification_is_delivered] DEFAULT (0),
        [is_read] BIT NOT NULL
            CONSTRAINT [DF_exam_app_notification_is_read] DEFAULT (0)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_exam_app_notification_dedupe_key'
      AND object_id = OBJECT_ID(N'[erpsystem].[tbl_exam_application_notifications]')
)
BEGIN
    CREATE UNIQUE INDEX [UX_exam_app_notification_dedupe_key]
        ON [erpsystem].[tbl_exam_application_notifications] ([dedupe_key]);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_exam_app_notification_admin_read_created'
      AND object_id = OBJECT_ID(N'[erpsystem].[tbl_exam_application_notifications]')
)
BEGIN
    CREATE INDEX [IX_exam_app_notification_admin_read_created]
        ON [erpsystem].[tbl_exam_application_notifications]
        ([admin_user_id], [is_read], [created_at_utc]);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_exam_app_notification_admin_delivered'
      AND object_id = OBJECT_ID(N'[erpsystem].[tbl_exam_application_notifications]')
)
BEGIN
    CREATE INDEX [IX_exam_app_notification_admin_delivered]
        ON [erpsystem].[tbl_exam_application_notifications]
        ([admin_user_id], [is_delivered]);
END;
GO
