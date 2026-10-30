using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261030160000_AddMeetBatchManagement")]
public partial class AddMeetBatchManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tbl_meet_batches",
            schema: "erpsystem",
            columns: table => new
            {
                meet_batch_id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                batch_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                is_archived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                created_by_user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_meet_batches", x => x.meet_batch_id);
                table.ForeignKey(
                    name: "FK_tbl_meet_batches_AspNetUsers_created_by_user_id",
                    column: x => x.created_by_user_id,
                    principalSchema: "erpsystem",
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.NoAction);
            });

        migrationBuilder.CreateTable(
            name: "tbl_meet_batch_members",
            schema: "erpsystem",
            columns: table => new
            {
                meet_batch_member_id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                meet_batch_id = table.Column<int>(type: "int", nullable: false),
                student_user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                added_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_meet_batch_members", x => x.meet_batch_member_id);
                table.ForeignKey(
                    name: "FK_tbl_meet_batch_members_AspNetUsers_student_user_id",
                    column: x => x.student_user_id,
                    principalSchema: "erpsystem",
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.NoAction);
                table.ForeignKey(
                    name: "FK_tbl_meet_batch_members_tbl_meet_batches_meet_batch_id",
                    column: x => x.meet_batch_id,
                    principalSchema: "erpsystem",
                    principalTable: "tbl_meet_batches",
                    principalColumn: "meet_batch_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "UX_meet_batches_batch_code",
            schema: "erpsystem",
            table: "tbl_meet_batches",
            column: "batch_code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_meet_batches_created_by",
            schema: "erpsystem",
            table: "tbl_meet_batches",
            column: "created_by_user_id");

        migrationBuilder.CreateIndex(
            name: "UX_meet_batch_members_batch_student",
            schema: "erpsystem",
            table: "tbl_meet_batch_members",
            columns: new[] { "meet_batch_id", "student_user_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_meet_batch_members_student",
            schema: "erpsystem",
            table: "tbl_meet_batch_members",
            column: "student_user_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "tbl_meet_batch_members",
            schema: "erpsystem");

        migrationBuilder.DropTable(
            name: "tbl_meet_batches",
            schema: "erpsystem");
    }
}
