using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261022130000_AddExamRetakePaymentMetadata")]
public sealed class AddExamRetakePaymentMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[erpsystem].[tblpayment_transactions]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'erpsystem.tblpayment_transactions', N'payment_purpose') IS NULL
                    ALTER TABLE [erpsystem].[tblpayment_transactions]
                        ADD [payment_purpose] NVARCHAR(32) NOT NULL
                            CONSTRAINT [DF_tblpayment_transactions_payment_purpose] DEFAULT ('Course');

                IF COL_LENGTH(N'erpsystem.tblpayment_transactions', N'exam_topic_id') IS NULL
                    ALTER TABLE [erpsystem].[tblpayment_transactions]
                        ADD [exam_topic_id] INT NULL;

                IF COL_LENGTH(N'erpsystem.tblpayment_transactions', N'exam_attempt_number') IS NULL
                    ALTER TABLE [erpsystem].[tblpayment_transactions]
                        ADD [exam_attempt_number] INT NULL;

                IF COL_LENGTH(N'erpsystem.tblpayment_transactions', N'exam_attempt_id') IS NULL
                    ALTER TABLE [erpsystem].[tblpayment_transactions]
                        ADD [exam_attempt_id] BIGINT NULL;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve payment and retake history; rollback intentionally leaves these
        // nullable metadata columns in place rather than risking data loss.
    }
}
