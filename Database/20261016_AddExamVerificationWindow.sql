IF COL_LENGTH(N'erpsystem.tblstudent_topic_exam_attempts', N'verification_started_at_utc') IS NULL
BEGIN
    ALTER TABLE [erpsystem].[tblstudent_topic_exam_attempts]
        ADD [verification_started_at_utc] datetime2 NULL;
END;
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_topic_exam_one_active_attempt'
      AND object_id = OBJECT_ID(N'[erpsystem].[tblstudent_topic_exam_attempts]')
)
BEGIN
    DROP INDEX [UX_topic_exam_one_active_attempt]
        ON [erpsystem].[tblstudent_topic_exam_attempts];
END;
GO

CREATE UNIQUE INDEX [UX_topic_exam_one_active_attempt]
    ON [erpsystem].[tblstudent_topic_exam_attempts] ([user_id], [course_id], [topic_id])
    WHERE [status] <> 'Passed'
      AND [status] <> 'Failed'
      AND [status] <> 'Terminated'
      AND [status] <> 'TimedOut';
GO

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016140000_AddExamVerificationWindow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016140000_AddExamVerificationWindow', N'10.0.0');
END;
GO
