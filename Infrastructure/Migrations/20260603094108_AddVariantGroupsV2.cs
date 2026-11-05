using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantGroupsV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariantGroup_Products_ProductId",
                table: "ProductVariantGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariantGroupImage_ProductVariantGroup_GroupId",
                table: "ProductVariantGroupImage");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_ProductVariantGroup_GroupId",
                table: "ProductVariants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductVariantGroupImage",
                table: "ProductVariantGroupImage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductVariantGroup",
                table: "ProductVariantGroup");

            migrationBuilder.RenameTable(
                name: "ProductVariantGroupImage",
                newName: "ProductVariantGroupImages");

            migrationBuilder.RenameTable(
                name: "ProductVariantGroup",
                newName: "ProductVariantGroups");

            migrationBuilder.RenameIndex(
                name: "IX_ProductVariantGroupImage_GroupId",
                table: "ProductVariantGroupImages",
                newName: "IX_ProductVariantGroupImages_GroupId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductVariantGroup_ProductId",
                table: "ProductVariantGroups",
                newName: "IX_ProductVariantGroups_ProductId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductVariantGroupImages",
                table: "ProductVariantGroupImages",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductVariantGroups",
                table: "ProductVariantGroups",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariantGroupImages_ProductVariantGroups_GroupId",
                table: "ProductVariantGroupImages",
                column: "GroupId",
                principalTable: "ProductVariantGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariantGroups_Products_ProductId",
                table: "ProductVariantGroups",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_ProductVariantGroups_GroupId",
                table: "ProductVariants",
                column: "GroupId",
                principalTable: "ProductVariantGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariantGroupImages_ProductVariantGroups_GroupId",
                table: "ProductVariantGroupImages");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariantGroups_Products_ProductId",
                table: "ProductVariantGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_ProductVariantGroups_GroupId",
                table: "ProductVariants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductVariantGroups",
                table: "ProductVariantGroups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductVariantGroupImages",
                table: "ProductVariantGroupImages");

            migrationBuilder.RenameTable(
                name: "ProductVariantGroups",
                newName: "ProductVariantGroup");

            migrationBuilder.RenameTable(
                name: "ProductVariantGroupImages",
                newName: "ProductVariantGroupImage");

            migrationBuilder.RenameIndex(
                name: "IX_ProductVariantGroups_ProductId",
                table: "ProductVariantGroup",
                newName: "IX_ProductVariantGroup_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductVariantGroupImages_GroupId",
                table: "ProductVariantGroupImage",
                newName: "IX_ProductVariantGroupImage_GroupId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductVariantGroup",
                table: "ProductVariantGroup",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductVariantGroupImage",
                table: "ProductVariantGroupImage",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariantGroup_Products_ProductId",
                table: "ProductVariantGroup",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariantGroupImage_ProductVariantGroup_GroupId",
                table: "ProductVariantGroupImage",
                column: "GroupId",
                principalTable: "ProductVariantGroup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_ProductVariantGroup_GroupId",
                table: "ProductVariants",
                column: "GroupId",
                principalTable: "ProductVariantGroup",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
