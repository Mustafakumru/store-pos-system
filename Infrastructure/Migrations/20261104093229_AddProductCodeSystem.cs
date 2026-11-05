using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCodeSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId1",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_CategoryId1",
                table: "Products");

            // Eski hatali golge iliski kolonu (tum satirlarda NULL) kaldirilir
            migrationBuilder.DropColumn(
                name: "CategoryId1",
                table: "Products");

            migrationBuilder.AddColumn<int>(
                name: "SeasonYear",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AgeGroup",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Products",
                type: "varchar(8)",
                unicode: false,
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Department",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Season",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Categories",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CodeSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Scope = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentValue = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeSequences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Code",
                table: "Products",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Code",
                table: "Categories",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CodeSequences_Scope",
                table: "CodeSequences",
                column: "Scope",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodeSequences");

            migrationBuilder.DropIndex(
                name: "IX_Products_Code",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Code",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "AgeGroup",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Season",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "SeasonYear",
                table: "Products");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId1",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId1",
                table: "Products",
                column: "CategoryId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId1",
                table: "Products",
                column: "CategoryId1",
                principalTable: "Categories",
                principalColumn: "Id");
        }
    }
}
