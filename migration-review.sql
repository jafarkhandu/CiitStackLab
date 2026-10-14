BEGIN TRANSACTION;
IF OBJECT_ID(N'erpsystem.tbltraining_notes', N'U') IS NULL
BEGIN
    THROW 51000, 'erpsystem.tbltraining_notes does not exist.', 1;
END;
IF OBJECT_ID(N'erpsystem.tblcontent_questions', N'U') IS NULL
BEGIN
    THROW 51000, 'erpsystem.tblcontent_questions does not exist.', 1;
END;
IF OBJECT_ID(N'erpsystem.tblcontent_interview_questions', N'U') IS NULL
BEGIN
    THROW 51000, 'erpsystem.tblcontent_interview_questions does not exist.', 1;
END;
IF OBJECT_ID(N'erpsystem.tblcontent_program_questions', N'U') IS NULL
BEGIN
    THROW 51000, 'erpsystem.tblcontent_program_questions does not exist.', 1;
END;

IF COL_LENGTH(N'erpsystem.tblcontent_questions', N'note_id') IS NULL
BEGIN
    ALTER TABLE [erpsystem].[tblcontent_questions] ADD [note_id] INT NULL;
END;
IF COL_LENGTH(N'erpsystem.tblcontent_interview_questions', N'note_id') IS NULL
BEGIN
    ALTER TABLE [erpsystem].[tblcontent_interview_questions] ADD [note_id] INT NULL;
END;
IF COL_LENGTH(N'erpsystem.tblcontent_program_questions', N'note_id') IS NULL
BEGIN
    ALTER TABLE [erpsystem].[tblcontent_program_questions] ADD [note_id] INT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = N'FK_tblcontent_questions_tbltraining_notes_note_id'
      AND [parent_object_id] = OBJECT_ID(N'erpsystem.tblcontent_questions'))
BEGIN
    ALTER TABLE [erpsystem].[tblcontent_questions] WITH CHECK
        ADD CONSTRAINT [FK_tblcontent_questions_tbltraining_notes_note_id]
        FOREIGN KEY ([note_id]) REFERENCES [erpsystem].[tbltraining_notes] ([note_id]);
    ALTER TABLE [erpsystem].[tblcontent_questions]
        CHECK CONSTRAINT [FK_tblcontent_questions_tbltraining_notes_note_id];
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = N'FK_tblcontent_interview_questions_tbltraining_notes_note_id'
      AND [parent_object_id] = OBJECT_ID(N'erpsystem.tblcontent_interview_questions'))
BEGIN
    ALTER TABLE [erpsystem].[tblcontent_interview_questions] WITH CHECK
        ADD CONSTRAINT [FK_tblcontent_interview_questions_tbltraining_notes_note_id]
        FOREIGN KEY ([note_id]) REFERENCES [erpsystem].[tbltraining_notes] ([note_id]);
    ALTER TABLE [erpsystem].[tblcontent_interview_questions]
        CHECK CONSTRAINT [FK_tblcontent_interview_questions_tbltraining_notes_note_id];
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = N'FK_tblcontent_program_questions_tbltraining_notes_note_id'
      AND [parent_object_id] = OBJECT_ID(N'erpsystem.tblcontent_program_questions'))
BEGIN
    ALTER TABLE [erpsystem].[tblcontent_program_questions] WITH CHECK
        ADD CONSTRAINT [FK_tblcontent_program_questions_tbltraining_notes_note_id]
        FOREIGN KEY ([note_id]) REFERENCES [erpsystem].[tbltraining_notes] ([note_id]);
    ALTER TABLE [erpsystem].[tblcontent_program_questions]
        CHECK CONSTRAINT [FK_tblcontent_program_questions_tbltraining_notes_note_id];
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_tblcontent_questions_note_id'
      AND [object_id] = OBJECT_ID(N'erpsystem.tblcontent_questions'))
BEGIN
    CREATE INDEX [IX_tblcontent_questions_note_id]
        ON [erpsystem].[tblcontent_questions] ([note_id]);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_tblcontent_interview_questions_note_id'
      AND [object_id] = OBJECT_ID(N'erpsystem.tblcontent_interview_questions'))
BEGIN
    CREATE INDEX [IX_tblcontent_interview_questions_note_id]
        ON [erpsystem].[tblcontent_interview_questions] ([note_id]);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_tblcontent_program_questions_note_id'
      AND [object_id] = OBJECT_ID(N'erpsystem.tblcontent_program_questions'))
BEGIN
    CREATE INDEX [IX_tblcontent_program_questions_note_id]
        ON [erpsystem].[tblcontent_program_questions] ([note_id]);
END;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261013120000_LinkAssessmentsToNotesChapters', N'10.0.0');

COMMIT;
GO

BEGIN TRANSACTION;
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

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261013143000_AddStudentCoursePurchases', N'10.0.0');

COMMIT;
GO

BEGIN TRANSACTION;
IF COL_LENGTH(N'erpsystem.tbltraining_notes', N'RestoredAt') IS NOT NULL
BEGIN
    ALTER TABLE [erpsystem].[tbltraining_notes]
    DROP COLUMN [RestoredAt];
END;

IF OBJECT_ID(N'erpsystem.tblstudent_course_purchases', N'U') IS NULL
BEGIN
    THROW 50001,
        'tblstudent_course_purchases must exist before this migration.',
        1;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_tblstudent_course_purchases_course_id'
      AND [object_id] =
          OBJECT_ID(N'erpsystem.tblstudent_course_purchases')
)
BEGIN
    CREATE INDEX [IX_tblstudent_course_purchases_course_id]
    ON [erpsystem].[tblstudent_course_purchases] ([course_id]);
END;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261013150000_ReconcileTrainingNoteAndPurchaseIndex', N'10.0.0');

COMMIT;
GO

