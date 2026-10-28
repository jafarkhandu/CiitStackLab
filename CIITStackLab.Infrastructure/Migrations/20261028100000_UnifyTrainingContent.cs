using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261028100000_UnifyTrainingContent")]
public partial class UnifyTrainingContent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'erpsystem.tbltraining_topic_contents', N'html_content') IS NULL
            BEGIN
                ALTER TABLE [erpsystem].[tbltraining_topic_contents]
                ADD [html_content] nvarchar(max) NULL;
            END;

            IF OBJECT_ID(N'erpsystem.tbltraining_notes', N'U') IS NOT NULL
            BEGIN
                DECLARE @dropSql nvarchar(max) = N'';

                SELECT @dropSql += N'ALTER TABLE '
                    + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.'
                    + QUOTENAME(OBJECT_NAME(parent_object_id))
                    + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';'
                FROM sys.foreign_keys
                WHERE referenced_object_id = OBJECT_ID(N'erpsystem.tbltraining_notes');

                IF LEN(@dropSql) > 0
                    EXEC sys.sp_executesql @dropSql;

                DROP TABLE [erpsystem].[tbltraining_notes];
            END;

            IF COL_LENGTH(N'erpsystem.tbltraining_topic_contents', N'note_id') IS NOT NULL
            BEGIN
                DECLARE @contentFkSql nvarchar(max) = N'';

                SELECT @contentFkSql += N'ALTER TABLE '
                    + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.'
                    + QUOTENAME(OBJECT_NAME(parent_object_id))
                    + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';'
                FROM sys.foreign_keys
                WHERE parent_object_id = OBJECT_ID(N'erpsystem.tbltraining_topic_contents')
                  AND COL_NAME(parent_object_id, (
                      SELECT column_id FROM sys.columns
                      WHERE object_id = parent_object_id AND name = N'note_id'
                  )) = N'note_id';

                IF LEN(@contentFkSql) > 0
                    EXEC sys.sp_executesql @contentFkSql;

                ALTER TABLE [erpsystem].[tbltraining_topic_contents] DROP COLUMN [note_id];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'erpsystem.tbltraining_topic_contents', N'note_id') IS NULL
            BEGIN
                ALTER TABLE [erpsystem].[tbltraining_topic_contents]
                ADD [note_id] int NULL;
            END;

            IF COL_LENGTH(N'erpsystem.tbltraining_topic_contents', N'html_content') IS NOT NULL
            BEGIN
                ALTER TABLE [erpsystem].[tbltraining_topic_contents] DROP COLUMN [html_content];
            END;
            """);
    }
}
