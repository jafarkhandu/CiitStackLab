/*
    Persists student MCQ attempts and post-submission answer snapshots.
    Safe to run more than once. Existing ERP tables are not modified.
*/

IF OBJECT_ID(N'erpsystem.tblstudent_assessment_attempts', N'U') IS NULL
BEGIN
    CREATE TABLE [erpsystem].[tblstudent_assessment_attempts]
    (
        [attempt_id] INT IDENTITY(1,1) NOT NULL,
        [user_id] NVARCHAR(450) NOT NULL,
        [course_id] INT NOT NULL,
        [content_id] INT NOT NULL,
        [score] INT NOT NULL,
        [total_questions] INT NOT NULL,
        [answers_json] NVARCHAR(MAX) NOT NULL,
        [submitted_at] DATETIME2 NOT NULL,

        CONSTRAINT [PK_tblstudent_assessment_attempts]
            PRIMARY KEY CLUSTERED ([attempt_id]),

        CONSTRAINT [FK_tblstudent_assessment_attempts_AspNetUsers]
            FOREIGN KEY ([user_id])
            REFERENCES [erpsystem].[AspNetUsers] ([Id]),

        CONSTRAINT [FK_tblstudent_assessment_attempts_courses]
            FOREIGN KEY ([course_id])
            REFERENCES [erpsystem].[tbltraining_courses] ([course_id]),

        CONSTRAINT [FK_tblstudent_assessment_attempts_contents]
            FOREIGN KEY ([content_id])
            REFERENCES [erpsystem].[tbltraining_topic_contents] ([content_id]),

        CONSTRAINT [CK_tblstudent_assessment_attempts_score]
            CHECK ([score] >= 0 AND [total_questions] > 0 AND [score] <= [total_questions])
    );
END;
GO

IF OBJECT_ID(N'erpsystem.tblstudent_assessment_attempts', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE [name] = N'IX_tblstudent_assessment_attempts_user_course_content_date'
         AND [object_id] = OBJECT_ID(N'erpsystem.tblstudent_assessment_attempts')
   )
BEGIN
    CREATE INDEX [IX_tblstudent_assessment_attempts_user_course_content_date]
        ON [erpsystem].[tblstudent_assessment_attempts]
        (
            [user_id],
            [course_id],
            [content_id],
            [submitted_at] DESC
        );
END;
GO
