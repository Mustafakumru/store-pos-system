using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "ProductVariants",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductVariantGroup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GroupType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Color"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariantGroup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductVariantGroup_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariantGroupImage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariantGroupImage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductVariantGroupImage_ProductVariantGroup_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ProductVariantGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_GroupId",
                table: "ProductVariants",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariantGroup_ProductId",
                table: "ProductVariantGroup",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariantGroupImage_GroupId",
                table: "ProductVariantGroupImage",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_ProductVariantGroup_GroupId",
                table: "ProductVariants",
                column: "GroupId",
                principalTable: "ProductVariantGroup",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_ProductVariantGroup_GroupId",
                table: "ProductVariants");

            migrationBuilder.DropTable(
                name: "ProductVariantGroupImage");

            migrationBuilder.DropTable(
                name: "ProductVariantGroup");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_GroupId",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "ProductVariants");
        }
    }
}
