using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261021120000_AddCourseImagePath")]
public sealed class AddCourseImagePath : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'erpsystem.tbltraining_courses', N'course_image_path') IS NULL
            BEGIN
                ALTER TABLE [erpsystem].[tbltraining_courses]
                ADD [course_image_path] NVARCHAR(500) NULL;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'erpsystem.tbltraining_courses', N'course_image_path') IS NOT NULL
            BEGIN
                ALTER TABLE [erpsystem].[tbltraining_courses]
                DROP COLUMN [course_image_path];
            END;
            """);
    }
}