/*
    Stores student enrollment requests and administrator-granted course access.
    This script creates a new table only; existing ERP tables are not altered.
    Paid course access is manually approved; no payment gateway is assumed.
*/

IF OBJECT_ID(N'erpsystem.tblstudent_course_enrollments', N'U') IS NULL
BEGIN
    CREATE TABLE [erpsystem].[tblstudent_course_enrollments]
    (
        [enrollment_id] INT IDENTITY(1,1) NOT NULL,
        [user_id] NVARCHAR(450) NOT NULL,
        [course_id] INT NOT NULL,
        [status] NVARCHAR(30) NOT NULL,
        [price_at_enrollment] DECIMAL(18,2) NOT NULL,
        [requested_at] DATETIME2 NOT NULL,
        [reviewed_at] DATETIME2 NULL,
        [reviewed_by_user_id] NVARCHAR(450) NULL,

        CONSTRAINT [PK_tblstudent_course_enrollments]
            PRIMARY KEY CLUSTERED ([enrollment_id]),

        CONSTRAINT [FK_tblstudent_course_enrollments_AspNetUsers]
            FOREIGN KEY ([user_id])
            REFERENCES [erpsystem].[AspNetUsers] ([Id]),

        CONSTRAINT [FK_tblstudent_course_enrollments_courses]
            FOREIGN KEY ([course_id])
            REFERENCES [erpsystem].[tbltraining_courses] ([course_id]),

        CONSTRAINT [FK_tblstudent_course_enrollments_reviewed_by]
            FOREIGN KEY ([reviewed_by_user_id])
            REFERENCES [erpsystem].[AspNetUsers] ([Id]),

        CONSTRAINT [CK_tblstudent_course_enrollments_status]
            CHECK ([status] IN (N'PendingApproval', N'Active', N'Rejected')),

        CONSTRAINT [CK_tblstudent_course_enrollments_price]
            CHECK ([price_at_enrollment] >= 0)
    );
END;
GO

IF OBJECT_ID(N'erpsystem.tblstudent_course_enrollments', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE [name] = N'UX_tblstudent_course_enrollments_user_course'
         AND [object_id] = OBJECT_ID(N'erpsystem.tblstudent_course_enrollments')
   )
BEGIN
    CREATE UNIQUE INDEX [UX_tblstudent_course_enrollments_user_course]
        ON [erpsystem].[tblstudent_course_enrollments] ([user_id], [course_id]);
END;
GO

IF OBJECT_ID(N'erpsystem.tblstudent_course_enrollments', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE [name] = N'IX_tblstudent_course_enrollments_status_date'
         AND [object_id] = OBJECT_ID(N'erpsystem.tblstudent_course_enrollments')
   )
BEGIN
    CREATE INDEX [IX_tblstudent_course_enrollments_status_date]
        ON [erpsystem].[tblstudent_course_enrollments] ([status], [requested_at]);
END;
GO
