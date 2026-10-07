IF COL_LENGTH(N'erpsystem.tbltraining_topic_contents', N'html_content') IS NULL
BEGIN
    ALTER TABLE [erpsystem].[tbltraining_topic_contents]
        ADD [html_content] NVARCHAR(MAX) NULL;
END;
