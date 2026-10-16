using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261016093000_AllowTopicAssessmentAttempts")]
    public partial class AllowTopicAssessmentAttempts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "content_id",
                schema: "erpsystem",
                table: "tblstudent_assessment_attempts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [erpsystem].[tblstudent_assessment_attempts] SET [content_id] = 0 WHERE [content_id] IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "content_id",
                schema: "erpsystem",
                table: "tblstudent_assessment_attempts",
                type: "int",
                nullable: false,
                oldClrType: typeof(int?),
                oldType: "int",
                oldNullable: true);
        }
    }
}