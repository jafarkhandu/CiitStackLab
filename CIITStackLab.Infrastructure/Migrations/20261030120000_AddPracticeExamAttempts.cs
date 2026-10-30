using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261030120000_AddPracticeExamAttempts")]
public partial class AddPracticeExamAttempts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tblstudent_practice_exam_attempts",
            schema: "erpsystem",
            columns: table => new
            {
                attempt_id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                course_id = table.Column<int>(type: "int", nullable: false),
                topic_id = table.Column<int>(type: "int", nullable: false),
                question_ids_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                answers_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                score = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                total_questions = table.Column<int>(type: "int", nullable: false),
                started_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                submitted_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tblstudent_practice_exam_attempts", x => x.attempt_id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_practice_exam_user_course_topic_started",
            schema: "erpsystem",
            table: "tblstudent_practice_exam_attempts",
            columns: new[] { "user_id", "course_id", "topic_id", "started_at_utc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "tblstudent_practice_exam_attempts",
            schema: "erpsystem");
    }
}
