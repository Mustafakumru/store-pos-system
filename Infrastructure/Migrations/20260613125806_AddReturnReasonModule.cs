using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnReasonModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReturnReasonId",
                table: "SaleItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReturnReasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnReasons", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ReturnReasons",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "IsActive", "IsDeleted", "Name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Baskı Hatası", null, null },
                    { 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Beden", null, null },
                    { 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Delik", null, null },
                    { 4, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Diğer", null, null },
                    { 5, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Dikiş Açılması", null, null },
                    { 6, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Dikiş Bozukluğu", null, null },
                    { 7, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Dokuma/Örgü Hatası", null, null },
                    { 8, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Düğme", null, null },
                    { 9, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Düğme, Kopça, Fermuarlar", null, null },
                    { 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Esneme", null, null },
                    { 11, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Fermuar Hatası", null, null },
                    { 12, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Fiyat", null, null },
                    { 13, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Kumaş Boyama", null, null },
                    { 14, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Model", null, null },
                    { 15, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Müşteri Memnuniyeti", null, null },
                    { 16, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Renk", null, null },
                    { 17, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Renk Solması", null, null },
                    { 18, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Tülenme", null, null },
                    { 19, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Üründe Aşınma", null, null },
                    { 20, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Yırtılma", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_ReturnReasonId",
                table: "SaleItems",
                column: "ReturnReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnReasons_Name",
                table: "ReturnReasons",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_ReturnReasons_ReturnReasonId",
                table: "SaleItems",
                column: "ReturnReasonId",
                principalTable: "ReturnReasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_ReturnReasons_ReturnReasonId",
                table: "SaleItems");

            migrationBuilder.DropTable(
                name: "ReturnReasons");

            migrationBuilder.DropIndex(
                name: "IX_SaleItems_ReturnReasonId",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ReturnReasonId",
                table: "SaleItems");
        }
    }
}
