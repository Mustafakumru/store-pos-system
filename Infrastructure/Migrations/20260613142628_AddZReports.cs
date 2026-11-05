using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddZReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    ReportNumber = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalSalesAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalRefundsAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CashTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CardTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GiftCardTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    RefundCount = table.Column<int>(type: "int", nullable: false),
                    CashierId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZReports_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZReports_StoreId_ReportNumber",
                table: "ZReports",
                columns: new[] { "StoreId", "ReportNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZReports");
        }
    }
}
