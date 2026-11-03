using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261103180000_AddMeetChatEnabled")]
public partial class AddMeetChatEnabled : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "chat_enabled",
            schema: "erpsystem",
            table: "tbl_meet_sessions",
            type: "bit",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "chat_enabled",
            schema: "erpsystem",
            table: "tbl_meet_sessions");
    }
}
