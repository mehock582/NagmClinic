using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NagmClinic.Data.Migrations
{
    /// <inheritdoc />
    public partial class addBarcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "PharmacyPurchaseLines",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "ItemBatches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE ib
                SET ib.Barcode = LEFT(CONCAT(pi.Barcode, '-', ib.Id), 100)
                FROM ItemBatches AS ib
                INNER JOIN PharmacyItems AS pi ON pi.Id = ib.ItemId
                WHERE ib.Barcode IS NULL OR LTRIM(RTRIM(ib.Barcode)) = '';
                """
            );

            migrationBuilder.Sql(
                """
                UPDATE ib
                SET ib.Barcode = CONCAT('BATCH-', ib.Id)
                FROM ItemBatches AS ib
                WHERE ib.Barcode IS NULL OR LTRIM(RTRIM(ib.Barcode)) = '';
                """
            );

            migrationBuilder.Sql(
                """
                UPDATE pl
                SET pl.Barcode = ib.Barcode
                FROM PharmacyPurchaseLines AS pl
                INNER JOIN ItemBatches AS ib ON ib.Id = pl.ItemBatchId
                WHERE pl.Barcode IS NULL OR LTRIM(RTRIM(pl.Barcode)) = '';
                """
            );

            migrationBuilder.Sql(
                """
                UPDATE pl
                SET pl.Barcode = LEFT(CONCAT(pi.Barcode, '-PL-', pl.Id), 100)
                FROM PharmacyPurchaseLines AS pl
                INNER JOIN PharmacyItems AS pi ON pi.Id = pl.ItemId
                WHERE pl.Barcode IS NULL OR LTRIM(RTRIM(pl.Barcode)) = '';
                """
            );

            migrationBuilder.Sql(
                """
                UPDATE pl
                SET pl.Barcode = CONCAT('PL-', pl.Id)
                FROM PharmacyPurchaseLines AS pl
                WHERE pl.Barcode IS NULL OR LTRIM(RTRIM(pl.Barcode)) = '';
                """
            );

            migrationBuilder.AlterColumn<string>(
                name: "Barcode",
                table: "PharmacyPurchaseLines",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Barcode",
                table: "ItemBatches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_Barcode",
                table: "ItemBatches",
                column: "Barcode",
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_PharmacyItems_Barcode",
                table: "PharmacyItems");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "PharmacyItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ItemBatches_Barcode",
                table: "ItemBatches");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "PharmacyPurchaseLines");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "ItemBatches");

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "PharmacyItems",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyItems_Barcode",
                table: "PharmacyItems",
                column: "Barcode",
                unique: true);
        }
    }
}
