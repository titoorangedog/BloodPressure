using Microsoft.EntityFrameworkCore.Migrations;

namespace BloodPressure.Persistence.Migrations;

public partial class UpdateClinicalThresholdBands : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "SystolicNormalLowMax",
            table: "UserSettings",
            type: "int",
            nullable: false,
            defaultValue: 109);

        migrationBuilder.AddColumn<int>(
            name: "SystolicNormalOptimalMax",
            table: "UserSettings",
            type: "int",
            nullable: false,
            defaultValue: 120);

        migrationBuilder.AddColumn<int>(
            name: "SystolicWarningHighMax",
            table: "UserSettings",
            type: "int",
            nullable: false,
            defaultValue: 139);

        migrationBuilder.AddColumn<int>(
            name: "SystolicVeryHighMin",
            table: "UserSettings",
            type: "int",
            nullable: false,
            defaultValue: 180);

        migrationBuilder.AddColumn<int>(
            name: "DiastolicNormalLowMax",
            table: "UserSettings",
            type: "int",
            nullable: false,
            defaultValue: 69);

        migrationBuilder.AddColumn<int>(
            name: "DiastolicNormalOptimalMax",
            table: "UserSettings",
            type: "int",
            nullable: false,
            defaultValue: 80);

        migrationBuilder.AddColumn<int>(
            name: "DiastolicWarningHighMax",
            table: "UserSettings",
            type: "int",
            nullable: false,
            defaultValue: 89);

        migrationBuilder.AddColumn<int>(
            name: "DiastolicVeryHighMin",
            table: "UserSettings",
            type: "int",
            nullable: false,
            defaultValue: 120);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SystolicNormalLowMax",
            table: "UserSettings");

        migrationBuilder.DropColumn(
            name: "SystolicNormalOptimalMax",
            table: "UserSettings");

        migrationBuilder.DropColumn(
            name: "SystolicWarningHighMax",
            table: "UserSettings");

        migrationBuilder.DropColumn(
            name: "SystolicVeryHighMin",
            table: "UserSettings");

        migrationBuilder.DropColumn(
            name: "DiastolicNormalLowMax",
            table: "UserSettings");

        migrationBuilder.DropColumn(
            name: "DiastolicNormalOptimalMax",
            table: "UserSettings");

        migrationBuilder.DropColumn(
            name: "DiastolicWarningHighMax",
            table: "UserSettings");

        migrationBuilder.DropColumn(
            name: "DiastolicVeryHighMin",
            table: "UserSettings");
    }
}
