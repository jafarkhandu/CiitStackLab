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
            // Some environments already have this table from an earlier deployment.
            // Create it only when absent; never replace or drop existing payment data.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[erpsystem].[tblpayment_transactions]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [erpsystem].[tblpayment_transactions]
                    (
                        [payment_transaction_id] INT IDENTITY(1,1) NOT NULL,
                        [user_id] NVARCHAR(450) NOT NULL,
                        [course_id] INT NOT NULL,
                        [merchant_order_id] NVARCHAR(100) NOT NULL,
                        [amount] DECIMAL(18,2) NOT NULL,
                        [status] NVARCHAR(32) NOT NULL,
                        [phonepe_order_id] NVARCHAR(100) NULL,
                        [created_at] DATETIME2 NOT NULL,
                        [updated_at] DATETIME2 NOT NULL,
                        [completed_at] DATETIME2 NULL,
                        CONSTRAINT [PK_tblpayment_transactions]
                            PRIMARY KEY ([payment_transaction_id]),
                        CONSTRAINT [FK_tblpayment_transactions_AspNetUsers_user_id]
                            FOREIGN KEY ([user_id])
                            REFERENCES [erpsystem].[AspNetUsers] ([Id]),
                        CONSTRAINT [FK_tblpayment_transactions_tbltraining_courses_course_id]
                            FOREIGN KEY ([course_id])
                            REFERENCES [erpsystem].[tbltraining_courses] ([course_id])
                    );
                END;

                IF OBJECT_ID(N'[erpsystem].[tblpayment_transactions]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'IX_tblpayment_transactions_course_id'
                          AND [object_id] = OBJECT_ID(N'[erpsystem].[tblpayment_transactions]'))
                        CREATE INDEX [IX_tblpayment_transactions_course_id]
                            ON [erpsystem].[tblpayment_transactions] ([course_id]);

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'IX_tblpayment_transactions_user_course_status'
                          AND [object_id] = OBJECT_ID(N'[erpsystem].[tblpayment_transactions]'))
                        CREATE INDEX [IX_tblpayment_transactions_user_course_status]
                            ON [erpsystem].[tblpayment_transactions] ([user_id], [course_id], [status]);

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'UX_tblpayment_transactions_merchant_order_id'
                          AND [object_id] = OBJECT_ID(N'[erpsystem].[tblpayment_transactions]'))
                        CREATE UNIQUE INDEX [UX_tblpayment_transactions_merchant_order_id]
                            ON [erpsystem].[tblpayment_transactions] ([merchant_order_id]);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally do not drop this table: it may pre-date this migration
            // and can contain payment records that must be preserved.
        }
    }
}