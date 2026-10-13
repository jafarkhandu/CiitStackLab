using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileTrainingNoteAndPurchaseIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'erpsystem.tblstudent_course_purchases', N'U') IS NOT NULL
                   AND EXISTS
                   (
                       SELECT 1
                       FROM sys.indexes
                       WHERE [name] = N'IX_tblstudent_course_purchases_course_id'
                         AND [object_id] =
                             OBJECT_ID(N'erpsystem.tblstudent_course_purchases')
                   )
                BEGIN
                    DROP INDEX [IX_tblstudent_course_purchases_course_id]
                    ON [erpsystem].[tblstudent_course_purchases];
                END;

                IF OBJECT_ID(N'erpsystem.tbltraining_notes', N'U') IS NOT NULL
                   AND COL_LENGTH(
                       N'erpsystem.tbltraining_notes',
                       N'RestoredAt'
                   ) IS NULL
                BEGIN
                    ALTER TABLE [erpsystem].[tbltraining_notes]
                    ADD [RestoredAt] datetime2 NULL;
                END;
                """);
        }
    }
}
