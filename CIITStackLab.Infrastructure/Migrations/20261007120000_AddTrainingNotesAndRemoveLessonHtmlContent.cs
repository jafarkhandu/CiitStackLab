using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations;

public partial class AddTrainingNotesAndRemoveLessonHtmlContent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "html_content",
            schema: "erpsystem",
            table: "tbltraining_topic_contents");

        migrationBuilder.CreateTable(
            name: "tbltraining_notes",
            schema: "erpsystem",
            columns: table => new
            {
                note_id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                course_id = table.Column<int>(type: "int", nullable: false),
                topic_id = table.Column<int>(type: "int", nullable: false),
                page_id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                title_with_number = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                html_content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                sort_order = table.Column<int>(type: "int", nullable: false),
                flag = table.Column<int>(type: "int", nullable: true),
                InsertedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RestoredAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbltraining_notes", x => x.note_id);
                table.ForeignKey("FK_tbltraining_notes_tbltraining_courses_course_id", x => x.course_id, "tbltraining_courses", "course_id", "erpsystem", onDelete: ReferentialAction.NoAction);
                table.ForeignKey("FK_tbltraining_notes_tbltraining_topics_topic_id", x => x.topic_id, "tbltraining_topics", "topic_id", "erpsystem", onDelete: ReferentialAction.NoAction);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tbltraining_notes_topic_id",
            schema: "erpsystem",
            table: "tbltraining_notes",
            column: "topic_id");

        migrationBuilder.CreateIndex(
            name: "UX_tbltraining_notes_course_topic_page",
            schema: "erpsystem",
            table: "tbltraining_notes",
            columns: new[] { "course_id", "topic_id", "page_id" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tbltraining_notes", schema: "erpsystem");
        migrationBuilder.AddColumn<string>(
            name: "html_content",
            schema: "erpsystem",
            table: "tbltraining_topic_contents",
            type: "nvarchar(max)",
            nullable: true);
    }
}