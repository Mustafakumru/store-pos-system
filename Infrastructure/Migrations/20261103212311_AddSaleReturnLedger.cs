using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleReturnLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CardRefundTotal",
                table: "ZReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CashRefundTotal",
                table: "ZReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "SaleReturns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    CashierId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ExchangeSaleId = table.Column<int>(type: "int", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleReturns_Sales_ExchangeSaleId",
                        column: x => x.ExchangeSaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturns_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleReturnLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleReturnId = table.Column<int>(type: "int", nullable: false),
                    SaleItemId = table.Column<int>(type: "int", nullable: true),
                    OriginalSaleId = table.Column<int>(type: "int", nullable: true),
                    OriginalReceiptNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VariantId = table.Column<int>(type: "int", nullable: false),
                    BarcodeSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReturnReasonId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReturnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleReturnLines_ReturnReasons_ReturnReasonId",
                        column: x => x.ReturnReasonId,
                        principalTable: "ReturnReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SaleReturnLines_SaleItems_SaleItemId",
                        column: x => x.SaleItemId,
                        principalTable: "SaleItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturnLines_SaleReturns_SaleReturnId",
                        column: x => x.SaleReturnId,
                        principalTable: "SaleReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SaleReturnPayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleReturnId = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GiftCardNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReturnPayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleReturnPayouts_SaleReturns_SaleReturnId",
                        column: x => x.SaleReturnId,
                        principalTable: "SaleReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_OriginalSaleId",
                table: "SaleReturnLines",
                column: "OriginalSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_ReturnReasonId",
                table: "SaleReturnLines",
                column: "ReturnReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_SaleItemId",
                table: "SaleReturnLines",
                column: "SaleItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_SaleReturnId",
                table: "SaleReturnLines",
                column: "SaleReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnPayouts_SaleReturnId",
                table: "SaleReturnPayouts",
                column: "SaleReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_CashierId_CreatedAt",
                table: "SaleReturns",
                columns: new[] { "CashierId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_ExchangeSaleId",
                table: "SaleReturns",
                column: "ExchangeSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_StoreId_CreatedAt",
                table: "SaleReturns",
                columns: new[] { "StoreId", "CreatedAt" });

            // ── Eski iadeleri deftere aktar ──────────────────────────────
            // Eskiden iade sadece SaleItem.RefundedQuantity/RefundedAmount'a
            // kümülatif yazılıyordu. Raporlar artık defterden okuduğu için
            // geçmiş iadeler kaybolmasın: iade edilmiş her kalem için bir kayıt.
            // Bilinmeyenler için en iyi tahmin: mağaza = satışın mağazası,
            // an = kalemin son güncellenme anı, kasiyer = son güncelleyen,
            // ödeme = nakit. (Değişimde kredi olarak kullanılan eski iadeler
            // ayırt edilemez - geçmiş değişim günlerinin raporu eski hâliyle kalır.)
            migrationBuilder.Sql(@"
INSERT INTO SaleReturns (StoreId, CashierId, ExchangeSaleId, TotalAmount, Note, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted)
SELECT s.StoreId,
       COALESCE(NULLIF(si.UpdatedBy, ''), s.CashierId),
       NULL,
       si.RefundedAmount,
       CONCAT('LEGACY:', si.Id),
       COALESCE(si.UpdatedAt, s.CreatedAt),
       si.UpdatedBy, NULL, NULL, 0
FROM SaleItems si
JOIN Sales s ON s.Id = si.SaleId
WHERE si.RefundedAmount > 0 AND si.RefundedQuantity > 0 AND si.IsDeleted = 0;

INSERT INTO SaleReturnLines (SaleReturnId, SaleItemId, OriginalSaleId, OriginalReceiptNumber, VariantId,
                             BarcodeSnapshot, ProductNameSnapshot, Quantity, Amount, ReturnReasonId,
                             CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted)
SELECT r.Id, si.Id, s.Id, s.ReceiptNumber, si.VariantId,
       si.BarcodeSnapshot, si.ProductNameSnapshot, si.RefundedQuantity, si.RefundedAmount, si.ReturnReasonId,
       r.CreatedAt, r.CreatedBy, NULL, NULL, 0
FROM SaleReturns r
JOIN SaleItems si ON r.Note = CONCAT('LEGACY:', si.Id)
JOIN Sales s ON s.Id = si.SaleId;

INSERT INTO SaleReturnPayouts (SaleReturnId, Method, Amount, GiftCardNumber, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted)
SELECT r.Id, 0, r.TotalAmount, NULL, r.CreatedAt, r.CreatedBy, NULL, NULL, 0
FROM SaleReturns r
WHERE r.Note LIKE 'LEGACY:%';

UPDATE SaleReturns SET Note = N'Eski kayıttan aktarıldı' WHERE Note LIKE 'LEGACY:%';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleReturnLines");

            migrationBuilder.DropTable(
                name: "SaleReturnPayouts");

            migrationBuilder.DropTable(
                name: "SaleReturns");

            migrationBuilder.DropColumn(
                name: "CardRefundTotal",
                table: "ZReports");

            migrationBuilder.DropColumn(
                name: "CashRefundTotal",
                table: "ZReports");
        }
    }
}
