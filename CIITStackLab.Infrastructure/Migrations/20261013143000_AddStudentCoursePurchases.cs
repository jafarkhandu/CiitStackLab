using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CIITStackLab.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261013143000_AddStudentCoursePurchases")]
public sealed partial class AddStudentCoursePurchases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'erpsystem.tblstudent_course_purchases', N'U') IS NULL
            BEGIN
                CREATE TABLE [erpsystem].[tblstudent_course_purchases]
                (
                    [purchase_id] INT IDENTITY(1,1) NOT NULL
                        CONSTRAINT [PK_tblstudent_course_purchases] PRIMARY KEY,
                    [user_id] NVARCHAR(450) NOT NULL,
                    [course_id] INT NOT NULL,
                    [purchase_price] DECIMAL(18,2) NOT NULL,
                    [purchased_at] DATETIME2 NOT NULL,
                    CONSTRAINT [FK_tblstudent_course_purchases_AspNetUsers_user_id]
                        FOREIGN KEY ([user_id]) REFERENCES [erpsystem].[AspNetUsers] ([Id]),
                    CONSTRAINT [FK_tblstudent_course_purchases_tbltraining_courses_course_id]
                        FOREIGN KEY ([course_id]) REFERENCES [erpsystem].[tbltraining_courses] ([course_id])
                );
            END;

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE [name] = N'UX_tblstudent_course_purchases_user_course'
                  AND [object_id] = OBJECT_ID(N'erpsystem.tblstudent_course_purchases')
            )
            BEGIN
                CREATE UNIQUE INDEX [UX_tblstudent_course_purchases_user_course]
                    ON [erpsystem].[tblstudent_course_purchases] ([user_id], [course_id]);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'erpsystem.tblstudent_course_purchases', N'U') IS NOT NULL
            BEGIN
                DROP TABLE [erpsystem].[tblstudent_course_purchases];
            END;
            """);
    }
}
