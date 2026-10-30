using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DiagnoseMeetModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_meet_sessions_created_by",
                schema: "erpsystem",
                table: "tbl_meet_sessions",
                newName: "IX_tbl_meet_sessions_created_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_tbl_meet_sessions_created_by_user_id",
                schema: "erpsystem",
                table: "tbl_meet_sessions",
                newName: "IX_meet_sessions_created_by");
        }
    }
}
