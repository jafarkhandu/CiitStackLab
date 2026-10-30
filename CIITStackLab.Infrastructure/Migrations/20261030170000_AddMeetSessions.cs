using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations;

public partial class AddMeetSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tbl_meet_sessions",
            schema: "erpsystem",
            columns: table => new
            {
                meet_session_id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                meet_batch_id = table.Column<int>(type: "int", nullable: false),
                title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                room_name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                starts_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                duration_minutes = table.Column<int>(type: "int", nullable: false),
                status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                created_by_user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_meet_sessions", x => x.meet_session_id);
                table.ForeignKey(
                    name: "FK_tbl_meet_sessions_tbl_meet_batches_meet_batch_id",
                    column: x => x.meet_batch_id,
                    principalSchema: "erpsystem",
                    principalTable: "tbl_meet_batches",
                    principalColumn: "meet_batch_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_tbl_meet_sessions_AspNetUsers_created_by_user_id",
                    column: x => x.created_by_user_id,
                    principalSchema: "erpsystem",
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.NoAction);
            });

        migrationBuilder.CreateIndex(
            name: "UX_meet_sessions_room_name",
            schema: "erpsystem",
            table: "tbl_meet_sessions",
            column: "room_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_meet_sessions_batch_start",
            schema: "erpsystem",
            table: "tbl_meet_sessions",
            columns: new[] { "meet_batch_id", "starts_at_utc" });
        
        migrationBuilder.CreateIndex(
            name: "IX_meet_sessions_created_by",
            schema: "erpsystem",
            table: "tbl_meet_sessions",
            column: "created_by_user_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "tbl_meet_sessions",
            schema: "erpsystem");
    }
}
