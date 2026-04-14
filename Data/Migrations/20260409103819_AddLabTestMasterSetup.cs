using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NagmClinic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLabTestMasterSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "ClinicServices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CriticalRange",
                table: "ClinicServices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceCode",
                table: "ClinicServices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeviceMapped",
                table: "ClinicServices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LabAnalyzerId",
                table: "ClinicServices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LabCategoryId",
                table: "ClinicServices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrintName",
                table: "ClinicServices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceRange",
                table: "ClinicServices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SampleType",
                table: "ClinicServices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "ClinicServices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "ClinicServices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LabAnalyzers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WholeBloodSampleVolume = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PredilutedSampleVolume = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabAnalyzers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicServices_Code",
                table: "ClinicServices",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicServices_DeviceCode",
                table: "ClinicServices",
                column: "DeviceCode",
                filter: "[DeviceCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicServices_LabAnalyzerId",
                table: "ClinicServices",
                column: "LabAnalyzerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicServices_LabCategoryId",
                table: "ClinicServices",
                column: "LabCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LabAnalyzers_Code",
                table: "LabAnalyzers",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LabCategories_NameAr",
                table: "LabCategories",
                column: "NameAr",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicServices_LabAnalyzers_LabAnalyzerId",
                table: "ClinicServices",
                column: "LabAnalyzerId",
                principalTable: "LabAnalyzers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicServices_LabCategories_LabCategoryId",
                table: "ClinicServices",
                column: "LabCategoryId",
                principalTable: "LabCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicServices_LabAnalyzers_LabAnalyzerId",
                table: "ClinicServices");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicServices_LabCategories_LabCategoryId",
                table: "ClinicServices");

            migrationBuilder.DropTable(
                name: "LabAnalyzers");

            migrationBuilder.DropTable(
                name: "LabCategories");

            migrationBuilder.DropIndex(
                name: "IX_ClinicServices_Code",
                table: "ClinicServices");

            migrationBuilder.DropIndex(
                name: "IX_ClinicServices_DeviceCode",
                table: "ClinicServices");

            migrationBuilder.DropIndex(
                name: "IX_ClinicServices_LabAnalyzerId",
                table: "ClinicServices");

            migrationBuilder.DropIndex(
                name: "IX_ClinicServices_LabCategoryId",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "CriticalRange",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "DeviceCode",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "IsDeviceMapped",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "LabAnalyzerId",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "LabCategoryId",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "PrintName",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "ReferenceRange",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "SampleType",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "ClinicServices");
        }
    }
}
