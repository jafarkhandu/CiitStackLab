IF OBJECT_ID(N'[erpsystem].[tblstudent_topic_exam_attempts]', N'U') IS NULL
BEGIN
    CREATE TABLE [erpsystem].[tblstudent_topic_exam_attempts] (
        [exam_attempt_id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_tblstudent_topic_exam_attempts] PRIMARY KEY,
        [user_id] NVARCHAR(450) NOT NULL,
        [course_id] INT NOT NULL,
        [topic_id] INT NOT NULL,
        [scheduled_at_utc] DATETIME2 NOT NULL,
        [started_at_utc] DATETIME2 NULL,
        [submitted_at_utc] DATETIME2 NULL,
        [deadline_at_utc] DATETIME2 NULL,
        [status] NVARCHAR(30) NOT NULL,
        [question_ids_json] NVARCHAR(MAX) NOT NULL,
        [option_order_json] NVARCHAR(MAX) NOT NULL,
        [answers_json] NVARCHAR(MAX) NOT NULL,
        [device_token_hash] NVARCHAR(128) NULL,
        [warning_count] INT NOT NULL CONSTRAINT [DF_topic_exam_warning_count] DEFAULT (0),
        [score] INT NOT NULL CONSTRAINT [DF_topic_exam_score] DEFAULT (0),
        [total_questions] INT NOT NULL CONSTRAINT [DF_topic_exam_total_questions] DEFAULT (0),
        [passed] BIT NOT NULL CONSTRAINT [DF_topic_exam_passed] DEFAULT (0),
        [termination_reason] NVARCHAR(200) NULL,
        [created_at_utc] DATETIME2 NOT NULL
    );
    CREATE INDEX [IX_topic_exam_user_course_topic] ON [erpsystem].[tblstudent_topic_exam_attempts] ([user_id], [course_id], [topic_id], [created_at_utc]);
    CREATE INDEX [IX_topic_exam_user_topic_status] ON [erpsystem].[tblstudent_topic_exam_attempts] ([user_id], [course_id], [topic_id], [status]);
END;
GO

IF OBJECT_ID(N'[erpsystem].[tblstudent_topic_exam_security_events]', N'U') IS NULL
BEGIN
    CREATE TABLE [erpsystem].[tblstudent_topic_exam_security_events] (
        [event_id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_tblstudent_topic_exam_security_events] PRIMARY KEY,
        [exam_attempt_id] BIGINT NOT NULL,
        [event_type] NVARCHAR(80) NOT NULL,
        [details] NVARCHAR(1000) NULL,
        [occurred_at_utc] DATETIME2 NOT NULL
    );
    CREATE INDEX [IX_topic_exam_events_attempt_date] ON [erpsystem].[tblstudent_topic_exam_security_events] ([exam_attempt_id], [occurred_at_utc]);
END;
GO
