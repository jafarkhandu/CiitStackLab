using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261021090000_AddExamApplicationNotifications")]
    public partial class AddExamApplicationNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_exam_application_notifications",
                schema: "erpsystem",
                columns: table => new
                {
                    notification_id = table.Column<long>(type: "bigint")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    dedupe_key = table.Column<string>(
                        type: "char(64)",
                        fixedLength: true,
                        maxLength: 64,
                        nullable: false),
                    admin_user_id = table.Column<string>(
                        type: "nvarchar(450)",
                        maxLength: 450,
                        nullable: false),
                    student_user_id = table.Column<string>(
                        type: "nvarchar(450)",
                        maxLength: 450,
                        nullable: false),
                    student_name = table.Column<string>(
                        type: "nvarchar(256)",
                        maxLength: 256,
                        nullable: false),
                    course_id = table.Column<int>(type: "int", nullable: false),
                    course_title = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false),
                    topic_id = table.Column<int>(type: "int", nullable: false),
                    topic_title = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false),
                    valid_question_count = table.Column<int>(type: "int", nullable: false),
                    required_question_count = table.Column<int>(
                        type: "int",
                        nullable: false,
                        defaultValue: 50),
                    reason = table.Column<string>(
                        type: "nvarchar(1000)",
                        maxLength: 1000,
                        nullable: false),
                    notification_day = table.Column<DateTime>(
                        type: "date",
                        nullable: false),
                    created_at_utc = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: false),
                    is_delivered = table.Column<bool>(
                        type: "bit",
                        nullable: false,
                        defaultValue: false),
                    is_read = table.Column<bool>(
                        type: "bit",
                        nullable: false,
                        defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_tbl_exam_application_notifications",
                        x => x.notification_id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_exam_app_notification_dedupe_key",
                schema: "erpsystem",
                table: "tbl_exam_application_notifications",
                column: "dedupe_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_app_notification_admin_read_created",
                schema: "erpsystem",
                table: "tbl_exam_application_notifications",
                columns: new[] { "admin_user_id", "is_read", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_exam_app_notification_admin_delivered",
                schema: "erpsystem",
                table: "tbl_exam_application_notifications",
                columns: new[] { "admin_user_id", "is_delivered" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_exam_application_notifications",
                schema: "erpsystem");
        }
    }
}
