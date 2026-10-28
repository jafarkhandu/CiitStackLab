/*
    CIITStackLab migration-history reconciliation
    Branch: fix/unified-content-table

    PURPOSE:
    The existing stacklabdb schema is already provisioned, but
    erpsystem.__EFMigrationsHistory is empty. Running Update-Database would
    replay historical migrations against tables/columns that already exist.

    SAFETY:
    - Back up the database before running this script.
    - This script does NOT create/alter/drop application tables or columns.
    - It aborts if key current-schema prerequisites are missing or the legacy
      tbltraining_notes table / topic-content note_id column still exists.
    - It records the known migrations as applied only after the checks pass.
    - Run against the intended database (stacklabdb) and review before execution.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'stacklabdb'
    THROW 51000, 'Wrong database. Select stacklabdb before running this script.', 1;

IF OBJECT_ID(N'erpsystem.__EFMigrationsHistory', N'U') IS NULL
    THROW 51000, 'erpsystem.__EFMigrationsHistory is missing. Stop and investigate before reconciliation.', 1;

DECLARE @Missing nvarchar(max) = N'';

DECLARE @RequiredTables TABLE (TableName sysname NOT NULL PRIMARY KEY);
INSERT INTO @RequiredTables (TableName)
VALUES
    (N'tbltraining_courses'),
    (N'tbltraining_topics'),
    (N'tbltraining_course_topics'),
    (N'tbltraining_topic_contents'),
    (N'tblcontent_questions'),
    (N'tblcontent_interview_questions'),
    (N'tblcontent_program_questions'),
    (N'tblcontent_program_answers'),
    (N'tblstudent_course_purchases'),
    (N'tblpayment_transactions'),
    (N'tblstudent_assessment_attempts'),
    (N'tblstudent_lesson_progress'),
    (N'tblstudent_topic_exam_attempts'),
    (N'tblstudent_topic_exam_questions'),
    (N'tblstudent_topic_exam_security_events'),
    (N'tbl_exam_application_notifications');

SELECT @Missing += N'Missing table: erpsystem.' + t.TableName + N'. '
FROM @RequiredTables AS t
WHERE OBJECT_ID(N'erpsystem.' + t.TableName, N'U') IS NULL;

IF COL_LENGTH(N'erpsystem.tbltraining_topics', N'price') IS NULL
    SET @Missing += N'Missing column: tbltraining_topics.price. ';
IF COL_LENGTH(N'erpsystem.tbltraining_topics', N'duration_minutes') IS NULL
    SET @Missing += N'Missing column: tbltraining_topics.duration_minutes. ';
IF COL_LENGTH(N'erpsystem.tbltraining_topic_contents', N'content_id') IS NULL
    SET @Missing += N'Missing column: tbltraining_topic_contents.content_id. ';
IF COL_LENGTH(N'erpsystem.tbltraining_topic_contents', N'topic_id') IS NULL
    SET @Missing += N'Missing column: tbltraining_topic_contents.topic_id. ';
IF COL_LENGTH(N'erpsystem.tbltraining_topic_contents', N'html_content') IS NULL
    SET @Missing += N'Missing column: tbltraining_topic_contents.html_content. ';
IF COL_LENGTH(N'erpsystem.tbltraining_topic_contents', N'video_name') IS NULL
    SET @Missing += N'Missing column: tbltraining_topic_contents.video_name. ';
IF COL_LENGTH(N'erpsystem.tbltraining_topic_contents', N'slides') IS NULL
    SET @Missing += N'Missing column: tbltraining_topic_contents.slides. ';
IF COL_LENGTH(N'erpsystem.tblcontent_questions', N'note_id') IS NULL
    SET @Missing += N'Missing column: tblcontent_questions.note_id. ';
IF COL_LENGTH(N'erpsystem.tblcontent_interview_questions', N'note_id') IS NULL
    SET @Missing += N'Missing column: tblcontent_interview_questions.note_id. ';
IF COL_LENGTH(N'erpsystem.tblcontent_program_questions', N'note_id') IS NULL
    SET @Missing += N'Missing column: tblcontent_program_questions.note_id. ';

IF OBJECT_ID(N'erpsystem.tbltraining_notes', N'U') IS NOT NULL
    SET @Missing += N'Legacy table erpsystem.tbltraining_notes still exists. ';
IF COL_LENGTH(N'erpsystem.tbltraining_topic_contents', N'note_id') IS NOT NULL
    SET @Missing += N'Legacy column tbltraining_topic_contents.note_id still exists. ';

IF LEN(@Missing) > 0
BEGIN
    DECLARE @ErrorMessage nvarchar(2048) =
        N'Reconciliation stopped. No migration history was changed. ' + @Missing;
    THROW 51001, @ErrorMessage, 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Migrations TABLE
    (
        MigrationId nvarchar(150) NOT NULL PRIMARY KEY,
        ProductVersion nvarchar(32) NOT NULL
    );

    INSERT INTO @Migrations (MigrationId, ProductVersion)
    VALUES
        (N'20261007091754_AddTopicPricingAndDuration', N'10.0.13'),
        (N'20261007122009_AddTrainingNotesAndRemoveLessonHtmlContent', N'10.0.13'),
        (N'20261008104854_RefactorNotesToTopicChapters', N'10.0.13'),
        (N'20261008112602_AddContentNoteRelation', N'10.0.13'),
        (N'20261013120000_LinkAssessmentsToNotesChapters', N'10.0.13'),
        (N'20261013143000_AddStudentCoursePurchases', N'10.0.13'),
        (N'20261013150000_ReconcileTrainingNoteAndPurchaseIndex', N'10.0.13'),
        (N'20261016093000_AllowTopicAssessmentAttempts', N'10.0.13'),
        (N'20261016120000_AddTopicExaminationSystem', N'10.0.13'),
        (N'20261016130000_AddSeparateTopicExamQuestions', N'10.0.13'),
        (N'20261016140000_AddExamVerificationWindow', N'10.0.13'),
        (N'20261021090000_AddExamApplicationNotifications', N'10.0.13'),
        (N'20261021105838_SyncCourseImageModel', N'10.0.13'),
        (N'20261021120000_AddCourseImagePath', N'10.0.13'),
        (N'20261021130000_AddCourseDiscountSettings', N'10.0.13'),
        (N'20261022130000_AddExamRetakePaymentColumns', N'10.0.13'),
        (N'20261022130000_AddExamRetakePaymentMetadata', N'10.0.13'),
        (N'20261028100000_UnifyTrainingContent', N'10.0.13');

    INSERT INTO [erpsystem].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    SELECT m.MigrationId, m.ProductVersion
    FROM @Migrations AS m
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM [erpsystem].[__EFMigrationsHistory] AS h
        WHERE h.MigrationId = m.MigrationId
    );

    COMMIT TRANSACTION;

    SELECT MigrationId, ProductVersion
    FROM [erpsystem].[__EFMigrationsHistory]
    ORDER BY MigrationId;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
