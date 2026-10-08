using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorNotesToTopicChapters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tbltraining_notes_tbltraining_courses_course_id",
                schema: "erpsystem",
                table: "tbltraining_notes");

            migrationBuilder.DropIndex(
                name: "IX_tbltraining_notes_topic_id",
                schema: "erpsystem",
                table: "tbltraining_notes");

            migrationBuilder.DropIndex(
                name: "UX_tbltraining_notes_course_topic_page",
                schema: "erpsystem",
                table: "tbltraining_notes");

            migrationBuilder.DropColumn(
                name: "course_id",
                schema: "erpsystem",
                table: "tbltraining_notes");

            migrationBuilder.DropColumn(
                name: "title_with_number",
                schema: "erpsystem",
                table: "tbltraining_notes");

            migrationBuilder.RenameColumn(
                name: "page_id",
                schema: "erpsystem",
                table: "tbltraining_notes",
                newName: "chapter_id");

            migrationBuilder.CreateIndex(
                name: "UX_tbltraining_notes_topic_chapter",
                schema: "erpsystem",
                table: "tbltraining_notes",
                columns: new[] { "topic_id", "chapter_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_tbltraining_notes_topic_chapter",
                schema: "erpsystem",
                table: "tbltraining_notes");

            migrationBuilder.RenameColumn(
                name: "chapter_id",
                schema: "erpsystem",
                table: "tbltraining_notes",
                newName: "page_id");

            migrationBuilder.AddColumn<int>(
                name: "course_id",
                schema: "erpsystem",
                table: "tbltraining_notes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "title_with_number",
                schema: "erpsystem",
                table: "tbltraining_notes",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.AddForeignKey(
                name: "FK_tbltraining_notes_tbltraining_courses_course_id",
                schema: "erpsystem",
                table: "tbltraining_notes",
                column: "course_id",
                principalSchema: "erpsystem",
                principalTable: "tbltraining_courses",
                principalColumn: "course_id");
        }
    }
}
