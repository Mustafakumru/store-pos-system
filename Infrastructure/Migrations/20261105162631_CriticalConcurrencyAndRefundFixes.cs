using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CriticalConcurrencyAndRefundFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                table: "Sales",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Sales",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PersonelId",
                table: "AspNetUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            // ── Veri düzeltmeleri (unique index'lerden ÖNCE) ──────────────

            // 1) Mevcut fişlerin iade toplamı (kalemlerden)
            migrationBuilder.Sql(@"
                UPDATE s SET s.RefundedAmount = ISNULL(x.Total, 0)
                FROM Sales s
                CROSS APPLY (SELECT SUM(i.RefundedAmount) AS Total
                             FROM SaleItems i WHERE i.SaleId = s.Id) x;");

            // 2) Eşzamanlılık hatasıyla açılmış YİNELENEN açık kasa oturumları:
            //    kasiyer başına en eski açık oturum kalır (satışlar onun açılış
            //    anından itibaren sayılır), sonrakiler kapatılır.
            migrationBuilder.Sql(@"
                WITH d AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY StoreId, CashierId ORDER BY OpenedAt, Id) AS rn
                    FROM CashDrawerSessions
                    WHERE Status = 0 AND IsDeleted = 0)
                UPDATE c SET c.Status = 1, c.ClosedAt = SYSUTCDATETIME(), c.UpdatedAt = SYSUTCDATETIME(),
                             c.UpdatedBy = 'migration:duplicate-open-session'
                FROM CashDrawerSessions c JOIN d ON d.Id = c.Id
                WHERE d.rn > 1;");

            // 3) YİNELENEN PersonelId'ler: ilk kullanıcı numarasını korur, diğerlerine
            //    99xxxxx aralığından yeni numara verilir (yönetici kullanıcı listesinden
            //    görüp ilgili kişiye bildirmelidir).
            migrationBuilder.Sql(@"
                WITH d AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY PersonelId ORDER BY Id) AS rn
                    FROM AspNetUsers WHERE PersonelId IS NOT NULL),
                r AS (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY Id) AS n FROM d WHERE rn > 1)
                UPDATE u SET u.PersonelId = CONCAT('99', RIGHT(CONCAT('00000', r.n), 5))
                FROM AspNetUsers u JOIN r ON r.Id = u.Id;");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerSessions_OneOpenPerCashier",
                table: "CashDrawerSessions",
                columns: new[] { "StoreId", "CashierId" },
                unique: true,
                filter: "[Status] = 0 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PersonelId",
                table: "AspNetUsers",
                column: "PersonelId",
                unique: true,
                filter: "[PersonelId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashDrawerSessions_OneOpenPerCashier",
                table: "CashDrawerSessions");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PersonelId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Sales");

            migrationBuilder.AlterColumn<string>(
                name: "PersonelId",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
