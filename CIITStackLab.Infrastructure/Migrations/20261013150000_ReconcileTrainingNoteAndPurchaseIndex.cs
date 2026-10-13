using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileTrainingNoteAndPurchaseIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RestoredAt",
                schema: "erpsystem",
                table: "tbltraining_notes");

            migrationBuilder.CreateIndex(
                name: "IX_tblstudent_course_purchases_course_id",
                schema: "erpsystem",
                table: "tblstudent_course_purchases",
                column: "course_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tblstudent_course_purchases_course_id",
                schema: "erpsystem",
                table: "tblstudent_course_purchases");

            migrationBuilder.AddColumn<DateTime>(
                name: "RestoredAt",
                schema: "erpsystem",
                table: "tbltraining_notes",
                type: "datetime2",
                nullable: true);
        }
    }
}
