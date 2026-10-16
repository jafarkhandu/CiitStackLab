using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261016120000_AddTopicExaminationSystem")]
    public partial class AddTopicExaminationSystem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tblstudent_topic_exam_attempts",
                schema: "erpsystem",
                columns: table => new
                {
                    exam_attempt_id = table.Column<long>(type: "bigint").Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    course_id = table.Column<int>(type: "int", nullable: false),
                    topic_id = table.Column<int>(type: "int", nullable: false),
                    scheduled_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    submitted_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    deadline_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    question_ids_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    option_order_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    answers_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    device_token_hash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    warning_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    score = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    total_questions = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    passed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    termination_reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_tblstudent_topic_exam_attempts", x => x.exam_attempt_id));

            migrationBuilder.CreateTable(
                name: "tblstudent_topic_exam_security_events",
                schema: "erpsystem",
                columns: table => new
                {
                    event_id = table.Column<long>(type: "bigint").Annotation("SqlServer:Identity", "1, 1"),
                    exam_attempt_id = table.Column<long>(type: "bigint", nullable: false),
                    event_type = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_tblstudent_topic_exam_security_events", x => x.event_id));

            migrationBuilder.CreateIndex(
                name: "IX_topic_exam_user_course_topic",
                schema: "erpsystem",
                table: "tblstudent_topic_exam_attempts",
                columns: new[] { "user_id", "course_id", "topic_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_topic_exam_user_topic_status",
                schema: "erpsystem",
                table: "tblstudent_topic_exam_attempts",
                columns: new[] { "user_id", "course_id", "topic_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_topic_exam_events_attempt_date",
                schema: "erpsystem",
                table: "tblstudent_topic_exam_security_events",
                columns: new[] { "exam_attempt_id", "occurred_at_utc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "tblstudent_topic_exam_security_events", schema: "erpsystem");
            migrationBuilder.DropTable(name: "tblstudent_topic_exam_attempts", schema: "erpsystem");
        }
    }
}
