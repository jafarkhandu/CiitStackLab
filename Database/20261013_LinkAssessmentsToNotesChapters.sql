/*
    Links all assessment question types directly to the Notes chapter row.
    note_id references erpsystem.tbltraining_notes.note_id.
    Existing content_id and topic_id links are deliberately retained for
    backwards compatibility. This script does not modify or delete data.
*/
IF OBJECT_ID(N'erpsystem.tbltraining_notes', N'U') IS NULL
    THROW 51000, 'erpsystem.tbltraining_notes does not exist.', 1;
IF OBJECT_ID(N'erpsystem.tblcontent_questions', N'U') IS NULL
    THROW 51000, 'erpsystem.tblcontent_questions does not exist.', 1;
IF OBJECT_ID(N'erpsystem.tblcontent_interview_questions', N'U') IS NULL
    THROW 51000, 'erpsystem.tblcontent_interview_questions does not exist.', 1;
IF OBJECT_ID(N'erpsystem.tblcontent_program_questions', N'U') IS NULL
    THROW 51000, 'erpsystem.tblcontent_program_questions does not exist.', 1;
GO

IF COL_LENGTH(N'erpsystem.tblcontent_questions', N'note_id') IS NULL
    ALTER TABLE [erpsystem].[tblcontent_questions] ADD [note_id] INT NULL;
IF COL_LENGTH(N'erpsystem.tblcontent_interview_questions', N'note_id') IS NULL
    ALTER TABLE [erpsystem].[tblcontent_interview_questions] ADD [note_id] INT NULL;
IF COL_LENGTH(N'erpsystem.tblcontent_program_questions', N'note_id') IS NULL
    ALTER TABLE [erpsystem].[tblcontent_program_questions] ADD [note_id] INT NULL;
GO

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
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_tblcontent_questions_note_id'
      AND [object_id] = OBJECT_ID(N'erpsystem.tblcontent_questions'))
    CREATE INDEX [IX_tblcontent_questions_note_id]
        ON [erpsystem].[tblcontent_questions] ([note_id]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_tblcontent_interview_questions_note_id'
      AND [object_id] = OBJECT_ID(N'erpsystem.tblcontent_interview_questions'))
    CREATE INDEX [IX_tblcontent_interview_questions_note_id]
        ON [erpsystem].[tblcontent_interview_questions] ([note_id]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_tblcontent_program_questions_note_id'
      AND [object_id] = OBJECT_ID(N'erpsystem.tblcontent_program_questions'))
    CREATE INDEX [IX_tblcontent_program_questions_note_id]
        ON [erpsystem].[tblcontent_program_questions] ([note_id]);
GO
