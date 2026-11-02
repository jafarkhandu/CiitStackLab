using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261102120000_AddMeetReminderTracking")]
public partial class AddMeetReminderTracking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "reminder_sent_at_utc",
            schema: "erpsystem",
            table: "tbl_meet_sessions",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "reminder_sent_at_utc",
            schema: "erpsystem",
            table: "tbl_meet_sessions");
    }
}
