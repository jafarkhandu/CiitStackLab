using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncCourseImageModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tblpayment_transactions",
                schema: "erpsystem",
                columns: table => new
                {
                    payment_transaction_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    course_id = table.Column<int>(type: "int", nullable: false),
                    merchant_order_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    phonepe_order_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    completed_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblpayment_transactions", x => x.payment_transaction_id);
                    table.ForeignKey(
                        name: "FK_tblpayment_transactions_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalSchema: "erpsystem",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tblpayment_transactions_tbltraining_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "erpsystem",
                        principalTable: "tbltraining_courses",
                        principalColumn: "course_id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_tblpayment_transactions_course_id",
                schema: "erpsystem",
                table: "tblpayment_transactions",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "IX_tblpayment_transactions_user_course_status",
                schema: "erpsystem",
                table: "tblpayment_transactions",
                columns: new[] { "user_id", "course_id", "status" });

            migrationBuilder.CreateIndex(
                name: "UX_tblpayment_transactions_merchant_order_id",
                schema: "erpsystem",
                table: "tblpayment_transactions",
                column: "merchant_order_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tblpayment_transactions",
                schema: "erpsystem");
        }
    }
}
