using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleItemDiscountFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUserId",
                table: "SaleItems",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "SaleItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DiscountType",
                table: "SaleItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "SaleItems",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "SaleItems");
        }
    }
}
