using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261016130000_AddSeparateTopicExamQuestions")]
    public partial class AddSeparateTopicExamQuestions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tblstudent_topic_exam_questions",
                schema: "erpsystem",
                columns: table => new
                {
                    exam_question_id = table.Column<int>(type: "int").Annotation("SqlServer:Identity", "1, 1"),
                    topic_id = table.Column<int>(type: "int", nullable: false),
                    question = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    option1 = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    option2 = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    option3 = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    option4 = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    correct_option_number = table.Column<int>(type: "int", nullable: false),
                    flag = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblstudent_topic_exam_questions", x => x.exam_question_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_topic_exam_questions_topic_id",
                schema: "erpsystem",
                table: "tblstudent_topic_exam_questions",
                column: "topic_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "tblstudent_topic_exam_questions", schema: "erpsystem");
        }
    }
}
