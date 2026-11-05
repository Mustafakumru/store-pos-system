using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StoreInventories",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SaleItems",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "GiftCards",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Coupons",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_CreatedAt",
                table: "IdempotencyRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_UserId_Key",
                table: "IdempotencyRecords",
                columns: new[] { "UserId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StoreInventories");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "GiftCards");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Coupons");
        }
    }
}
