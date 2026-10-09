/*
    Adds student lesson resume and completion tracking.

    Safe to run more than once. Existing ERP tables and their data are not
    altered; the script only creates the new progress table if it is missing.
*/

IF OBJECT_ID(N'erpsystem.tblstudent_lesson_progress', N'U') IS NULL
BEGIN
    CREATE TABLE [erpsystem].[tblstudent_lesson_progress]
    (
        [progress_id] INT IDENTITY(1,1) NOT NULL,
        [user_id] NVARCHAR(450) NOT NULL,
        [course_id] INT NOT NULL,
        [content_id] INT NOT NULL,
        [is_completed] BIT NOT NULL
            CONSTRAINT [DF_tblstudent_lesson_progress_is_completed]
            DEFAULT (0),
        [last_accessed_at] DATETIME2 NOT NULL,
        [completed_at] DATETIME2 NULL,

        CONSTRAINT [PK_tblstudent_lesson_progress]
            PRIMARY KEY CLUSTERED ([progress_id]),

        CONSTRAINT [FK_tblstudent_lesson_progress_AspNetUsers]
            FOREIGN KEY ([user_id])
            REFERENCES [erpsystem].[AspNetUsers] ([Id]),

        CONSTRAINT [FK_tblstudent_lesson_progress_tbltraining_courses]
            FOREIGN KEY ([course_id])
            REFERENCES [erpsystem].[tbltraining_courses] ([course_id]),

        CONSTRAINT [FK_tblstudent_lesson_progress_tbltraining_topic_contents]
            FOREIGN KEY ([content_id])
            REFERENCES [erpsystem].[tbltraining_topic_contents] ([content_id])
    );
END;
GO

IF OBJECT_ID(N'erpsystem.tblstudent_lesson_progress', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE [name] = N'UX_tblstudent_lesson_progress_user_course_content'
         AND [object_id] = OBJECT_ID(N'erpsystem.tblstudent_lesson_progress')
   )
BEGIN
    CREATE UNIQUE INDEX [UX_tblstudent_lesson_progress_user_course_content]
        ON [erpsystem].[tblstudent_lesson_progress]
        (
            [user_id],
            [course_id],
            [content_id]
        );
END;
GO
