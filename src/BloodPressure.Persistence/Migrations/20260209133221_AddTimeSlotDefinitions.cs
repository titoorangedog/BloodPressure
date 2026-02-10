using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodPressure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeSlotDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeSlotDefinitions",
                table: "UserSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.UpdateData(
                table: "TimeSlotOptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "Mattino");

            migrationBuilder.UpdateData(
                table: "TimeSlotOptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "Pomeriggio");

            migrationBuilder.UpdateData(
                table: "TimeSlotOptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "Sera");

            migrationBuilder.UpdateData(
                table: "TimeSlotOptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "Notte");

            migrationBuilder.InsertData(
                table: "TimeSlotOptions",
                columns: new[] { "Id", "Name" },
                values: new object[] { 5, "Mezzo giorno" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TimeSlotOptions",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DropColumn(
                name: "TimeSlotDefinitions",
                table: "UserSettings");

            migrationBuilder.UpdateData(
                table: "TimeSlotOptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "Morning");

            migrationBuilder.UpdateData(
                table: "TimeSlotOptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "Afternoon");

            migrationBuilder.UpdateData(
                table: "TimeSlotOptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "Evening");

            migrationBuilder.UpdateData(
                table: "TimeSlotOptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "Night");
        }
    }
}
