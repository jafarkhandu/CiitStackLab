using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations;

public partial class AddMeetLifecycleAndParticipants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "started_at_utc",
            schema: "erpsystem",
            table: "tbl_meet_sessions",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ended_at_utc",
            schema: "erpsystem",
            table: "tbl_meet_sessions",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "cancelled_at_utc",
            schema: "erpsystem",
            table: "tbl_meet_sessions",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "waiting_room_enabled",
            schema: "erpsystem",
            table: "tbl_meet_sessions",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "recording_enabled",
            schema: "erpsystem",
            table: "tbl_meet_sessions",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "recording_status",
            schema: "erpsystem",
            table: "tbl_meet_sessions",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Disabled");

        migrationBuilder.AddColumn<DateTime>(
            name: "recording_started_at_utc",
            schema: "erpsystem",
            table: "tbl_meet_sessions",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "recording_ended_at_utc",
            schema: "erpsystem",
            table: "tbl_meet_sessions",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "recording_retention_until_utc",
            schema: "erpsystem",
            table: "tbl_meet_sessions",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "recording_deleted_at_utc",
            schema: "erpsystem",
            table: "tbl_meet_sessions",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "tbl_meet_session_participants",
            schema: "erpsystem",
            columns: table => new
            {
                meet_session_participant_id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                meet_session_id = table.Column<int>(type: "int", nullable: false),
                student_user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                requested_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                admitted_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                left_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                can_use_microphone = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                can_use_camera = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                can_share_screen = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_meet_session_participants", x => x.meet_session_participant_id);
                table.ForeignKey(
                    name: "FK_tbl_meet_session_participants_tbl_meet_sessions_meet_session_id",
                    column: x => x.meet_session_id,
                    principalSchema: "erpsystem",
                    principalTable: "tbl_meet_sessions",
                    principalColumn: "meet_session_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_tbl_meet_session_participants_AspNetUsers_student_user_id",
                    column: x => x.student_user_id,
                    principalSchema: "erpsystem",
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.NoAction);
            });

        migrationBuilder.CreateIndex(
            name: "UX_meet_session_participants_session_student",
            schema: "erpsystem",
            table: "tbl_meet_session_participants",
            columns: new[] { "meet_session_id", "student_user_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_meet_session_participants_session_status",
            schema: "erpsystem",
            table: "tbl_meet_session_participants",
            columns: new[] { "meet_session_id", "status" });

        migrationBuilder.CreateIndex(
            name: "IX_meet_session_participants_student_user_id",
            schema: "erpsystem",
            table: "tbl_meet_session_participants",
            column: "student_user_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "tbl_meet_session_participants",
            schema: "erpsystem");

        migrationBuilder.DropColumn(name: "started_at_utc", schema: "erpsystem", table: "tbl_meet_sessions");
        migrationBuilder.DropColumn(name: "ended_at_utc", schema: "erpsystem", table: "tbl_meet_sessions");
        migrationBuilder.DropColumn(name: "cancelled_at_utc", schema: "erpsystem", table: "tbl_meet_sessions");
        migrationBuilder.DropColumn(name: "waiting_room_enabled", schema: "erpsystem", table: "tbl_meet_sessions");
        migrationBuilder.DropColumn(name: "recording_enabled", schema: "erpsystem", table: "tbl_meet_sessions");
        migrationBuilder.DropColumn(name: "recording_status", schema: "erpsystem", table: "tbl_meet_sessions");
        migrationBuilder.DropColumn(name: "recording_started_at_utc", schema: "erpsystem", table: "tbl_meet_sessions");
        migrationBuilder.DropColumn(name: "recording_ended_at_utc", schema: "erpsystem", table: "tbl_meet_sessions");
        migrationBuilder.DropColumn(name: "recording_retention_until_utc", schema: "erpsystem", table: "tbl_meet_sessions");
        migrationBuilder.DropColumn(name: "recording_deleted_at_utc", schema: "erpsystem", table: "tbl_meet_sessions");
    }
}
