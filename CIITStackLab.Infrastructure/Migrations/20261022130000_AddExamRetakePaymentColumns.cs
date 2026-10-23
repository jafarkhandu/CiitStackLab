using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261022130000_AddExamRetakePaymentColumns")]
    public partial class AddExamRetakePaymentColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[erpsystem].[tblpayment_transactions]', N'U') IS NULL
                    THROW 51000, 'Required table erpsystem.tblpayment_transactions does not exist.', 1;

                IF COL_LENGTH(N'erpsystem.tblpayment_transactions', N'payment_purpose') IS NULL
                BEGIN
                    ALTER TABLE [erpsystem].[tblpayment_transactions]
                    ADD [payment_purpose] NVARCHAR(32) NOT NULL
                        CONSTRAINT [DF_tblpayment_transactions_payment_purpose] DEFAULT ('Course');
                END;

                IF COL_LENGTH(N'erpsystem.tblpayment_transactions', N'exam_topic_id') IS NULL
                BEGIN
                    ALTER TABLE [erpsystem].[tblpayment_transactions]
                    ADD [exam_topic_id] INT NULL;
                END;

                IF COL_LENGTH(N'erpsystem.tblpayment_transactions', N'exam_attempt_number') IS NULL
                BEGIN
                    ALTER TABLE [erpsystem].[tblpayment_transactions]
                    ADD [exam_attempt_number] INT NULL;
                END;

                IF COL_LENGTH(N'erpsystem.tblpayment_transactions', N'exam_attempt_id') IS NULL
                BEGIN
                    ALTER TABLE [erpsystem].[tblpayment_transactions]
                    ADD [exam_attempt_id] BIGINT NULL;
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep these columns on rollback: they may contain exam-retake payment data.
        }
    }
}
