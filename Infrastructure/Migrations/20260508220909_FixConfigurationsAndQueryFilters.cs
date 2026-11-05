using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixConfigurationsAndQueryFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "WarehouseStocks");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StoreInventories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "WarehouseStocks",
                type: "bigint",
                rowVersion: true,
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "StoreInventories",
                type: "bigint",
                rowVersion: true,
                nullable: false,
                defaultValue: 0L);
        }
    }
}
