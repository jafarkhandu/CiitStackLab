using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTopicPricingAndDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "price",
                schema: "erpsystem",
                table: "tbltraining_topics",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "duration_minutes",
                schema: "erpsystem",
                table: "tbltraining_topics",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "duration_minutes",
                schema: "erpsystem",
                table: "tbltraining_topics");

            migrationBuilder.DropColumn(
                name: "price",
                schema: "erpsystem",
                table: "tbltraining_topics");
        }
    }
}
