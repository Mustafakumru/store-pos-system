using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGiftCardSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GiftCardSoldCard",
                table: "ZReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GiftCardSoldCash",
                table: "ZReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GiftCardSoldTotal",
                table: "ZReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "GiftCardSales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    CashierId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftCardSales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GiftCardSales_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GiftCardSaleLine",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GiftCardSaleId = table.Column<int>(type: "int", nullable: false),
                    GiftCardId = table.Column<int>(type: "int", nullable: false),
                    CardNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftCardSaleLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GiftCardSaleLine_GiftCardSales_GiftCardSaleId",
                        column: x => x.GiftCardSaleId,
                        principalTable: "GiftCardSales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GiftCardSaleLine_GiftCards_GiftCardId",
                        column: x => x.GiftCardId,
                        principalTable: "GiftCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GiftCardSalePayment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GiftCardSaleId = table.Column<int>(type: "int", nullable: false),
                    PaymentType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InstallmentCount = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftCardSalePayment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GiftCardSalePayment_GiftCardSales_GiftCardSaleId",
                        column: x => x.GiftCardSaleId,
                        principalTable: "GiftCardSales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GiftCardSaleLine_GiftCardId",
                table: "GiftCardSaleLine",
                column: "GiftCardId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftCardSaleLine_GiftCardSaleId",
                table: "GiftCardSaleLine",
                column: "GiftCardSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftCardSalePayment_GiftCardSaleId",
                table: "GiftCardSalePayment",
                column: "GiftCardSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftCardSales_CashierId_CreatedAt",
                table: "GiftCardSales",
                columns: new[] { "CashierId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GiftCardSales_StoreId_CreatedAt",
                table: "GiftCardSales",
                columns: new[] { "StoreId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GiftCardSaleLine");

            migrationBuilder.DropTable(
                name: "GiftCardSalePayment");

            migrationBuilder.DropTable(
                name: "GiftCardSales");

            migrationBuilder.DropColumn(
                name: "GiftCardSoldCard",
                table: "ZReports");

            migrationBuilder.DropColumn(
                name: "GiftCardSoldCash",
                table: "ZReports");

            migrationBuilder.DropColumn(
                name: "GiftCardSoldTotal",
                table: "ZReports");
        }
    }
}
