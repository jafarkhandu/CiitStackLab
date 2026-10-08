using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentNoteRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "note_id",
                schema: "erpsystem",
                table: "tbltraining_topic_contents",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbltraining_topic_contents_note_id",
                schema: "erpsystem",
                table: "tbltraining_topic_contents",
                column: "note_id");

            migrationBuilder.AddForeignKey(
                name: "FK_tbltraining_topic_contents_tbltraining_notes_note_id",
                schema: "erpsystem",
                table: "tbltraining_topic_contents",
                column: "note_id",
                principalSchema: "erpsystem",
                principalTable: "tbltraining_notes",
                principalColumn: "note_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tbltraining_topic_contents_tbltraining_notes_note_id",
                schema: "erpsystem",
                table: "tbltraining_topic_contents");

            migrationBuilder.DropIndex(
                name: "IX_tbltraining_topic_contents_note_id",
                schema: "erpsystem",
                table: "tbltraining_topic_contents");

            migrationBuilder.DropColumn(
                name: "note_id",
                schema: "erpsystem",
                table: "tbltraining_topic_contents");
        }
    }
}
