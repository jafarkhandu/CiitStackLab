using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261016140000_AddExamVerificationWindow")]
    public partial class AddExamVerificationWindow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "verification_started_at_utc",
                schema: "erpsystem",
                table: "tblstudent_topic_exam_attempts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.DropIndex(
                name: "UX_topic_exam_one_active_attempt",
                schema: "erpsystem",
                table: "tblstudent_topic_exam_attempts");

            migrationBuilder.CreateIndex(
                name: "UX_topic_exam_one_active_attempt",
                schema: "erpsystem",
                table: "tblstudent_topic_exam_attempts",
                columns: new[] { "user_id", "course_id", "topic_id" },
                unique: true,
                filter: "[status] <> 'Passed' AND [status] <> 'Failed' AND [status] <> 'Terminated' AND [status] <> 'TimedOut'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_topic_exam_one_active_attempt",
                schema: "erpsystem",
                table: "tblstudent_topic_exam_attempts");

            migrationBuilder.CreateIndex(
                name: "UX_topic_exam_one_active_attempt",
                schema: "erpsystem",
                table: "tblstudent_topic_exam_attempts",
                columns: new[] { "user_id", "course_id", "topic_id" },
                unique: true,
                filter: "[status] <> 'Passed' AND [status] <> 'Failed' AND [status] <> 'Terminated'");

            migrationBuilder.DropColumn(
                name: "verification_started_at_utc",
                schema: "erpsystem",
                table: "tblstudent_topic_exam_attempts");
        }
    }
}
