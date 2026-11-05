using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashDrawerSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashDrawerSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    CashierId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OpeningFloat = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpeningApprovedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClosingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosingApprovedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashDrawerSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashDrawerSessions_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerSessions_StoreId_CashierId_Status",
                table: "CashDrawerSessions",
                columns: new[] { "StoreId", "CashierId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashDrawerSessions");
        }
    }
}
