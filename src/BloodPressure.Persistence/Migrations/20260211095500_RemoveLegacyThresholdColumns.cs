using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BloodPressure.Persistence.Migrations;

[DbContext(typeof(BloodPressureDbContext))]
[Migration("20260211095500_RemoveLegacyThresholdColumns")]
public partial class RemoveLegacyThresholdColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('UserSettings', 'SystolicNormalMax') IS NOT NULL
                ALTER TABLE [UserSettings] DROP COLUMN [SystolicNormalMax];
            IF COL_LENGTH('UserSettings', 'SystolicHighMax') IS NOT NULL
                ALTER TABLE [UserSettings] DROP COLUMN [SystolicHighMax];
            IF COL_LENGTH('UserSettings', 'DiastolicNormalMax') IS NOT NULL
                ALTER TABLE [UserSettings] DROP COLUMN [DiastolicNormalMax];
            IF COL_LENGTH('UserSettings', 'DiastolicHighMax') IS NOT NULL
                ALTER TABLE [UserSettings] DROP COLUMN [DiastolicHighMax];
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('UserSettings', 'SystolicNormalMax') IS NULL
                ALTER TABLE [UserSettings] ADD [SystolicNormalMax] int NOT NULL CONSTRAINT [DF_UserSettings_SystolicNormalMax] DEFAULT 120;
            IF COL_LENGTH('UserSettings', 'SystolicHighMax') IS NULL
                ALTER TABLE [UserSettings] ADD [SystolicHighMax] int NOT NULL CONSTRAINT [DF_UserSettings_SystolicHighMax] DEFAULT 139;
            IF COL_LENGTH('UserSettings', 'DiastolicNormalMax') IS NULL
                ALTER TABLE [UserSettings] ADD [DiastolicNormalMax] int NOT NULL CONSTRAINT [DF_UserSettings_DiastolicNormalMax] DEFAULT 80;
            IF COL_LENGTH('UserSettings', 'DiastolicHighMax') IS NULL
                ALTER TABLE [UserSettings] ADD [DiastolicHighMax] int NOT NULL CONSTRAINT [DF_UserSettings_DiastolicHighMax] DEFAULT 89;
            """);
    }
}
