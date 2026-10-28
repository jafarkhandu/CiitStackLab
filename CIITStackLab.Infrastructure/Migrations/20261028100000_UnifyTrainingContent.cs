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
                    + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.'
                    + QUOTENAME(OBJECT_NAME(fk.parent_object_id))
                    + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
                FROM sys.foreign_keys AS fk
                INNER JOIN sys.foreign_key_columns AS fkc
                    ON fkc.constraint_object_id = fk.object_id
                INNER JOIN sys.columns AS col
                    ON col.object_id = fkc.parent_object_id
                   AND col.column_id = fkc.parent_column_id
                WHERE fk.parent_object_id = OBJECT_ID(N'erpsystem.tbltraining_topic_contents')
                  AND col.name = N'note_id';

                IF LEN(@contentFkSql) > 0
                    EXEC sys.sp_executesql @contentFkSql;

                DECLARE @dropIndexSql nvarchar(max) = N'';
                SELECT @dropIndexSql += N'DROP INDEX ' + QUOTENAME(i.name)
                    + N' ON ' + QUOTENAME(OBJECT_SCHEMA_NAME(i.object_id)) + N'.'
                    + QUOTENAME(OBJECT_NAME(i.object_id)) + N';'
                FROM sys.indexes AS i
                INNER JOIN sys.index_columns AS ic
                    ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                INNER JOIN sys.columns AS col
                    ON col.object_id = ic.object_id AND col.column_id = ic.column_id
                WHERE i.object_id = OBJECT_ID(N'erpsystem.tbltraining_topic_contents')
                  AND col.name = N'note_id'
                  AND i.is_primary_key = 0
                  AND i.is_unique_constraint = 0;

                IF LEN(@dropIndexSql) > 0
                    EXEC sys.sp_executesql @dropIndexSql;

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
