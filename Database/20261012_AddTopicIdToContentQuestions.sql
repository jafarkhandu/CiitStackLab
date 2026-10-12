/*
    Allows MCQs to be attached directly to a chapter/topic while keeping
    existing content-linked questions intact.
    The column is nullable; existing question rows are not rewritten.
*/

IF COL_LENGTH(N'erpsystem.tblcontent_questions', N'topic_id') IS NULL
BEGIN
    ALTER TABLE [erpsystem].[tblcontent_questions]
        ADD [topic_id] INT NULL;
END;
GO

IF OBJECT_ID(N'erpsystem.tblcontent_questions', N'U') IS NOT NULL
   AND OBJECT_ID(N'erpsystem.tbltraining_topics', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.foreign_keys
       WHERE [name] = N'FK_tblcontent_questions_tbltraining_topics_topic_id'
         AND [parent_object_id] = OBJECT_ID(N'erpsystem.tblcontent_questions')
   )
BEGIN
    ALTER TABLE [erpsystem].[tblcontent_questions]
        WITH CHECK ADD CONSTRAINT [FK_tblcontent_questions_tbltraining_topics_topic_id]
        FOREIGN KEY ([topic_id])
        REFERENCES [erpsystem].[tbltraining_topics] ([topic_id]);

    ALTER TABLE [erpsystem].[tblcontent_questions]
        CHECK CONSTRAINT [FK_tblcontent_questions_tbltraining_topics_topic_id];
END;
GO

IF OBJECT_ID(N'erpsystem.tblcontent_questions', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE [name] = N'IX_tblcontent_questions_topic_id'
         AND [object_id] = OBJECT_ID(N'erpsystem.tblcontent_questions')
   )
BEGIN
    CREATE INDEX [IX_tblcontent_questions_topic_id]
        ON [erpsystem].[tblcontent_questions] ([topic_id]);
END;
GO
