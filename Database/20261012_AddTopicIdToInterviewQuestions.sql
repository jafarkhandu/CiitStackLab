/*
    Allows interview questions to be attached directly to a chapter/topic.
    Existing content-linked records are retained; topic_id is nullable.
*/
IF OBJECT_ID(N'erpsystem.tblcontent_interview_questions', N'U') IS NULL
    THROW 51000, 'erpsystem.tblcontent_interview_questions does not exist.', 1;
IF OBJECT_ID(N'erpsystem.tbltraining_topics', N'U') IS NULL
    THROW 51000, 'erpsystem.tbltraining_topics does not exist.', 1;
GO
IF COL_LENGTH(N'erpsystem.tblcontent_interview_questions', N'topic_id') IS NULL
BEGIN
    ALTER TABLE [erpsystem].[tblcontent_interview_questions] ADD [topic_id] INT NULL;
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
    WHERE [name] = N'FK_tblcontent_interview_questions_tbltraining_topics_topic_id'
      AND [parent_object_id] = OBJECT_ID(N'erpsystem.tblcontent_interview_questions'))
BEGIN
    ALTER TABLE [erpsystem].[tblcontent_interview_questions] WITH CHECK
        ADD CONSTRAINT [FK_tblcontent_interview_questions_tbltraining_topics_topic_id]
        FOREIGN KEY ([topic_id]) REFERENCES [erpsystem].[tbltraining_topics] ([topic_id]);
    ALTER TABLE [erpsystem].[tblcontent_interview_questions]
        CHECK CONSTRAINT [FK_tblcontent_interview_questions_tbltraining_topics_topic_id];
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_tblcontent_interview_questions_topic_id'
      AND [object_id] = OBJECT_ID(N'erpsystem.tblcontent_interview_questions'))
BEGIN
    CREATE INDEX [IX_tblcontent_interview_questions_topic_id]
        ON [erpsystem].[tblcontent_interview_questions] ([topic_id]);
END;
GO
