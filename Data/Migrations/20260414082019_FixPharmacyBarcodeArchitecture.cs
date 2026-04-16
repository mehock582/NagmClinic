using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NagmClinic.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPharmacyBarcodeArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ItemBatches_Barcode",
                table: "ItemBatches");

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "PharmacyItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyItems_Barcode",
                table: "PharmacyItems",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_Barcode",
                table: "ItemBatches",
                column: "Barcode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PharmacyItems_Barcode",
                table: "PharmacyItems");

            migrationBuilder.DropIndex(
                name: "IX_ItemBatches_Barcode",
                table: "ItemBatches");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "PharmacyItems");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_Barcode",
                table: "ItemBatches",
                column: "Barcode",
                unique: true);
        }
    }
}
