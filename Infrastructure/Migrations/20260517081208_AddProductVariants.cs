using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PriceHistories_Products_ProductId",
                table: "PriceHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_Products_ProductId",
                table: "SaleItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransferItems_Products_ProductId",
                table: "StockTransferItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StoreInventories_Products_ProductId",
                table: "StoreInventories");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseStocks_Products_ProductId",
                table: "WarehouseStocks");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CreatedAt",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_ReceiptNumber",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Products_Barcode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "WarehouseStocks",
                newName: "VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_WarehouseStocks_ProductId",
                table: "WarehouseStocks",
                newName: "IX_WarehouseStocks_VariantId");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "StoreInventories",
                newName: "VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_StoreInventories_StoreId_ProductId",
                table: "StoreInventories",
                newName: "IX_StoreInventories_StoreId_VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_StoreInventories_ProductId",
                table: "StoreInventories",
                newName: "IX_StoreInventories_VariantId");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "StockTransferItems",
                newName: "VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_StockTransferItems_ProductId",
                table: "StockTransferItems",
                newName: "IX_StockTransferItems_VariantId");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "SaleItems",
                newName: "VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_SaleItems_ProductId",
                table: "SaleItems",
                newName: "IX_SaleItems_VariantId");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "PriceHistories",
                newName: "VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_PriceHistory_ProductId_EndDate",
                table: "PriceHistories",
                newName: "IX_PriceHistories_VariantId_EndDate");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Sales",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "Completed");

            migrationBuilder.AlterColumn<string>(
                name: "ReceiptNumber",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<int>(
                name: "PaymentType",
                table: "Sales",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<bool>(
                name: "HasNegativeStockItem",
                table: "Sales",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountAmount",
                table: "Sales",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "CashierId",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AddColumn<string>(
                name: "ColorSnapshot",
                table: "SaleItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SizeSnapshot",
                table: "SaleItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoryId1",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Products",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductVariants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Size = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    WarehouseStockId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductVariants_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductVariants_WarehouseStocks_WarehouseStockId",
                        column: x => x.WarehouseStockId,
                        principalTable: "WarehouseStocks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId1",
                table: "Products",
                column: "CategoryId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_Barcode",
                table: "ProductVariants",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId",
                table: "ProductVariants",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_WarehouseStockId",
                table: "ProductVariants",
                column: "WarehouseStockId");

            migrationBuilder.AddForeignKey(
                name: "FK_PriceHistories_ProductVariants_VariantId",
                table: "PriceHistories",
                column: "VariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId1",
                table: "Products",
                column: "CategoryId1",
                principalTable: "Categories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_ProductVariants_VariantId",
                table: "SaleItems",
                column: "VariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransferItems_ProductVariants_VariantId",
                table: "StockTransferItems",
                column: "VariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StoreInventories_ProductVariants_VariantId",
                table: "StoreInventories",
                column: "VariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseStocks_ProductVariants_VariantId",
                table: "WarehouseStocks",
                column: "VariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PriceHistories_ProductVariants_VariantId",
                table: "PriceHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId1",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_ProductVariants_VariantId",
                table: "SaleItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransferItems_ProductVariants_VariantId",
                table: "StockTransferItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StoreInventories_ProductVariants_VariantId",
                table: "StoreInventories");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseStocks_ProductVariants_VariantId",
                table: "WarehouseStocks");

            migrationBuilder.DropTable(
                name: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_Products_CategoryId1",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ColorSnapshot",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "SizeSnapshot",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "CategoryId1",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "VariantId",
                table: "WarehouseStocks",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_WarehouseStocks_VariantId",
                table: "WarehouseStocks",
                newName: "IX_WarehouseStocks_ProductId");

            migrationBuilder.RenameColumn(
                name: "VariantId",
                table: "StoreInventories",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_StoreInventories_VariantId",
                table: "StoreInventories",
                newName: "IX_StoreInventories_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_StoreInventories_StoreId_VariantId",
                table: "StoreInventories",
                newName: "IX_StoreInventories_StoreId_ProductId");

            migrationBuilder.RenameColumn(
                name: "VariantId",
                table: "StockTransferItems",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_StockTransferItems_VariantId",
                table: "StockTransferItems",
                newName: "IX_StockTransferItems_ProductId");

            migrationBuilder.RenameColumn(
                name: "VariantId",
                table: "SaleItems",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_SaleItems_VariantId",
                table: "SaleItems",
                newName: "IX_SaleItems_ProductId");

            migrationBuilder.RenameColumn(
                name: "VariantId",
                table: "PriceHistories",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_PriceHistories_VariantId_EndDate",
                table: "PriceHistories",
                newName: "IX_PriceHistory_ProductId_EndDate");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Completed",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ReceiptNumber",
                table: "Sales",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentType",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<bool>(
                name: "HasNegativeStockItem",
                table: "Sales",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountAmount",
                table: "Sales",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "CashierId",
                table: "Sales",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "Products",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CreatedAt",
                table: "Sales",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_ReceiptNumber",
                table: "Sales",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceHistories_Products_ProductId",
                table: "PriceHistories",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_Products_ProductId",
                table: "SaleItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransferItems_Products_ProductId",
                table: "StockTransferItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StoreInventories_Products_ProductId",
                table: "StoreInventories",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseStocks_Products_ProductId",
                table: "WarehouseStocks",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
