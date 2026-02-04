using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BloodPressure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SportActivityOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SportActivityOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SymptomOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SymptomOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeSlotOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeSlotOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Licenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    StartDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Licenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Licenses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Readings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Systolic = table.Column<int>(type: "int", nullable: false),
                    Diastolic = table.Column<int>(type: "int", nullable: false),
                    HeartRate = table.Column<int>(type: "int", nullable: true),
                    WeightKg = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Position = table.Column<int>(type: "int", nullable: false),
                    MedicationSkipped = table.Column<bool>(type: "bit", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    ColorKey = table.Column<int>(type: "int", nullable: false),
                    TimeSlotOptionId = table.Column<int>(type: "int", nullable: true),
                    SportActivityOptionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Readings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Readings_SportActivityOptions_SportActivityOptionId",
                        column: x => x.SportActivityOptionId,
                        principalTable: "SportActivityOptions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Readings_TimeSlotOptions_TimeSlotOptionId",
                        column: x => x.TimeSlotOptionId,
                        principalTable: "TimeSlotOptions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Readings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HeightCm = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SystolicVeryLowMax = table.Column<int>(type: "int", nullable: false),
                    SystolicLowMax = table.Column<int>(type: "int", nullable: false),
                    SystolicNormalMax = table.Column<int>(type: "int", nullable: false),
                    SystolicHighMax = table.Column<int>(type: "int", nullable: false),
                    DiastolicVeryLowMax = table.Column<int>(type: "int", nullable: false),
                    DiastolicLowMax = table.Column<int>(type: "int", nullable: false),
                    DiastolicNormalMax = table.Column<int>(type: "int", nullable: false),
                    DiastolicHighMax = table.Column<int>(type: "int", nullable: false),
                    DashboardPreferences_DefaultRangeDays = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    DefaultSymptomOptionIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultSelections_TimeSlotOptionId = table.Column<int>(type: "int", nullable: true),
                    DefaultSelections_SportActivityOptionId = table.Column<int>(type: "int", nullable: true),
                    UiPreferences_CompactMode = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadingSymptoms",
                columns: table => new
                {
                    ReadingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SymptomOptionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingSymptoms", x => new { x.ReadingId, x.SymptomOptionId });
                    table.ForeignKey(
                        name: "FK_ReadingSymptoms_Readings_ReadingId",
                        column: x => x.ReadingId,
                        principalTable: "Readings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingSymptoms_SymptomOptions_SymptomOptionId",
                        column: x => x.SymptomOptionId,
                        principalTable: "SymptomOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "SportActivityOptions",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "None" },
                    { 2, "Light" },
                    { 3, "Moderate" },
                    { 4, "Intense" }
                });

            migrationBuilder.InsertData(
                table: "SymptomOptions",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Headache" },
                    { 2, "Dizziness" },
                    { 3, "Nausea" },
                    { 4, "Chest Pain" },
                    { 5, "Blurred Vision" }
                });

            migrationBuilder.InsertData(
                table: "TimeSlotOptions",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Morning" },
                    { 2, "Afternoon" },
                    { 3, "Evening" },
                    { 4, "Night" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_UserId_IsActive",
                table: "Licenses",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Readings_SportActivityOptionId",
                table: "Readings",
                column: "SportActivityOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Readings_TimeSlotOptionId",
                table: "Readings",
                column: "TimeSlotOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Readings_UserId",
                table: "Readings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingSymptoms_SymptomOptionId",
                table: "ReadingSymptoms",
                column: "SymptomOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Licenses");

            migrationBuilder.DropTable(
                name: "ReadingSymptoms");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "Readings");

            migrationBuilder.DropTable(
                name: "SymptomOptions");

            migrationBuilder.DropTable(
                name: "SportActivityOptions");

            migrationBuilder.DropTable(
                name: "TimeSlotOptions");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
