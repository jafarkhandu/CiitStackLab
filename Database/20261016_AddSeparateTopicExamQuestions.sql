IF OBJECT_ID(N'[erpsystem].[tblstudent_topic_exam_questions]', N'U') IS NULL
BEGIN
    CREATE TABLE [erpsystem].[tblstudent_topic_exam_questions] (
        [exam_question_id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_tblstudent_topic_exam_questions] PRIMARY KEY,
        [topic_id] INT NOT NULL,
        [question] NVARCHAR(2000) NOT NULL,
        [option1] NVARCHAR(1000) NOT NULL,
        [option2] NVARCHAR(1000) NOT NULL,
        [option3] NVARCHAR(1000) NOT NULL,
        [option4] NVARCHAR(1000) NOT NULL,
        [correct_option_number] INT NOT NULL,
        [flag] INT NOT NULL CONSTRAINT [DF_topic_exam_question_flag] DEFAULT (0),
        [created_at_utc] DATETIME2 NOT NULL
    );
    CREATE INDEX [IX_topic_exam_questions_topic_id]
        ON [erpsystem].[tblstudent_topic_exam_questions] ([topic_id]);
END;
GO
