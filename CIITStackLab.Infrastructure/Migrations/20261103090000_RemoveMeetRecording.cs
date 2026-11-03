using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261103090000_RemoveMeetRecording")]
public partial class RemoveMeetRecording : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "recording_enabled", schema: "erpsystem", table: "tbl_meet_sessions");
        migrationBuilder.DropColumn(name: "recording_status", schema: "erpsystem", table: "tbl_meet_sessions");
        migrationBuilder.DropColumn(name: "recording_started_at_utc", schema: "erpsystem", table: "tbl_meet_sessions");
        migrationBuilder.DropColumn(name: "recording_ended_at_utc", schema: "erpsystem", table: "tbl_meet_sessions");
        migrationBuilder.DropColumn(name: "recording_retention_until_utc", schema: "erpsystem", table: "tbl_meet_sessions");
        migrationBuilder.DropColumn(name: "recording_deleted_at_utc", schema: "erpsystem", table: "tbl_meet_sessions");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "recording_enabled", schema: "erpsystem", table: "tbl_meet_sessions", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "recording_status", schema: "erpsystem", table: "tbl_meet_sessions", type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Disabled");
        migrationBuilder.AddColumn<DateTime>(name: "recording_started_at_utc", schema: "erpsystem", table: "tbl_meet_sessions", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "recording_ended_at_utc", schema: "erpsystem", table: "tbl_meet_sessions", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "recording_retention_until_utc", schema: "erpsystem", table: "tbl_meet_sessions", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "recording_deleted_at_utc", schema: "erpsystem", table: "tbl_meet_sessions", type: "datetime2", nullable: true);
    }
}
