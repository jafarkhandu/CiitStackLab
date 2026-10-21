using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261021130000_AddCourseDiscountSettings")]
public sealed class AddCourseDiscountSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'erpsystem.tbltraining_courses', N'discount_mode') IS NULL
                ALTER TABLE [erpsystem].[tbltraining_courses] ADD [discount_mode] NVARCHAR(20) NULL;

            IF COL_LENGTH(N'erpsystem.tbltraining_courses', N'discount_value') IS NULL
                ALTER TABLE [erpsystem].[tbltraining_courses] ADD [discount_value] DECIMAL(10,2) NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'erpsystem.tbltraining_courses', N'discount_value') IS NOT NULL
                ALTER TABLE [erpsystem].[tbltraining_courses] DROP COLUMN [discount_value];

            IF COL_LENGTH(N'erpsystem.tbltraining_courses', N'discount_mode') IS NOT NULL
                ALTER TABLE [erpsystem].[tbltraining_courses] DROP COLUMN [discount_mode];
            """);
    }
}