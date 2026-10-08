using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingNotesAndRemoveLessonHtmlContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbltraining_notes",
                schema: "erpsystem",
                columns: table => new
                {
                    note_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
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
                    table.ForeignKey(
                        name: "FK_tbltraining_notes_tbltraining_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "erpsystem",
                        principalTable: "tbltraining_courses",
                        principalColumn: "course_id");
                    table.ForeignKey(
                        name: "FK_tbltraining_notes_tbltraining_topics_topic_id",
                        column: x => x.topic_id,
                        principalSchema: "erpsystem",
                        principalTable: "tbltraining_topics",
                        principalColumn: "topic_id");
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbltraining_notes",
                schema: "erpsystem");
        }
    }
}
