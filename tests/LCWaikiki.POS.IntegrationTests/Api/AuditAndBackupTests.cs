using Domain.Entities;
using LCWaikiki.POS.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using static LCWaikiki.POS.IntegrationTests.Infrastructure.Scenario;

namespace LCWaikiki.POS.IntegrationTests.Api;

/// <summary>İşlem kaydı (kim / ne / ne zaman, maskeleme, yetki, değiştirilemezlik) ve yedekleme.</summary>
[Collection(ApiCollection.Name)]
public class AuditAndBackupTests
{
    private readonly PosApiFactory _factory;
    public AuditAndBackupTests(PosApiFactory factory) => _factory = factory;

    private async Task<Scenario> OpenStoreAsync()
    {
        var s = await CreateAsync(_factory, 100m);
        await s.OpenSessionAsync(s.Cashier);
        return s;
    }

    private Task<List<AuditLog>> LogsAsync(Func<IQueryable<AuditLog>, IQueryable<AuditLog>> filter)
        => _factory.QueryDbAsync(db => filter(db.AuditLogs.AsNoTracking()).OrderBy(a => a.Id).ToListAsync());

    // ── İstek kaydı ──────────────────────────────────────────────

    [Fact]
    public async Task Sale_is_recorded_with_cashier_store_and_receipt_number()
    {
        var s = await OpenStoreAsync();
        var sale = await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(100) });
        var receipt = sale.Str("receiptNumber");

        var log = Assert.Single(await LogsAsync(q => q.Where(a => a.Action == "Pos.ProcessSale" && a.EntityId == receipt)));
        Assert.True(log.Succeeded);
        Assert.Equal(200, log.StatusCode);
        Assert.Equal(s.Cashier.PersonelId, log.PersonelId);
        Assert.Equal(s.StoreId, log.StoreId);
        Assert.Equal($"Fiş {receipt}", log.Summary);
        Assert.Contains("\"quantity\":1", log.Details);
        Assert.True(DateTime.UtcNow - log.OccurredAt < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Rejected_refund_is_recorded_with_reason()
    {
        var s = await OpenStoreAsync();
        var res = await s.Api.Post("/api/pos/sales/YOK-FIS-123/refund", new
        {
            items = new[] { new { variantId = s.Variants[0], refundQuantity = 1, returnReasonId = s.ReturnReasonId } },
            reason = "test", payoutMethod = 0
        }, s.Cashier.Token);
        Assert.False(res.Ok);

        var log = (await LogsAsync(q => q.Where(a => a.Action == "Pos.ProcessRefund" && a.EntityId == "YOK-FIS-123"))).Last();
        Assert.False(log.Succeeded);
        Assert.Equal(res.Code, log.StatusCode);
        Assert.Equal(res.Message, log.Summary);
        Assert.Equal(s.Cashier.PersonelId, log.PersonelId);
    }

    [Fact]
    public async Task Failed_pos_login_is_recorded_with_personel_id_but_never_the_pin()
    {
        var s = await CreateAsync(_factory);
        const string wrongPin = "9137";
        var res = await s.Api.Post("/api/pos/auth/login", new { personelId = s.Cashier.PersonelId, pin = wrongPin });
        Assert.Equal(401, res.Code);

        var log = (await LogsAsync(q => q.Where(a => a.Action == "PosAuth.Login" && a.PersonelId == s.Cashier.PersonelId))).Last();
        Assert.False(log.Succeeded);
        Assert.Null(log.UserId);                       // kimliksiz deneme
        Assert.Contains("\"pin\":\"***\"", log.Details);
        Assert.DoesNotContain(wrongPin, log.Details);
        Assert.DoesNotContain(s.Cashier.Pin, log.Details ?? "");
    }

    [Fact]
    public async Task Approval_and_gift_card_records_hide_pin_token_and_card_number()
    {
        var s = await OpenStoreAsync();
        await s.ApprovalAsync("discount", s.Cashier);
        var card = "HK" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var sold = await s.Api.Post("/api/pos/giftcards/sell",
            new { lines = new[] { new { cardNumber = card, amount = 50 } }, payments = new[] { Cash(50) } }, s.Cashier.Token);
        Assert.True(sold.Ok, sold.ToString());

        var logs = await LogsAsync(q => q.Where(a => a.PersonelId == s.Cashier.PersonelId
            && (a.Action == "ManagerApproval.RequestApproval" || a.Action == "GiftCards.Sell")));
        var approval = logs.Last(l => l.Action == "ManagerApproval.RequestApproval");
        var giftCard = logs.Last(l => l.Action == "GiftCards.Sell");

        Assert.DoesNotContain(s.Manager.Pin, approval.Details);
        Assert.Contains(s.Manager.PersonelId, approval.Details);      // kimin onayladığı görünür
        Assert.DoesNotContain(card, giftCard.Details);
        Assert.Contains(card[^4..], giftCard.Details);
    }

    [Fact]
    public async Task Failed_panel_login_shows_who_tried_but_not_the_password()
    {
        var api = new ApiClient(_factory.CreateClient());
        const string wrong = "Yanlis-Sifre-123";
        var res = await api.Post("/api/auth/login", new { email = Scenario.AdminEmail, password = wrong });
        Assert.False(res.Ok);

        var log = (await LogsAsync(q => q.Where(a => a.Action == "Auth.Login" && !a.Succeeded))).Last();
        Assert.Equal(Scenario.AdminEmail, log.UserName);       // kim denedi: tam iş e-postası
        Assert.DoesNotContain(wrong, log.Details);
        Assert.Contains("\"password\":\"***\"", log.Details);
    }

    // ── Alan değişikliği kaydı ───────────────────────────────────

    [Fact]
    public async Task Coupon_deactivation_records_old_and_new_value_linked_to_request()
    {
        var s = await CreateAsync(_factory);
        var coupon = await s.Api.Post("/api/coupons", new
        {
            code = "AUD" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), description = "kayıt testi",
            discountType = 0, value = 10, minPurchaseAmount = (decimal?)null, maxDiscountAmount = (decimal?)null,
            startDate = (DateTime?)null, endDate = (DateTime?)null, usageLimit = (int?)null
        }, s.AdminToken);
        Assert.True(coupon.Ok, coupon.ToString());
        var id = coupon.Int("id");

        Assert.True((await s.Api.Put($"/api/coupons/{id}/active", new { isActive = false }, s.AdminToken)).Ok);

        var change = Assert.Single(await LogsAsync(q => q.Where(a =>
            a.Category == AuditCategory.Change && a.EntityType == "Coupon" && a.EntityId == id.ToString())));
        Assert.Equal("Coupon.Updated", change.Action);
        Assert.Contains("\"IsActive\":{\"eski\":\"True\",\"yeni\":\"False\"}", change.Details);
        Assert.False(string.IsNullOrEmpty(change.UserId));
        Assert.NotEqual("system", change.UserId);           // değiştiren admin

        var request = Assert.Single(await LogsAsync(q => q.Where(a => a.CorrelationId == change.CorrelationId && a.Category == AuditCategory.Request)));
        Assert.Equal("Coupons.SetActive", request.Action);
    }

    [Fact]
    public async Task User_deactivation_and_pin_reset_are_recorded_without_hash_values()
    {
        var s = await CreateAsync(_factory);
        Assert.True((await s.Api.Post($"/api/pos/auth/reset-pin/{s.Cashier.UserId}", new { }, s.Manager.Token)).Ok);
        Assert.True((await s.Api.Patch($"/api/auth/users/{s.Cashier.UserId}/toggle-status", null, s.Manager.Token)).Ok);

        var changes = await LogsAsync(q => q.Where(a => a.Category == AuditCategory.Change
            && a.EntityType == "AppUser" && a.EntityId == s.Cashier.UserId));
        Assert.All(changes, c => Assert.DoesNotContain("PBKDF2", c.Details, StringComparison.OrdinalIgnoreCase));

        // Kurulumda kasiyerin KENDİ PIN değişikliği kendi adıyla; sıfırlama ve pasife alma müdürün adıyla
        Assert.Contains(changes, c => c.PersonelId == s.Cashier.PersonelId && c.Details!.Contains("\"PinHash\":\"(değişti)\""));
        var byManager = changes.Where(c => c.PersonelId == s.Manager.PersonelId).ToList();
        Assert.Contains(byManager, c => c.Details!.Contains("\"PinHash\":\"(değişti)\""));
        Assert.Contains(byManager, c => c.Details!.Contains("\"IsActive\":{\"eski\":\"True\",\"yeni\":\"False\"}"));
    }

    // ── Görüntüleme yetkisi ──────────────────────────────────────

    [Fact]
    public async Task Manager_sees_only_own_store_cashier_is_forbidden_admin_sees_all()
    {
        var a = await OpenStoreAsync();
        var b = await OpenStoreAsync();
        await a.SaleAsync(a.Cashier, new[] { Item(a.Variants[0]) }, new[] { Cash(100) });
        await b.SaleAsync(b.Cashier, new[] { Item(b.Variants[0]) }, new[] { Cash(100) });

        var managerView = await a.Api.Get($"/api/audit-logs?storeId={b.StoreId}&pageSize=200", a.Manager.Token);
        Assert.True(managerView.Ok, managerView.ToString());
        var stores = managerView.Body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("storeId").GetInt32()).Distinct().ToList();
        Assert.Equal([a.StoreId], stores);   // storeId=b yok sayıldı

        Assert.Equal(403, (await a.Api.Get("/api/audit-logs", a.Cashier.Token)).Code);
        Assert.Equal(401, (await a.Api.Get("/api/audit-logs")).Code);

        var adminView = await a.Api.Get($"/api/audit-logs?storeId={b.StoreId}&action=Pos.ProcessSale", a.AdminToken);
        var item = adminView.Body.GetProperty("items")[0];
        Assert.Equal(b.StoreId, item.GetProperty("storeId").GetInt32());
        Assert.Equal("Satış", item.GetProperty("actionLabel").GetString());
        Assert.False(string.IsNullOrEmpty(item.GetProperty("storeName").GetString()));
    }

    // ── Değiştirilemezlik ────────────────────────────────────────

    [Fact]
    public async Task Audit_records_cannot_be_updated_or_deleted_even_with_direct_sql()
    {
        var s = await OpenStoreAsync();
        await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(100) });
        var id = (await LogsAsync(q => q.Where(a => a.PersonelId == s.Cashier.PersonelId))).Last().Id;

        await _factory.QueryDbAsync(async db =>
        {
            var upd = await Assert.ThrowsAsync<SqlException>(() =>
                db.Database.ExecuteSqlRawAsync("UPDATE AuditLogs SET Summary = N'silindi' WHERE Id = {0}", id));
            Assert.Contains("append-only", upd.Message);
            await Assert.ThrowsAsync<SqlException>(() =>
                db.Database.ExecuteSqlRawAsync("DELETE FROM AuditLogs WHERE Id = {0}", id));
            return 0;
        });
        Assert.Single(await LogsAsync(q => q.Where(a => a.Id == id && a.Summary != "silindi")));
    }

    // ── Yedekleme ────────────────────────────────────────────────

    [Fact]
    public async Task Admin_backup_is_verified_restorable_and_recorded_others_forbidden()
    {
        var s = await OpenStoreAsync();
        await s.SaleAsync(s.Cashier, new[] { Item(s.Variants[0]) }, new[] { Cash(100) });

        Assert.Equal(403, (await s.Api.Post("/api/backups/run", new { }, s.Manager.Token)).Code);
        Assert.Equal(403, (await s.Api.Post("/api/backups/run", new { }, s.Cashier.Token)).Code);

        var run = await s.Api.Post("/api/backups/run", new { }, s.AdminToken);
        Assert.True(run.Ok, run.ToString());
        Assert.True(run.Bool("succeeded"));
        Assert.True(run.Bool("verified"));
        Assert.True(run.Body.GetProperty("sizeBytes").GetInt64() > 0);
        var path = run.Str("filePath");

        var status = await s.Api.Get("/api/backups", s.AdminToken);
        Assert.Equal(path, status.Body.GetProperty("history")[0].GetProperty("filePath").GetString());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, status.Body.GetProperty("lastSuccessAt").ValueKind);
        Assert.Contains(await LogsAsync(q => q.Where(a => a.Action == "Backups.Run" && a.Succeeded)),
            l => l.UserId is not null && l.StatusCode == 200);

        // Yedekten gerçekten geri dönülebiliyor mu: ayrı bir veritabanına aç, satış sayısını karşılaştır
        var restored = "LCWaikikiPOS_RestoreTest_" + Guid.NewGuid().ToString("N")[..8];
        await _factory.QueryDbAsync(async db =>
        {
            var expectedSales = await db.Sales.IgnoreQueryFilters().CountAsync();
            var files = await db.Database.SqlQueryRaw<FileRow>(
                "RESTORE FILELISTONLY FROM DISK = {0}", path).ToListAsync();
            var dataDir = await db.Database.SqlQueryRaw<string>(
                "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400)) AS Value").SingleAsync();
            var sep = dataDir.Contains('/') ? "/" : "\\";
            var moves = string.Join(", ", files.Select(f =>
                $"MOVE N'{f.LogicalName}' TO N'{dataDir.TrimEnd('/', '\\')}{sep}{restored}_{f.LogicalName}{(f.Type == "L" ? ".ldf" : ".mdf")}'"));
            try
            {
                await db.Database.ExecuteSqlRawAsync($"RESTORE DATABASE [{restored}] FROM DISK = N'{path}' WITH {moves}");
                var restoredSales = await db.Database.SqlQueryRaw<int>(
                    $"SELECT COUNT(*) AS Value FROM [{restored}].dbo.Sales").SingleAsync();
                Assert.True(restoredSales > 0);
                Assert.Equal(expectedSales, restoredSales);
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync(
                    $"IF DB_ID('{restored}') IS NOT NULL BEGIN ALTER DATABASE [{restored}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{restored}]; END");
                if (File.Exists(path)) File.Delete(path);
            }
            return 0;
        });
    }

    public sealed class FileRow
    {
        public string LogicalName { get; set; } = "";
        public string Type { get; set; } = "";
    }
}
