using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NagmClinic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPharmacyManagementModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PharmacyCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PharmacySales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacySales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PharmacySuppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacySuppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyUnits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyPurchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PharmacyPurchases_PharmacySuppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "PharmacySuppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GenericName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    DefaultSellingPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PharmacyItems_PharmacyCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "PharmacyCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PharmacyItems_PharmacyLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "PharmacyLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PharmacyItems_PharmacyUnits_UnitId",
                        column: x => x.UnitId,
                        principalTable: "PharmacyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BonusQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QuantityRemaining = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemBatches_PharmacyItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "PharmacyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemBatches_PharmacySuppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "PharmacySuppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyPurchaseLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BonusQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ItemBatchId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyPurchaseLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PharmacyPurchaseLines_ItemBatches_ItemBatchId",
                        column: x => x.ItemBatchId,
                        principalTable: "ItemBatches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PharmacyPurchaseLines_PharmacyItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "PharmacyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PharmacyPurchaseLines_PharmacyPurchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "PharmacyPurchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PharmacySaleLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    ItemBatchId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BatchNumberSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpiryDateSnapshot = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SlotCodeSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacySaleLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PharmacySaleLines_ItemBatches_ItemBatchId",
                        column: x => x.ItemBatchId,
                        principalTable: "ItemBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PharmacySaleLines_PharmacyItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "PharmacyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PharmacySaleLines_PharmacySales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "PharmacySales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_ItemId_BatchNumber",
                table: "ItemBatches",
                columns: new[] { "ItemId", "BatchNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_ItemId_ExpiryDate",
                table: "ItemBatches",
                columns: new[] { "ItemId", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_SupplierId",
                table: "ItemBatches",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyCategories_Name",
                table: "PharmacyCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyItems_Barcode",
                table: "PharmacyItems",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyItems_CategoryId",
                table: "PharmacyItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyItems_LocationId",
                table: "PharmacyItems",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyItems_UnitId",
                table: "PharmacyItems",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyLocations_Code",
                table: "PharmacyLocations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyPurchaseLines_ItemBatchId",
                table: "PharmacyPurchaseLines",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyPurchaseLines_ItemId",
                table: "PharmacyPurchaseLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyPurchaseLines_PurchaseId",
                table: "PharmacyPurchaseLines",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyPurchases_SupplierId",
                table: "PharmacyPurchases",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySaleLines_ItemBatchId",
                table: "PharmacySaleLines",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySaleLines_ItemId",
                table: "PharmacySaleLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySaleLines_SaleId",
                table: "PharmacySaleLines",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyUnits_Name",
                table: "PharmacyUnits",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PharmacyPurchaseLines");

            migrationBuilder.DropTable(
                name: "PharmacySaleLines");

            migrationBuilder.DropTable(
                name: "PharmacyPurchases");

            migrationBuilder.DropTable(
                name: "ItemBatches");

            migrationBuilder.DropTable(
                name: "PharmacySales");

            migrationBuilder.DropTable(
                name: "PharmacyItems");

            migrationBuilder.DropTable(
                name: "PharmacySuppliers");

            migrationBuilder.DropTable(
                name: "PharmacyCategories");

            migrationBuilder.DropTable(
                name: "PharmacyLocations");

            migrationBuilder.DropTable(
                name: "PharmacyUnits");
        }
    }
}
