using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NagmClinic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLabIntegrationImportPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConnectorSource",
                table: "LabResults",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImportedAt",
                table: "LabResults",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientIdentifier",
                table: "LabResults",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceDeviceId",
                table: "LabResults",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceTestCode",
                table: "LabResults",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceTimestamp",
                table: "LabResults",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LabDeviceTestMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeviceTestCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LabTestId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabDeviceTestMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabDeviceTestMappings_ClinicServices_LabTestId",
                        column: x => x.LabTestId,
                        principalTable: "ClinicServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabResultImportRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PatientIdentifier = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TestCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResultValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConnectorSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessingStatus = table.Column<int>(type: "int", nullable: false),
                    LabResultId = table.Column<int>(type: "int", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabResultImportRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabResultImportRecords_LabResults_LabResultId",
                        column: x => x.LabResultId,
                        principalTable: "LabResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabDeviceTestMappings_DeviceId_DeviceTestCode",
                table: "LabDeviceTestMappings",
                columns: new[] { "DeviceId", "DeviceTestCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabDeviceTestMappings_LabTestId",
                table: "LabDeviceTestMappings",
                column: "LabTestId");

            migrationBuilder.CreateIndex(
                name: "IX_LabResultImportRecords_DeviceId_TestCode_PatientIdentifier_Timestamp",
                table: "LabResultImportRecords",
                columns: new[] { "DeviceId", "TestCode", "PatientIdentifier", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabResultImportRecords_ImportedAt",
                table: "LabResultImportRecords",
                column: "ImportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LabResultImportRecords_LabResultId",
                table: "LabResultImportRecords",
                column: "LabResultId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabDeviceTestMappings");

            migrationBuilder.DropTable(
                name: "LabResultImportRecords");

            migrationBuilder.DropColumn(
                name: "ConnectorSource",
                table: "LabResults");

            migrationBuilder.DropColumn(
                name: "ImportedAt",
                table: "LabResults");

            migrationBuilder.DropColumn(
                name: "PatientIdentifier",
                table: "LabResults");

            migrationBuilder.DropColumn(
                name: "SourceDeviceId",
                table: "LabResults");

            migrationBuilder.DropColumn(
                name: "SourceTestCode",
                table: "LabResults");

            migrationBuilder.DropColumn(
                name: "SourceTimestamp",
                table: "LabResults");
        }
    }
}
