using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOfflineSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_StoreId",
                table: "Sales");

            migrationBuilder.AddColumn<bool>(
                name: "IsOffline",
                table: "Sales",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OfflineReference",
                table: "Sales",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfflineSoldAt",
                table: "Sales",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_StoreId_OfflineReference",
                table: "Sales",
                columns: new[] { "StoreId", "OfflineReference" },
                unique: true,
                filter: "[OfflineReference] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_StoreId_OfflineReference",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "IsOffline",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "OfflineReference",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "OfflineSoldAt",
                table: "Sales");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_StoreId",
                table: "Sales",
                column: "StoreId");
        }
    }
}
